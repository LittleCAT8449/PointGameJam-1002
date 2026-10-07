using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Expands as a circular front. Each angular sector advances until its first
/// configured blocker; visuals and target contacts use the same stop distances.
/// </summary>
[DisallowMultipleComponent]
public sealed class ExpandingNoteWave2D : MonoBehaviour
{
    private const int SectorCount = 360;
    private const float RingThickness = 0.045f;
    private const float BlockedFrontFadeTime = 0.18f;
    private const float ReachTolerance = 0.025f;
    private const float FullCircle = Mathf.PI * 2f;
    private static Material runtimeWaveMaterial;

    private struct WaveTarget
    {
        public INoteWaveReceiver2D receiver;
        public INoteWaveRippleReceiver2D rippleReceiver;
        public MonoBehaviour component;
        public Collider2D[] colliders;
    }

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh waveMesh;
    private Vector3[] meshVertices;
    private Color[] meshColors;
    private int[] meshTriangles;
    private WaveTarget[] targets;
    private readonly HashSet<MonoBehaviour> contactedTargets = new HashSet<MonoBehaviour>();
    private readonly List<Collider2D> blockerColliderList = new List<Collider2D>();
    private HashSet<Collider2D> blockerColliders;
    private RaycastHit2D[] raycastResults;
    private float[] blockerDistances;
    private Vector2 origin;
    private NoteWaveKind waveKind;
    private Color waveColor;
    private float maxRadius;
    private float expansionSpeed;
    private float radius;
    private float waveStartTime;
    private int respondedTargetCount;
    private bool debugDisplay;
    private bool initialized;
    private GUIStyle debugLabelStyle;

    private float SectorAngle => FullCircle / SectorCount;

    public void Initialize(NoteWaveKind kind, float waveMaxRadius, float waveExpansionSpeed, Color color, bool showDebug)
    {
        origin = transform.position;
        waveKind = kind;
        maxRadius = Mathf.Max(0.1f, waveMaxRadius);
        expansionSpeed = Mathf.Max(0.1f, waveExpansionSpeed);
        waveColor = color;
        debugDisplay = showDebug;
        waveStartTime = Time.time;

        FindTargets();
        BuildBlockerDistances();
        CreateWaveMesh();
        initialized = true;
        UpdateWaveMesh();
    }

    private void Update()
    {
        if (!initialized)
            return;

        radius = Mathf.Min(maxRadius, radius + expansionSpeed * Time.deltaTime);
        AffectReachedTargets();
        UpdateWaveMesh();

        if (radius >= maxRadius)
            Destroy(gameObject);
    }

    private void BuildBlockerDistances()
    {
        blockerDistances = new float[SectorCount + 1];
        for (int i = 0; i < blockerDistances.Length; i++)
            blockerDistances[i] = maxRadius;

        blockerColliderList.Clear();
        NoteWaveBlocker2D[] blockerMarkers = FindObjectsByType<NoteWaveBlocker2D>(FindObjectsSortMode.None);
        foreach (NoteWaveBlocker2D blockerMarker in blockerMarkers)
        {
            if (blockerMarker != null)
                blockerMarker.AddCollidersTo(blockerColliderList);
        }

        if (blockerColliderList.Count == 0)
            return;

        blockerColliders = new HashSet<Collider2D>(blockerColliderList);
        Collider2D[] sceneColliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        raycastResults = new RaycastHit2D[Mathf.Max(16, sceneColliders.Length + 4)];

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(~0);
        filter.useTriggers = false;
        Physics2D.SyncTransforms();

        float sectorAngle = SectorAngle;
        float[] boundaryDistances = new float[SectorCount];
        float[] centerDistances = new float[SectorCount];

        for (int i = 0; i < SectorCount; i++)
            boundaryDistances[i] = RaycastToFirstBlocker(i * sectorAngle, filter);

        for (int i = 0; i < SectorCount; i++)
        {
            // Center samples catch narrow blockers that fall between boundary rays.
            float angle = (i + 0.5f) * sectorAngle;
            centerDistances[i] = RaycastToFirstBlocker(angle, filter);
        }

        // Clamp each shared mesh boundary by both adjacent wedge centers. This
        // keeps the mesh from bridging across a thin obstacle's corner.
        for (int i = 0; i < SectorCount; i++)
        {
            int previousCenter = (i + SectorCount - 1) % SectorCount;
            blockerDistances[i] = Mathf.Min(
                boundaryDistances[i],
                centerDistances[previousCenter],
                centerDistances[i]);
        }

        blockerDistances[SectorCount] = blockerDistances[0];
    }

