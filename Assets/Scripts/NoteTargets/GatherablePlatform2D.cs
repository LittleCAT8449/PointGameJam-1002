using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds a single point mesh and animates its points in a shader. The platform
/// collider becomes solid after the gather note finishes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class GatherablePlatform2D : MonoBehaviour, INoteWaveReceiver2D, INoteWaveRippleReceiver2D
{
    private static readonly int GatherProgressId = Shader.PropertyToID("_GatherProgress");
    private static readonly int DotSizeId = Shader.PropertyToID("_DotSize");
    private static readonly int DotColorId = Shader.PropertyToID("_DotColor");
    private static readonly int ScatteredAlphaId = Shader.PropertyToID("_ScatteredAlpha");

    private static Material runtimeMaterial;

    [Header("Platform")]
    [SerializeField] private Vector2 platformSize = new Vector2(3.2f, 0.4f);
    [SerializeField, Range(1, 128)] private int dotColumns = 32;
    [SerializeField, Range(1, 32)] private int dotRows = 4;
    [SerializeField, Min(0.01f)] private float dotSize = 0.085f;
    [SerializeField] private Color dotColor = new Color(0.32f, 1f, 0.58f, 1f);
    [SerializeField] private int sortingOrder = 3;
    [Tooltip("Leave empty to load the GatherPoints material from Resources.")]
    [SerializeField] private Material pointMaterial;

    [Header("Gather Animation")]
    [SerializeField, Min(0.1f)] private float gatherDuration = 0.9f;
    [SerializeField, Min(0f)] private float staggerDuration = 0.25f;
    [SerializeField, Min(0f)] private float scatterRadius = 1.35f;
    [SerializeField, Range(0f, 1f)] private float scatteredAlpha = 0.45f;

    private BoxCollider2D platformCollider;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Mesh pointMesh;
    private bool isGathering;
    private bool isGathered;
    private Vector2 rippleOrigin;
    private float rippleStartTime;
    private float rippleSpeed;
    private float rippleMaxRadius;
    private bool hasWaveRipple;

    public bool IsWaveAvailable => !isGathering && !isGathered;

    private void Awake()
    {
        platformCollider = GetComponent<BoxCollider2D>();
        platformCollider.size = platformSize;
        platformCollider.offset = Vector2.zero;
        platformCollider.enabled = false;

        CreatePointMesh();
        SetupRenderer();
        SetGatherProgress(0f);
    }

    public float GetWaveContactDistance(Vector2 origin)
    {
        Vector3 scale = transform.lossyScale;
        float worldScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        float cloudRadius = (scatterRadius + dotSize * 0.5f) * worldScale;
        return Mathf.Max(0f, Vector2.Distance(origin, transform.position) - cloudRadius);
    }

    public bool ReceiveWave(NoteWaveKind kind)
    {
        if (kind != NoteWaveKind.Gather || !IsWaveAvailable)
            return false;

        isGathering = true;
        StartCoroutine(GatherPoints());
        return true;
    }

    public void OnNoteWaveReached(NoteWaveKind kind, Vector2 origin, float startTime, float expansionSpeed, float maxRadius)
    {
        rippleOrigin = origin;
        rippleStartTime = startTime;
        rippleSpeed = expansionSpeed;
        rippleMaxRadius = maxRadius;
        hasWaveRipple = true;

        if (meshRenderer != null && propertyBlock != null)
        {
            NoteWaveRippleShader2D.Apply(propertyBlock, rippleOrigin, rippleStartTime, rippleSpeed, rippleMaxRadius);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void CreatePointMesh()
    {
        int columns = Mathf.Max(1, dotColumns);
        int rows = Mathf.Max(1, dotRows);
        int pointCount = columns * rows;
        int vertexCount = pointCount * 4;

        Vector3[] startCenters = new Vector3[vertexCount];
        Vector2[] targetCenters = new Vector2[vertexCount];
        Vector4[] animationData = new Vector4[vertexCount];
        Color[] vertexColors = new Color[vertexCount];
        int[] triangles = new int[pointCount * 6];
        Vector2[] corners =
        {
            new Vector2(-1f, -1f),
            new Vector2(-1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, -1f)
        };

        float totalDuration = Mathf.Max(0.1f, gatherDuration + staggerDuration);
        int vertex = 0;
        int triangle = 0;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Vector2 scatterOffset = Random.insideUnitCircle * scatterRadius;
                Vector2 targetCenter = new Vector2(
                    ((column + 0.5f) / columns - 0.5f) * platformSize.x,
                    ((row + 0.5f) / rows - 0.5f) * platformSize.y);
                float pointDelay = pointCount > 1
                    ? (staggerDuration / totalDuration) * (vertex / 4f) / (pointCount - 1f)
                    : 0f;

                for (int corner = 0; corner < 4; corner++)
                {
                    startCenters[vertex] = new Vector3(scatterOffset.x, scatterOffset.y, 0f);
                    targetCenters[vertex] = targetCenter;
                    animationData[vertex] = new Vector4(corners[corner].x, corners[corner].y, pointDelay, 0f);
                    vertexColors[vertex] = Color.white;
                    vertex++;
                }

                int first = vertex - 4;
                triangles[triangle++] = first;
                triangles[triangle++] = first + 1;
                triangles[triangle++] = first + 2;
                triangles[triangle++] = first;
                triangles[triangle++] = first + 2;
                triangles[triangle++] = first + 3;
            }
        }

        pointMesh = new Mesh
        {
            name = "Gatherable Platform Points",
            hideFlags = HideFlags.DontSave
        };
        pointMesh.vertices = startCenters;
        pointMesh.SetUVs(0, new List<Vector2>(targetCenters));
        pointMesh.SetUVs(1, new List<Vector4>(animationData));
        pointMesh.colors = vertexColors;
        pointMesh.triangles = triangles;

        const float rippleBoundsPadding = 0.1f;
        float halfWidth = Mathf.Max(platformSize.x * 0.5f, scatterRadius) + dotSize + rippleBoundsPadding;
        float halfHeight = Mathf.Max(platformSize.y * 0.5f, scatterRadius) + dotSize + rippleBoundsPadding;
        pointMesh.bounds = new Bounds(Vector3.zero, new Vector3(halfWidth * 2f, halfHeight * 2f, 0.1f));
    }

    private void SetupRenderer()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        Material material = ResolveMaterial();
        if (material == null)
        {
            Debug.LogError("Gather Points shader/material is missing.", this);
            return;
        }

        meshFilter.sharedMesh = pointMesh;
        meshRenderer.sharedMaterial = material;
        meshRenderer.sortingOrder = sortingOrder;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        propertyBlock = new MaterialPropertyBlock();
    }

    private Material ResolveMaterial()
    {
        if (pointMaterial != null)
            return pointMaterial;

        Material resourceMaterial = Resources.Load<Material>("GatherPoints");
        if (resourceMaterial != null)
            return resourceMaterial;

        if (runtimeMaterial != null)
            return runtimeMaterial;

        Shader shader = Shader.Find("PointGameJam/Gather Points");
        if (shader == null)
            return null;

        runtimeMaterial = new Material(shader)
        {
            name = "Gather Points (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        return runtimeMaterial;
    }

    private IEnumerator GatherPoints()
    {
        float elapsed = 0f;
        float totalDuration = Mathf.Max(0.1f, gatherDuration + staggerDuration);

        while (elapsed < totalDuration)
        {
            SetGatherProgress(elapsed / totalDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetGatherProgress(1f);
        isGathering = false;
        isGathered = true;
        platformCollider.enabled = true;
    }

    private void SetGatherProgress(float progress)
    {
        if (meshRenderer == null || propertyBlock == null)
            return;

        propertyBlock.Clear();
        propertyBlock.SetFloat(GatherProgressId, Mathf.Clamp01(progress));
        propertyBlock.SetFloat(DotSizeId, dotSize);
        propertyBlock.SetColor(DotColorId, dotColor);
        propertyBlock.SetFloat(ScatteredAlphaId, scatteredAlpha);
        if (hasWaveRipple)
            NoteWaveRippleShader2D.Apply(propertyBlock, rippleOrigin, rippleStartTime, rippleSpeed, rippleMaxRadius);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.32f, 1f, 0.58f, 0.8f);
        Gizmos.DrawWireCube(transform.position, new Vector3(platformSize.x, platformSize.y, 0f));
        Gizmos.color = new Color(0.32f, 1f, 0.58f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, scatterRadius);
    }

    private void OnDestroy()
    {
        if (meshFilter != null && meshFilter.sharedMesh == pointMesh)
            meshFilter.sharedMesh = null;

        if (pointMesh == null)
            return;

        if (Application.isPlaying)
            Destroy(pointMesh);
        else
            DestroyImmediate(pointMesh);
    }
}
