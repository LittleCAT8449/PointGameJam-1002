using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared expanding circular wave for the discover and state-switch notes.
/// Each matching target reacts once as the wave reaches it.
/// </summary>
[DisallowMultipleComponent]
public sealed class ExpandingNoteWave2D : MonoBehaviour
{
    private const int RingTextureSize = 256;
    private static Sprite sharedRingSprite;

    private SpriteRenderer ringRenderer;
    private INoteWaveReceiver2D[] targets;
    private readonly HashSet<INoteWaveReceiver2D> contactedTargets = new HashSet<INoteWaveReceiver2D>();
    private Vector2 origin;
    private NoteWaveKind waveKind;
    private float maxRadius;
    private float expansionSpeed;
    private float radius;
    private int respondedTargetCount;
    private bool debugDisplay;
    private bool initialized;
    private GUIStyle debugLabelStyle;

    public void Initialize(NoteWaveKind kind, float waveMaxRadius, float waveExpansionSpeed, Color color, bool showDebug)
    {
        origin = transform.position;
        waveKind = kind;
        maxRadius = Mathf.Max(0.1f, waveMaxRadius);
        expansionSpeed = Mathf.Max(0.1f, waveExpansionSpeed);
        debugDisplay = showDebug;
        targets = FindReceivers();

        ringRenderer = gameObject.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = GetRingSprite();
        ringRenderer.color = color;
        ringRenderer.sortingOrder = 100;
        initialized = true;
        UpdateRingScale();
    }

    private void Update()
    {
        if (!initialized)
            return;

        radius = Mathf.Min(maxRadius, radius + expansionSpeed * Time.deltaTime);
        UpdateRingScale();
        AffectReachedTargets();

        if (radius >= maxRadius)
            Destroy(gameObject);
    }

    private void AffectReachedTargets()
    {
        if (targets == null)
            return;

        foreach (INoteWaveReceiver2D target in targets)
        {
            if (target == null || contactedTargets.Contains(target) || !target.IsWaveAvailable)
                continue;

            float contactDistance = target.GetWaveContactDistance(origin);
            if (contactDistance > radius)
                continue;

            contactedTargets.Add(target);
            bool responded = target.ReceiveWave(waveKind);
            if (responded)
                respondedTargetCount++;

            if (debugDisplay)
            {
                Object targetObject = target as Object;
                string targetName = targetObject != null ? targetObject.name : "unknown target";
                string response = responded ? "responded" : "ignored this note";
                Debug.Log($"[{GetNoteLabel()}] Wave contacted '{targetName}' at radius {radius:F2}; target {response}.", targetObject);
            }
        }
    }

