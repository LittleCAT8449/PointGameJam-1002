using UnityEngine;

/// <summary>
/// Attach to a GameObject with a SpriteRenderer. A single child SpriteRenderer
/// uses a shader to overlay a brightness-driven halftone pattern.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public sealed class ImageBrightnessDotMatrix : MonoBehaviour, INoteWaveRippleReceiver2D, INoteWaveContactPoint2D
{
    [Header("Halftone")]
    [Tooltip("Source pixels between dot centers. Increase this to make the pattern sparser.")]
    [SerializeField, Min(1)] private int sampleStep = 12;
    [Tooltip("Maximum per-dot offset as a fraction of the grid cell. Set to 0 for a regular grid.")]
    [SerializeField, Range(0f, 0.5f)] private float jitterStrength = 0.15f;
    [SerializeField, Range(0f, 1f)] private float minimumDotRatio = 0.08f;
    [SerializeField, Range(0.05f, 1.5f)] private float maximumDotRatio = 0.95f;
    [Tooltip("Enable for dark dots on bright areas. Leave off for bright dots on dark areas.")]
    [SerializeField] private bool darkPixelsLarger;
    [Tooltip("Keeps the original image visible underneath the dots.")]
    [SerializeField] private bool showOriginalImageUnderDots = true;

    [Header("Appearance")]
    [SerializeField] private Color dotColor = new Color(0.2f, 1f, 0.35f, 1f);
    [Tooltip("Optional. If empty, the HalftoneSprite material in Resources is used.")]
    [SerializeField] private Material halftoneMaterial;
    [SerializeField] private int sortingOrderOffset = 1;

    [Header("Wave Response")]
    [Tooltip("When enabled, note waves create a traveling ripple in this object's dots.")]
    [SerializeField] private bool waveRippleEnabled = true;
    [Tooltip("How long the Reveal note keeps this object's dots aligned.")]
    [SerializeField, Min(0f)] private float revealRegularDuration = 1.2f;

    private static readonly int GridCountId = Shader.PropertyToID("_GridCount");
    private static readonly int SpriteBoundsId = Shader.PropertyToID("_SpriteBounds");
    private static readonly int DotRatioId = Shader.PropertyToID("_DotRatio");
    private static readonly int DotColorId = Shader.PropertyToID("_DotColor");
    private static readonly int JitterStrengthId = Shader.PropertyToID("_JitterStrength");
    private static readonly int DarkPixelsLargerId = Shader.PropertyToID("_DarkPixelsLarger");
    private static readonly int RevealRegularityId = Shader.PropertyToID("_RevealRegularity");

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer overlayRenderer;
    private GameObject overlayObject;
    private Material runtimeFallbackMaterial;
    private Material resourceMaterial;
    private bool searchedResources;
    private MaterialPropertyBlock propertyBlock;
    private bool sourceWasEnabled;
    private bool cachedVisibility;
    private Vector2 rippleOrigin;
    private float rippleStartTime;
    private float rippleSpeed;
    private float rippleMaxRadius;
    private bool hasWaveRipple;
    private bool lastWaveRippleEnabled;
    private float revealRegularStartTime = -1f;
    private float revealRegularEndTime = -1f;
    private float currentRevealRegularity;
    private float lastAppliedRevealRegularity = -1f;

    private Sprite lastSprite;
    private Material lastMaterial;
    private int lastSampleStep = -1;
    private int lastSortingOrder = int.MinValue;
    private int lastSortingLayer;
    private bool lastFlipX;
    private bool lastFlipY;
    private bool lastDarkPixelsLarger;
    private float lastMinimumDotRatio = -1f;
    private float lastMaximumDotRatio = -1f;
    private float lastJitterStrength = -1f;
    private Color lastDotColor;

    private void Awake()
    {
        sourceRenderer = GetComponent<SpriteRenderer>();
        sourceWasEnabled = sourceRenderer.enabled;
        propertyBlock = new MaterialPropertyBlock();
        lastWaveRippleEnabled = waveRippleEnabled;
    }

    private void Start()
    {
        RefreshOverlay();
    }

    private void Update()
    {
        if (sourceRenderer == null)
            return;

        UpdateWaveRippleToggle();
        UpdateRevealRegularity();

        Material material = ResolveMaterial();
        bool spriteChanged = sourceRenderer.sprite != lastSprite;
        bool materialChanged = material != lastMaterial;
        bool geometryChanged = sourceRenderer.flipX != lastFlipX || sourceRenderer.flipY != lastFlipY;
        bool sortingChanged = sourceRenderer.sortingOrder != lastSortingOrder
            || sourceRenderer.sortingLayerID != lastSortingLayer;

        if (spriteChanged || materialChanged || geometryChanged || sortingChanged
            || sampleStep != lastSampleStep)
        {
            RefreshOverlay();
            return;
        }

        if (showOriginalImageUnderDots != cachedVisibility)
            ApplyVisibility();

        if (!Mathf.Approximately(lastMinimumDotRatio, minimumDotRatio)
            || !Mathf.Approximately(lastMaximumDotRatio, maximumDotRatio)
            || !Mathf.Approximately(lastJitterStrength, jitterStrength)
            || lastDarkPixelsLarger != darkPixelsLarger
            || lastDotColor != dotColor)
        {
            ApplyShaderSettings();
        }
    }

    /// <summary>Recreate the overlay after changing the source Sprite or material.</summary>
    public void RefreshOverlay()
    {
        if (sourceRenderer == null)
            sourceRenderer = GetComponent<SpriteRenderer>();
        if (sourceRenderer == null || sourceRenderer.sprite == null)
        {
            Debug.LogWarning($"{nameof(ImageBrightnessDotMatrix)} requires a Sprite on its SpriteRenderer.", this);
            return;
        }

        Material material = ResolveMaterial();
        if (material == null)
        {
            Debug.LogError($"{nameof(ImageBrightnessDotMatrix)} could not find the HalftoneSprite shader/material.", this);
            return;
        }

        if (overlayObject == null)
        {
            overlayObject = new GameObject("Halftone Shader Overlay");
            overlayObject.layer = gameObject.layer;
            overlayObject.transform.SetParent(transform, false);
            overlayRenderer = overlayObject.AddComponent<SpriteRenderer>();
        }

        overlayRenderer.sprite = sourceRenderer.sprite;
        overlayRenderer.sharedMaterial = material;
        overlayRenderer.color = sourceRenderer.color;
        overlayRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        overlayRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
        overlayRenderer.maskInteraction = sourceRenderer.maskInteraction;
        overlayRenderer.spriteSortPoint = sourceRenderer.spriteSortPoint;
        overlayRenderer.flipX = sourceRenderer.flipX;
        overlayRenderer.flipY = sourceRenderer.flipY;
        overlayRenderer.drawMode = sourceRenderer.drawMode;
        overlayRenderer.size = sourceRenderer.size;
        overlayRenderer.enabled = sourceWasEnabled;

        ApplyVisibility();
        ApplyShaderSettings();

        lastSprite = sourceRenderer.sprite;
        lastMaterial = material;
        lastSampleStep = sampleStep;
        lastSortingOrder = sourceRenderer.sortingOrder;
        lastSortingLayer = sourceRenderer.sortingLayerID;
        lastFlipX = sourceRenderer.flipX;
        lastFlipY = sourceRenderer.flipY;
    }

    private Material ResolveMaterial()
    {
        if (halftoneMaterial != null)
            return halftoneMaterial;

        if (!searchedResources) { resourceMaterial = Resources.Load<Material>("HalftoneSprite"); searchedResources = true; }
        if (resourceMaterial != null)
            return resourceMaterial;

        if (runtimeFallbackMaterial == null)
        {
            Shader shader = Shader.Find("PointGameJam/Halftone Sprite");
            if (shader != null)
                runtimeFallbackMaterial = new Material(shader) { name = "HalftoneSprite (Runtime)" };
        }

        return runtimeFallbackMaterial;
    }

    private void ApplyShaderSettings()
    {
        if (overlayRenderer == null || sourceRenderer == null || sourceRenderer.sprite == null)
            return;

        Sprite sprite = sourceRenderer.sprite;
        Rect pixelRect = sprite.rect;
        int step = Mathf.Max(1, sampleStep);
        float columns = Mathf.Max(1, Mathf.Ceil(pixelRect.width / step));
        float rows = Mathf.Max(1, Mathf.Ceil(pixelRect.height / step));

        Bounds bounds = sprite.bounds;
        Vector3 boundsMin = bounds.min;
        Vector3 boundsSize = bounds.size;

        propertyBlock.Clear();
        propertyBlock.SetVector(GridCountId, new Vector4(columns, rows, 0f, 0f));
        propertyBlock.SetVector(SpriteBoundsId, new Vector4(
            boundsMin.x, boundsMin.y, boundsSize.x, boundsSize.y));
        propertyBlock.SetVector(DotRatioId, new Vector4(
            Mathf.Min(minimumDotRatio, maximumDotRatio),
            Mathf.Max(minimumDotRatio, maximumDotRatio), 0f, 0f));
        propertyBlock.SetFloat(JitterStrengthId, jitterStrength);
        propertyBlock.SetColor(DotColorId, dotColor);
        propertyBlock.SetFloat(DarkPixelsLargerId, darkPixelsLarger ? 1f : 0f);
        propertyBlock.SetFloat(RevealRegularityId, currentRevealRegularity);
        if (hasWaveRipple && waveRippleEnabled)
            NoteWaveRippleShader2D.Apply(propertyBlock, rippleOrigin, rippleStartTime, rippleSpeed, rippleMaxRadius);
        overlayRenderer.SetPropertyBlock(propertyBlock);
        lastAppliedRevealRegularity = currentRevealRegularity;

        lastMinimumDotRatio = minimumDotRatio;
        lastMaximumDotRatio = maximumDotRatio;
        lastJitterStrength = jitterStrength;
        lastDarkPixelsLarger = darkPixelsLarger;
        lastDotColor = dotColor;
    }

    public Vector2 GetWaveContactPoint(Vector2 origin)
    {
        if (sourceRenderer == null)
            sourceRenderer = GetComponent<SpriteRenderer>();
        if (sourceRenderer == null)
            return transform.position;

        Bounds bounds = sourceRenderer.bounds;
        Vector3 closestPoint = bounds.ClosestPoint(new Vector3(origin.x, origin.y, bounds.center.z));
        return new Vector2(closestPoint.x, closestPoint.y);
    }

    public void OnNoteWaveReached(NoteWaveKind kind, Vector2 origin, float startTime, float expansionSpeed, float maxRadius)
    {
        if (kind == NoteWaveKind.Reveal)
        {
            revealRegularStartTime = Time.time;
            revealRegularEndTime = revealRegularStartTime + Mathf.Max(0f, revealRegularDuration);
            UpdateRevealRegularity();
        }

        if (!waveRippleEnabled)
        {
            hasWaveRipple = false;
            SetRippleEnabled(false);
            return;
        }

        rippleOrigin = origin;
        rippleStartTime = startTime;
        rippleSpeed = expansionSpeed;
        rippleMaxRadius = maxRadius;
        hasWaveRipple = true;

        if (overlayRenderer == null || propertyBlock == null)
            return;

        overlayRenderer.GetPropertyBlock(propertyBlock);
        NoteWaveRippleShader2D.Apply(propertyBlock, rippleOrigin, rippleStartTime, rippleSpeed, rippleMaxRadius);
        overlayRenderer.SetPropertyBlock(propertyBlock);
    }

    private void UpdateWaveRippleToggle()
    {
        if (lastWaveRippleEnabled == waveRippleEnabled)
            return;

        lastWaveRippleEnabled = waveRippleEnabled;
        if (!waveRippleEnabled)
            hasWaveRipple = false;

        SetRippleEnabled(waveRippleEnabled && hasWaveRipple);
    }

    private void SetRippleEnabled(bool enabled)
    {
        if (overlayRenderer == null || propertyBlock == null)
            return;

        overlayRenderer.GetPropertyBlock(propertyBlock);
        NoteWaveRippleShader2D.SetEnabled(propertyBlock, enabled);
        overlayRenderer.SetPropertyBlock(propertyBlock);
    }

    private void UpdateRevealRegularity()
    {
        float regularity = 0f;
        float duration = revealRegularEndTime - revealRegularStartTime;
        if (duration > 0f && Time.time < revealRegularEndTime)
        {
            float transition = Mathf.Min(0.15f, duration * 0.25f);
            if (transition > 0f)
            {
                float elapsed = Time.time - revealRegularStartTime;
                float remaining = revealRegularEndTime - Time.time;
                float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transition));
                float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(remaining / transition));
                regularity = Mathf.Min(fadeIn, fadeOut);
            }
            else
            {
                regularity = 1f;
            }
        }

        if (Mathf.Approximately(currentRevealRegularity, regularity))
            return;

        currentRevealRegularity = regularity;
        if (overlayRenderer == null || propertyBlock == null
            || Mathf.Approximately(lastAppliedRevealRegularity, currentRevealRegularity))
        {
            return;
        }

        overlayRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(RevealRegularityId, currentRevealRegularity);
        overlayRenderer.SetPropertyBlock(propertyBlock);
        lastAppliedRevealRegularity = currentRevealRegularity;
    }

    private void ApplyVisibility()
    {
        if (sourceRenderer != null)
            sourceRenderer.enabled = showOriginalImageUnderDots && sourceWasEnabled;
        if (overlayRenderer != null)
            overlayRenderer.enabled = sourceWasEnabled;

        cachedVisibility = showOriginalImageUnderDots;
    }

    private void OnDestroy()
    {
        if (sourceRenderer != null)
            sourceRenderer.enabled = sourceWasEnabled;

        if (overlayObject != null)
        {
            if (Application.isPlaying)
                Destroy(overlayObject);
            else
                DestroyImmediate(overlayObject);
        }

        if (runtimeFallbackMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeFallbackMaterial);
            else
                DestroyImmediate(runtimeFallbackMaterial);
        }
    }
}