    private float RaycastToFirstBlocker(float angle, ContactFilter2D filter)
    {
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        int hitCount = Physics2D.Raycast(origin, direction, filter, raycastResults, maxRadius);
        float firstBlockerDistance = maxRadius;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit2D hit = raycastResults[hitIndex];
            if (hit.collider != null
                && blockerColliders.Contains(hit.collider)
                && hit.distance < firstBlockerDistance)
            {
                firstBlockerDistance = hit.distance;
            }
        }

        return firstBlockerDistance;
    }

    private void CreateWaveMesh()
    {
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sortingOrder = 100;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        Material waveMaterial = Resources.Load<Material>("NoteWaveFront");
        if (waveMaterial == null)
        {
            Shader waveShader = Shader.Find("PointGameJam/Note Wave Front");
            if (waveShader != null)
            {
                if (runtimeWaveMaterial == null)
                {
                    runtimeWaveMaterial = new Material(waveShader)
                    {
                        name = "Note Wave Front (Runtime)",
                        hideFlags = HideFlags.DontSave
                    };
                }

                waveMaterial = runtimeWaveMaterial;
            }
        }

        if (waveMaterial != null)
            meshRenderer.sharedMaterial = waveMaterial;
        else
        {
            meshRenderer.enabled = false;
            Debug.LogError("NoteWaveFront material or shader is missing. Wave contacts will still be evaluated.", this);
        }

        int vertexCount = SectorCount * 4;
        meshVertices = new Vector3[vertexCount];
        meshColors = new Color[vertexCount];
        meshTriangles = new int[SectorCount * 6];

        for (int i = 0; i < SectorCount; i++)
        {
            int vertex = i * 4;
            int triangle = i * 6;
            meshTriangles[triangle] = vertex;
            meshTriangles[triangle + 1] = vertex + 1;
            meshTriangles[triangle + 2] = vertex + 2;
            meshTriangles[triangle + 3] = vertex;
            meshTriangles[triangle + 4] = vertex + 2;
            meshTriangles[triangle + 5] = vertex + 3;
        }

        waveMesh = new Mesh
        {
            name = "Occluded Note Wave Front",
            hideFlags = HideFlags.DontSave
        };
        waveMesh.MarkDynamic();
        waveMesh.vertices = meshVertices;
        waveMesh.colors = meshColors;
        waveMesh.triangles = meshTriangles;
        waveMesh.bounds = new Bounds(Vector3.zero, new Vector3(maxRadius * 2f + 1f, maxRadius * 2f + 1f, 1f));
        meshFilter.sharedMesh = waveMesh;
    }

    private void UpdateWaveMesh()
    {
        if (waveMesh == null || blockerDistances == null)
            return;

        float sectorAngle = SectorAngle;
        for (int i = 0; i < SectorCount; i++)
        {
            float startAngle = i * sectorAngle;
            float endAngle = (i + 1) * sectorAngle;
            float stopDistanceA = blockerDistances[i];
            float stopDistanceB = blockerDistances[i + 1];
            float frontRadiusA = Mathf.Min(radius, stopDistanceA);
            float frontRadiusB = Mathf.Min(radius, stopDistanceB);
            float innerRadiusA = Mathf.Max(0f, frontRadiusA - RingThickness);
            float innerRadiusB = Mathf.Max(0f, frontRadiusB - RingThickness);
            float alphaA = GetBlockedFrontAlpha(stopDistanceA);
            float alphaB = GetBlockedFrontAlpha(stopDistanceB);
            Vector2 startDirection = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle));
            Vector2 endDirection = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));

            int vertex = i * 4;
            meshVertices[vertex] = startDirection * innerRadiusA;
            meshVertices[vertex + 1] = startDirection * frontRadiusA;
            meshVertices[vertex + 2] = endDirection * frontRadiusB;
            meshVertices[vertex + 3] = endDirection * innerRadiusB;

            Color colorA = waveColor;
            colorA.a *= alphaA;
            Color colorB = waveColor;
            colorB.a *= alphaB;
            meshColors[vertex] = colorA;
            meshColors[vertex + 1] = colorA;
            meshColors[vertex + 2] = colorB;
            meshColors[vertex + 3] = colorB;
        }

        waveMesh.vertices = meshVertices;
        waveMesh.colors = meshColors;
    }

    private float GetBlockedFrontAlpha(float stopDistance)
    {
        if (stopDistance >= maxRadius - ReachTolerance)
            return 1f;

        float distancePastBlocker = radius - stopDistance;
        if (distancePastBlocker <= 0f)
            return 1f;

        float fadeDistance = Mathf.Max(0.001f, expansionSpeed * BlockedFrontFadeTime);
        return 1f - Mathf.Clamp01(distancePastBlocker / fadeDistance);
    }

    private void AffectReachedTargets()
    {
        if (targets == null)
            return;

        foreach (WaveTarget target in targets)
        {
            if ((target.receiver == null && target.rippleReceiver == null) || target.component == null
                || !target.component.isActiveAndEnabled
                || contactedTargets.Contains(target.component))
            {
                continue;
            }

            float contactDistance = GetTargetContact(target, out Vector2 contactPoint);
            float reachableFrontDistance = GetFrontDistanceForPoint(contactPoint);
            if (contactDistance > reachableFrontDistance + ReachTolerance)
                continue;

            contactedTargets.Add(target.component);

            // A point-field ripple starts only when the front reaches the target
            // through a clear sector. Unreachable fields never receive a pulse.
            target.rippleReceiver?.OnNoteWaveReached(waveKind, origin, waveStartTime, expansionSpeed, maxRadius);

            bool responded = target.receiver != null
                && target.receiver.IsWaveAvailable
                && target.receiver.ReceiveWave(waveKind);
            if (responded)
                respondedTargetCount++;

            if (debugDisplay)
            {
                string response = responded
                    ? "responded"
                    : target.receiver == null ? "ripple only" : "reached, no state change";
                Debug.Log($"[{GetNoteLabel()}] Wave reached '{target.component.name}' at radius {radius:F2}; target {response}.", target.component);
            }
        }
    }

    private float GetTargetContact(WaveTarget target, out Vector2 contactPoint)
    {
        float nearestDistance = float.PositiveInfinity;
        contactPoint = target.component.transform.position;

        if (target.colliders != null)
        {
            foreach (Collider2D targetCollider in target.colliders)
            {
                if (targetCollider == null || !targetCollider.enabled || !targetCollider.gameObject.activeInHierarchy)
                    continue;

                Vector2 closestPoint = targetCollider.ClosestPoint(origin);
                float distance = Vector2.Distance(origin, closestPoint);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    contactPoint = closestPoint;
                }
            }
        }

        if (!float.IsPositiveInfinity(nearestDistance))
            return nearestDistance;

        if (target.component is INoteWaveContactPoint2D contactPointProvider)
        {
            contactPoint = contactPointProvider.GetWaveContactPoint(origin);
            return Vector2.Distance(origin, contactPoint);
        }

        if (target.receiver != null)
        {
            nearestDistance = target.receiver.GetWaveContactDistance(origin);
            Vector2 receiverDirection = (Vector2)target.component.transform.position - origin;
            contactPoint = receiverDirection.sqrMagnitude > 0.0001f
                ? origin + receiverDirection.normalized * nearestDistance
                : origin;
            return nearestDistance;
        }

        Renderer targetRenderer = target.component.GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
        {
            Bounds bounds = targetRenderer.bounds;
            Vector3 closestPoint = bounds.ClosestPoint(new Vector3(origin.x, origin.y, bounds.center.z));
            contactPoint = new Vector2(closestPoint.x, closestPoint.y);
            return Vector2.Distance(origin, contactPoint);
        }

        Vector2 targetDirection = (Vector2)target.component.transform.position - origin;
        contactPoint = (Vector2)target.component.transform.position;
        return targetDirection.magnitude;
    }

    private float GetBlockerDistanceForPoint(Vector2 point)
    {
        if (blockerDistances == null || blockerDistances.Length < 2)
            return maxRadius;

        Vector2 direction = point - origin;
        if (direction.sqrMagnitude <= 0.0001f)
            return blockerDistances[0];

        float angle = Mathf.Repeat(Mathf.Atan2(direction.y, direction.x), FullCircle);
        float sectorPosition = angle / SectorAngle;
        int sectorIndex = Mathf.Clamp(Mathf.FloorToInt(sectorPosition), 0, SectorCount - 1);
        float fraction = sectorPosition - sectorIndex;
        return Mathf.Lerp(blockerDistances[sectorIndex], blockerDistances[sectorIndex + 1], fraction);
    }

    private float GetFrontDistanceForPoint(Vector2 point)
    {
        if (blockerDistances == null || blockerDistances.Length < 2)
            return radius;

        Vector2 direction = point - origin;
        if (direction.sqrMagnitude <= 0.0001f)
            return Mathf.Min(radius, blockerDistances[0]);

        float angle = Mathf.Repeat(Mathf.Atan2(direction.y, direction.x), FullCircle);
        float sectorPosition = angle / SectorAngle;
        int sectorIndex = Mathf.Clamp(Mathf.FloorToInt(sectorPosition), 0, SectorCount - 1);
        float fraction = sectorPosition - sectorIndex;
        float frontA = Mathf.Min(radius, blockerDistances[sectorIndex]);
        float frontB = Mathf.Min(radius, blockerDistances[sectorIndex + 1]);
        return Mathf.Lerp(frontA, frontB, fraction);
    }

    private void FindTargets()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        List<WaveTarget> foundTargets = new List<WaveTarget>();

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (!behaviour.isActiveAndEnabled)
                continue;

            INoteWaveReceiver2D receiver = behaviour as INoteWaveReceiver2D;
            INoteWaveRippleReceiver2D rippleReceiver = behaviour as INoteWaveRippleReceiver2D;
            if (receiver == null && rippleReceiver == null)
                continue;

            foundTargets.Add(new WaveTarget
            {
                receiver = receiver,
                rippleReceiver = rippleReceiver,
                component = behaviour,
                colliders = behaviour.GetComponentsInChildren<Collider2D>(true)
            });
        }

        targets = foundTargets.ToArray();
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

    private void OnDrawGizmos()
    {
        if (!debugDisplay || !initialized)
            return;

        Vector3 center = new Vector3(origin.x, origin.y, transform.position.z);
        float sectorAngle = SectorAngle;

        for (int i = 0; i < SectorCount; i++)
        {
            float angleA = i * sectorAngle;
            float angleB = (i + 1) * sectorAngle;
            float stopDistanceA = blockerDistances[i];
            float stopDistanceB = blockerDistances[i + 1];
            float frontDistanceA = Mathf.Min(radius, stopDistanceA);
            float frontDistanceB = Mathf.Min(radius, stopDistanceB);
            Vector3 dirA = new Vector3(Mathf.Cos(angleA), Mathf.Sin(angleA), 0f);
            Vector3 dirB = new Vector3(Mathf.Cos(angleB), Mathf.Sin(angleB), 0f);

            bool isBlocked = stopDistanceA < maxRadius - ReachTolerance
                || stopDistanceB < maxRadius - ReachTolerance;
            float alpha = Mathf.Min(GetBlockedFrontAlpha(stopDistanceA), GetBlockedFrontAlpha(stopDistanceB));
            Gizmos.color = isBlocked ? new Color(1f, 0.22f, 0.18f, Mathf.Max(0.15f, alpha)) : waveColor;
            Gizmos.DrawLine(center + dirA * frontDistanceA, center + dirB * frontDistanceB);

            if (stopDistanceA < maxRadius - ReachTolerance && i % 12 == 0)
            {
                Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.35f);
                Gizmos.DrawLine(center, center + dirA * stopDistanceA);
            }
        }

        Gizmos.color = waveColor;
        Gizmos.DrawSphere(center, 0.08f);

        if (targets == null)
            return;

        foreach (WaveTarget target in targets)
        {
            if (target.component == null)
                continue;

            float contactDistance = GetTargetContact(target, out Vector2 contactPoint);
            float blockerDistance = GetBlockerDistanceForPoint(contactPoint);
            bool reachable = contactDistance <= blockerDistance + ReachTolerance;
            Gizmos.color = contactedTargets.Contains(target.component)
                ? Color.green
                : reachable ? Color.magenta : Color.gray;
            Gizmos.DrawWireSphere(target.component.transform.position, 0.12f);
            Gizmos.DrawLine(center, contactPoint);
        }
    }

    private void OnGUI()
    {
        if (!debugDisplay || !initialized)
            return;

        Color previousColor = GUI.color;
        GUI.color = Color.white;
        Rect panelRect = new Rect(8f, 8f, 700f, 36f);
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
            foreach (WaveTarget target in targets)
            {
                if (target.receiver != null && target.receiver.IsWaveAvailable)
                    availableCount++;
            }
        }

        int blockedSectorCount = 0;
        if (blockerDistances != null)
        {
            for (int i = 0; i < SectorCount; i++)
            {
                if (blockerDistances[i] < maxRadius - ReachTolerance
                    || blockerDistances[i + 1] < maxRadius - ReachTolerance)
                {
                    blockedSectorCount++;
                }
            }
        }

        GUI.Label(
            new Rect(16f, 10f, 684f, 32f),
            $"{GetNoteLabel()} Debug | 半径 {radius:F1}/{maxRadius:F1} | 阻挡方向 {blockedSectorCount}/{SectorCount} | 命中 {contactedTargets.Count} | 响应 {respondedTargetCount} | 接收器 {targets?.Length ?? 0} | 可响应 {availableCount}",
            debugLabelStyle);
        GUI.color = previousColor;
    }

    private void OnDestroy()
    {
        if (meshFilter != null && meshFilter.sharedMesh == waveMesh)
            meshFilter.sharedMesh = null;

        if (waveMesh == null)
            return;

        if (Application.isPlaying)
            Destroy(waveMesh);
        else
            DestroyImmediate(waveMesh);
    }
}