    private static INoteWaveReceiver2D[] FindReceivers()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        List<INoteWaveReceiver2D> foundTargets = new List<INoteWaveReceiver2D>();

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is INoteWaveReceiver2D target)
                foundTargets.Add(target);
        }

        return foundTargets.ToArray();
    }

    private string GetNoteLabel()
    {
        return waveKind switch
        {
            NoteWaveKind.Reveal => "显",
            NoteWaveKind.Switch => "转",
            NoteWaveKind.Gather => "聚",
            NoteWaveKind.Scatter => "散",
            NoteWaveKind.Connect => "连",
            _ => waveKind.ToString()
        };
    }

    private void UpdateRingScale()
    {
        // The generated sprite is two world units wide at scale one.
        transform.localScale = Vector3.one * radius;
    }

    private void OnDrawGizmos()
    {
        if (!debugDisplay || !initialized)
            return;

        Vector3 center = new Vector3(origin.x, origin.y, transform.position.z);
        Gizmos.color = new Color(1f, 0.7f, 0.15f, 0.75f);
        DrawCircle(center, maxRadius, true);

        Gizmos.color = waveKind switch
        {
            NoteWaveKind.Reveal => new Color(0.15f, 0.9f, 1f, 1f),
            NoteWaveKind.Switch => new Color(1f, 0.45f, 0.2f, 1f),
            NoteWaveKind.Gather => new Color(0.32f, 1f, 0.58f, 1f),
            NoteWaveKind.Scatter => new Color(1f, 0.42f, 0.9f, 1f),
            NoteWaveKind.Connect => new Color(0.55f, 0.9f, 1f, 1f),
            _ => Color.white
        };
        DrawCircle(center, radius, false);
        Gizmos.DrawSphere(center, 0.08f);

        if (targets == null)
            return;

        foreach (INoteWaveReceiver2D target in targets)
        {
            Component targetObject = target as Component;
            if (targetObject == null)
                continue;

            Transform targetTransform = targetObject.transform;
            Vector3 targetPosition = targetTransform.position;
            Gizmos.color = contactedTargets.Contains(target) ? Color.green : Color.magenta;
            Gizmos.DrawWireSphere(targetPosition, 0.12f);
            Gizmos.DrawLine(center, targetPosition);
        }
    }

    private void OnGUI()
    {
        if (!debugDisplay || !initialized)
            return;

        Color previousColor = GUI.color;
        GUI.color = Color.white;
        Rect panelRect = new Rect(8f, 8f, 600f, 36f);
        GUI.Box(panelRect, GUIContent.none);
        if (debugLabelStyle == null)
        {
            debugLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            debugLabelStyle.normal.textColor = Color.white;
        }

        int availableCount = 0;
        if (targets != null)
        {
            foreach (INoteWaveReceiver2D target in targets)
            {
                if (target != null && target.IsWaveAvailable)
                    availableCount++;
            }
        }

        GUI.Label(
            new Rect(16f, 10f, 584f, 32f),
            $"{GetNoteLabel()} Debug | 半径 {radius:F1}/{maxRadius:F1} | 命中 {contactedTargets.Count} | 响应 {respondedTargetCount} | 接收器 {targets?.Length ?? 0} | 可响应 {availableCount}",
            debugLabelStyle);
        GUI.color = previousColor;
    }

    private static void DrawCircle(Vector3 center, float circleRadius, bool dashed)
    {
        const int segments = 64;
        float angleStep = Mathf.PI * 2f / segments;

        for (int i = 0; i < segments; i++)
        {
            if (dashed && i % 2 != 0)
                continue;

            float startAngle = i * angleStep;
            float endAngle = (i + 1) * angleStep;
            Vector3 start = center + new Vector3(Mathf.Cos(startAngle), Mathf.Sin(startAngle), 0f) * circleRadius;
            Vector3 end = center + new Vector3(Mathf.Cos(endAngle), Mathf.Sin(endAngle), 0f) * circleRadius;
            Gizmos.DrawLine(start, end);
        }
    }

    private static Sprite GetRingSprite()
    {
        if (sharedRingSprite != null)
            return sharedRingSprite;

        Texture2D texture = new Texture2D(RingTextureSize, RingTextureSize, TextureFormat.RGBA32, false)
        {
            name = "Expanding Note Ring",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color32[] pixels = new Color32[RingTextureSize * RingTextureSize];
        float center = (RingTextureSize - 1f) * 0.5f;
        const float innerRadius = 122.25f;
        const float outerRadius = 125f;

        for (int y = 0; y < RingTextureSize; y++)
        {
            for (int x = 0; x < RingTextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float innerEdge = Mathf.SmoothStep(innerRadius - 0.6f, innerRadius + 0.6f, distance);
                float outerEdge = 1f - Mathf.SmoothStep(outerRadius - 0.6f, outerRadius + 0.6f, distance);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(innerEdge * outerEdge) * 255f);
                pixels[y * RingTextureSize + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        sharedRingSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, RingTextureSize, RingTextureSize),
            new Vector2(0.5f, 0.5f),
            RingTextureSize * 0.5f);
        sharedRingSprite.name = "Expanding Note Ring";
        sharedRingSprite.hideFlags = HideFlags.DontSave;
        return sharedRingSprite;
    }
}
