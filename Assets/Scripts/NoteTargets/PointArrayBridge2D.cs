using System.Collections;
using UnityEngine;

/// <summary>
/// Connects two configured point-array anchors when hit by the fifth note.
/// The bridge gets a solid collider after the connection animation completes.
/// </summary>
[DisallowMultipleComponent]
public sealed class PointArrayBridge2D : MonoBehaviour, INoteWaveReceiver2D
{
    private static readonly int DotCountId = Shader.PropertyToID("_DotCount");
    private static readonly int DotRadiusId = Shader.PropertyToID("_DotRadius");

    [Header("Point Arrays")]
    [SerializeField] private Transform endpointA;
    [SerializeField] private Transform endpointB;
    [SerializeField, Min(0f)] private float activationRadius = 0.18f;

    [Header("Bridge")]
    [SerializeField] private Material connectionMaterial;
    [SerializeField, Min(0.01f)] private float bridgeThickness = 0.14f;
    [SerializeField, Min(0.1f)] private float connectDuration = 0.7f;
    [SerializeField] private Color bridgeColor = new Color(0.55f, 0.9f, 1f, 1f);
    [SerializeField, Min(1)] private int dotCount = 32;
    [SerializeField, Range(0.1f, 0.5f)] private float dotRadius = 0.46f;

    private LineRenderer lineRenderer;
    private MaterialPropertyBlock propertyBlock;
    private bool isConnecting;
    private bool isConnected;

    public bool IsWaveAvailable => endpointA != null && endpointB != null && !isConnecting && !isConnected;

    private void Awake()
    {
        if (connectionMaterial == null)
            connectionMaterial = Resources.Load<Material>("ConnectionDots");

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.numCapVertices = 3;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.startWidth = bridgeThickness;
        lineRenderer.endWidth = bridgeThickness;
        lineRenderer.startColor = bridgeColor;
        lineRenderer.endColor = bridgeColor;
        lineRenderer.sortingOrder = 5;
        lineRenderer.enabled = false;

        if (connectionMaterial != null)
        {
            lineRenderer.sharedMaterial = connectionMaterial;
            propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetFloat(DotCountId, Mathf.Max(1, dotCount));
            propertyBlock.SetFloat(DotRadiusId, dotRadius);
            lineRenderer.SetPropertyBlock(propertyBlock);
        }
        else
            Debug.LogError("ConnectionDots material is missing from Resources.", this);

        if (endpointA == null || endpointB == null)
            Debug.LogError($"Assign both point-array anchors on {nameof(PointArrayBridge2D)} '{name}'.", this);
    }

    public float GetWaveContactDistance(Vector2 origin)
    {
        if (endpointA == null || endpointB == null)
            return Vector2.Distance(origin, transform.position);

        Vector2 start = endpointA.position;
        Vector2 end = endpointB.position;
        Vector2 segment = end - start;
        float segmentLengthSquared = segment.sqrMagnitude;
        float t = segmentLengthSquared > 0f
            ? Mathf.Clamp01(Vector2.Dot(origin - start, segment) / segmentLengthSquared)
            : 0f;
        Vector2 closestPoint = start + segment * t;
        return Mathf.Max(0f, Vector2.Distance(origin, closestPoint) - activationRadius);
    }

    public bool ReceiveWave(NoteWaveKind kind)
    {
        if (kind != NoteWaveKind.Connect || !IsWaveAvailable || lineRenderer == null || connectionMaterial == null)
            return false;

        isConnecting = true;
        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, endpointA.position);
        lineRenderer.SetPosition(1, endpointA.position);
        StartCoroutine(ConnectEndpoints());
        return true;
    }

    private IEnumerator ConnectEndpoints()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, connectDuration);

        while (elapsed < duration)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            UpdateBridge(progress);
            elapsed += Time.deltaTime;
            yield return null;
        }

        UpdateBridge(1f);
        CreateBridgeCollider();
        isConnecting = false;
        isConnected = true;
    }

    private void UpdateBridge(float progress)
    {
        if (endpointA == null || endpointB == null)
            return;

        Vector3 start = endpointA.position;
        Vector3 end = endpointB.position;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, Vector3.Lerp(start, end, progress));
    }

    private void CreateBridgeCollider()
    {
        Vector3 localStart = transform.InverseTransformPoint(endpointA.position);
        Vector3 localEnd = transform.InverseTransformPoint(endpointB.position);
        Vector2 localSegment = localEnd - localStart;
        float angle = Mathf.Atan2(localSegment.y, localSegment.x) * Mathf.Rad2Deg;

        GameObject colliderObject = new GameObject("Connected Bridge Collider");
        colliderObject.layer = gameObject.layer;
        colliderObject.transform.SetParent(transform, false);
        colliderObject.transform.localPosition = (localStart + localEnd) * 0.5f;
        colliderObject.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        BoxCollider2D bridgeCollider = colliderObject.AddComponent<BoxCollider2D>();
        bridgeCollider.size = new Vector2(localSegment.magnitude, bridgeThickness);
    }

    private void OnDrawGizmosSelected()
    {
        if (endpointA == null || endpointB == null)
            return;

        Gizmos.color = bridgeColor;
        Gizmos.DrawWireSphere(endpointA.position, 0.12f);
        Gizmos.DrawWireSphere(endpointB.position, 0.12f);
        Gizmos.DrawLine(endpointA.position, endpointB.position);
    }
}
