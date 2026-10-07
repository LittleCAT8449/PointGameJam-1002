using System.Collections;
using UnityEngine;

/// <summary>
/// A solid point-pattern wall that dissolves into scattered dots when it receives
/// the fourth note, then removes its solid colliders.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(NoteWaveBlocker2D))]
public sealed class DispersibleWall2D : MonoBehaviour, INoteWaveReceiver2D, INoteWaveRippleReceiver2D
{
    private static readonly int GridCountId = Shader.PropertyToID("_GridCount");
    private static readonly int DotRadiusId = Shader.PropertyToID("_DotRadius");
    private static readonly int ScatterProgressId = Shader.PropertyToID("_ScatterProgress");
    private static readonly int ScatterDistanceId = Shader.PropertyToID("_ScatterDistance");
    private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");

    [Header("Visual Points")]
    [SerializeField] private SpriteRenderer[] visuals;
    [SerializeField] private Collider2D[] solidColliders;
    [SerializeField] private Material scatterMaterial;
    [SerializeField] private Vector2Int gridCount = new Vector2Int(28, 36);
    [SerializeField, Range(0.1f, 0.5f)] private float dotRadius = 0.38f;
    [SerializeField, Range(0f, 0.5f)] private float scatterDistance = 0.18f;
    [SerializeField, Range(0.001f, 0.2f)] private float dissolveEdgeWidth = 0.04f;
    [SerializeField] private Color dissolveEdgeColor = new Color(1f, 0.55f, 1f, 0.8f);

    [Header("Scatter")]
    [SerializeField, Min(0.1f)] private float scatterDuration = 0.85f;

    private MaterialPropertyBlock propertyBlock;
    private bool isScattering;
    private bool isScattered;
    private Vector2 rippleOrigin;
    private float rippleStartTime;
    private float rippleSpeed;
    private float rippleMaxRadius;
    private bool hasWaveRipple;

    public bool IsWaveAvailable => !isScattering && !isScattered;

    private void Awake()
    {
        if (visuals == null || visuals.Length == 0)
            visuals = GetComponentsInChildren<SpriteRenderer>(true);

        if (solidColliders == null || solidColliders.Length == 0)
        {
            Collider2D[] allColliders = GetComponentsInChildren<Collider2D>(true);
            solidColliders = System.Array.FindAll(
                allColliders,
                targetCollider => targetCollider != null && !targetCollider.isTrigger);
        }

        if (scatterMaterial == null)
            scatterMaterial = Resources.Load<Material>("ScatteringSprite");

        if (scatterMaterial == null)
        {
            Debug.LogError("ScatteringSprite material is missing from Resources.", this);
            return;
        }

        propertyBlock = new MaterialPropertyBlock();
        foreach (SpriteRenderer visual in visuals)
        {
            if (visual == null)
                continue;

            visual.sharedMaterial = scatterMaterial;
            ApplyShaderProperties(visual, 0f);
        }
    }

    public float GetWaveContactDistance(Vector2 origin)
    {
        float nearestDistance = float.PositiveInfinity;

        if (solidColliders != null)
        {
            foreach (Collider2D targetCollider in solidColliders)
            {
                if (targetCollider == null)
                    continue;

                Bounds bounds = targetCollider.bounds;
                Vector3 closestPoint = bounds.ClosestPoint(new Vector3(origin.x, origin.y, bounds.center.z));
                nearestDistance = Mathf.Min(nearestDistance, Vector2.Distance(origin, closestPoint));
            }
        }

        if (!float.IsPositiveInfinity(nearestDistance))
            return nearestDistance;

        if (visuals != null)
        {
            foreach (SpriteRenderer visual in visuals)
            {
                if (visual == null)
                    continue;

                Bounds bounds = visual.bounds;
                Vector3 closestPoint = bounds.ClosestPoint(new Vector3(origin.x, origin.y, bounds.center.z));
                nearestDistance = Mathf.Min(nearestDistance, Vector2.Distance(origin, closestPoint));
            }
        }

        return float.IsPositiveInfinity(nearestDistance)
            ? Vector2.Distance(origin, transform.position)
            : nearestDistance;
    }

    public bool ReceiveWave(NoteWaveKind kind)
    {
        if (kind != NoteWaveKind.Scatter || !IsWaveAvailable)
            return false;

        isScattering = true;
        StartCoroutine(ScatterDots());
        return true;
    }

    public void OnNoteWaveReached(NoteWaveKind kind, Vector2 origin, float startTime, float expansionSpeed, float maxRadius)
    {
        rippleOrigin = origin;
        rippleStartTime = startTime;
        rippleSpeed = expansionSpeed;
        rippleMaxRadius = maxRadius;
        hasWaveRipple = true;

        if (visuals == null || propertyBlock == null)
            return;

        foreach (SpriteRenderer visual in visuals)
        {
            if (visual == null)
                continue;

            visual.GetPropertyBlock(propertyBlock);
            NoteWaveRippleShader2D.Apply(propertyBlock, rippleOrigin, rippleStartTime, rippleSpeed, rippleMaxRadius);
            visual.SetPropertyBlock(propertyBlock);
        }
    }

    private IEnumerator ScatterDots()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, scatterDuration);

        while (elapsed < duration)
        {
            ApplyProgress(elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyProgress(1f);

        if (solidColliders != null)
        {
            foreach (Collider2D targetCollider in solidColliders)
            {
                if (targetCollider != null)
                    targetCollider.enabled = false;
            }
        }

        if (visuals != null)
        {
            foreach (SpriteRenderer visual in visuals)
            {
                if (visual != null)
                    visual.enabled = false;
            }
        }

        isScattering = false;
        isScattered = true;
    }

    private void ApplyProgress(float progress)
    {
        if (visuals == null || propertyBlock == null)
            return;

        foreach (SpriteRenderer visual in visuals)
        {
            if (visual != null)
                ApplyShaderProperties(visual, progress);
        }
    }

    private void ApplyShaderProperties(SpriteRenderer visual, float progress)
    {
        visual.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVector(GridCountId, new Vector4(
            Mathf.Max(1, gridCount.x), Mathf.Max(1, gridCount.y), 0f, 0f));
        propertyBlock.SetFloat(DotRadiusId, dotRadius);
        propertyBlock.SetFloat(ScatterProgressId, Mathf.Clamp01(progress));
        propertyBlock.SetFloat(ScatterDistanceId, scatterDistance);
        propertyBlock.SetFloat(EdgeWidthId, dissolveEdgeWidth);
        propertyBlock.SetColor(EdgeColorId, dissolveEdgeColor);
        if (hasWaveRipple)
            NoteWaveRippleShader2D.Apply(propertyBlock, rippleOrigin, rippleStartTime, rippleSpeed, rippleMaxRadius);
        visual.SetPropertyBlock(propertyBlock);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.42f, 0.9f, 0.9f);
        foreach (Collider2D targetCollider in GetComponentsInChildren<Collider2D>())
        {
            if (targetCollider != null && !targetCollider.isTrigger)
                Gizmos.DrawWireCube(targetCollider.bounds.center, targetCollider.bounds.size);
        }
    }
}
