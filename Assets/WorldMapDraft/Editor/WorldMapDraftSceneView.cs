using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class WorldMapDraftSceneView
{
    private static readonly Dictionary<string, Color> LinkColors = new Dictionary<string, Color>
    {
        { "walkway", new Color(0.78f, 0.84f, 0.91f, 0.9f) },
        { "knowledge_gate", new Color(1f, 0.66f, 0.22f, 1f) },
        { "hidden", new Color(0.82f, 0.48f, 1f, 1f) },
        { "shortcut", new Color(0.24f, 0.86f, 0.92f, 1f) },
        { "hub_return", new Color(0.53f, 0.94f, 0.57f, 1f) },
        { "automatic_transition", new Color(1f, 0.82f, 0.34f, 1f) },
        { "other", new Color(0.92f, 0.92f, 0.92f, 0.9f) }
    };

    static WorldMapDraftSceneView()
    {
        SceneView.duringSceneGui += DrawScene;
    }

    private static void DrawScene(SceneView sceneView)
    {
        if (EditorApplication.isPlaying)
            return;
        WorldMapDraftGraph[] graphs = Resources.FindObjectsOfTypeAll<WorldMapDraftGraph>()
            .Where(graph => graph != null && graph.gameObject.scene.IsValid() && graph.gameObject.scene.isLoaded)
            .ToArray();
        if (graphs.Length == 0)
            return;

        foreach (WorldMapDraftGraph graph in graphs)
        {
            DrawConnections(graph);
            DrawRooms(graph);
        }
        DrawLegend(sceneView);
    }

    private static void DrawConnections(WorldMapDraftGraph graph)
    {
        foreach (WorldMapRoom room in graph.rooms)
        {
            if (room == null || room.gameObject.scene != graph.gameObject.scene)
                continue;
            foreach (WorldMapSceneConnection link in room.connections)
            {
                if (link == null || link.targetRoom == null || link.targetRoom.gameObject.scene != graph.gameObject.scene)
                    continue;
                Vector3 from = room.transform.position;
                Vector3 to = link.targetRoom.transform.position;
                Vector3 delta = to - from;
                if (delta.sqrMagnitude < 0.0001f)
                    continue;
                Vector3 direction = delta.normalized;
                Vector2 fromSize = room.RoomSize * 0.5f;
                Vector2 toSize = link.targetRoom.RoomSize * 0.5f;
                Vector3 start = from + direction * BorderDistance(direction, fromSize);
                Vector3 end = to - direction * BorderDistance(direction, toSize);
                Vector3[] route = MakeOrthogonalRoute(start, end);
                Color color = GetLinkColor(link);
                Handles.color = color;
                if (link.hidden || link.kind == WorldMapConnectionKind.KnowledgeGate || link.shortcut)
                    DrawDottedRoute(route, link.hidden ? 2.2f : 4f);
                else
                    Handles.DrawAAPolyLine(2f, route);

                if (link.oneWay)
                {
                    Vector3 midpoint = (route[1] + route[2]) * 0.5f;
                    Vector3 tangent = (route[2] - route[1]).sqrMagnitude > 0.0001f
                        ? (route[2] - route[1]).normalized
                        : (end - start).normalized;
                    Handles.ArrowHandleCap(0, midpoint, Quaternion.LookRotation(Vector3.forward, tangent), 0.32f, EventType.Repaint);
                }

                string edgeText = string.IsNullOrEmpty(link.id) ? link.label : link.id;
                if (link.requiresKnowledge != null && link.requiresKnowledge.Count > 0)
                    edgeText += " · " + string.Join(",", link.requiresKnowledge);
                GUIStyle edgeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                    fontSize = 9
                };
                Vector3 labelPoint = route[route.Length / 2];
                Handles.Label(labelPoint + Vector3.up * 0.12f, edgeText, edgeStyle);
            }
        }
    }

    private static void DrawRooms(WorldMapDraftGraph graph)
    {
        Dictionary<string, WorldMapRegionData> regionById = graph.regions
            .Where(region => region != null)
            .GroupBy(region => region.id)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (WorldMapRoom room in graph.rooms)
        {
            if (room == null || room.gameObject.scene != graph.gameObject.scene)
                continue;
            Color color = Color.gray;
            if (regionById.TryGetValue(room.regionId, out WorldMapRegionData region)
                && !string.IsNullOrEmpty(region.color_hex)
                && ColorUtility.TryParseHtmlString(region.color_hex, out Color parsed))
                color = parsed;
            Rect rect = RoomRect(room);
            Color fill = color;
            fill.a = Selection.activeGameObject == room.gameObject ? 0.48f : 0.27f;
            Handles.DrawSolidRectangleWithOutline(rect, fill, color);
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = 10
            };
            labelStyle.normal.textColor = Color.white;
            string text = room.roomId + "\n" + room.roomName;
            if (room.optional) text += "\n支线";
            if (room.hidden) text += " · 隐藏";
            Handles.Label(room.transform.position, text, labelStyle);
            if (Selection.activeGameObject == room.gameObject)
                Handles.DrawSolidRectangleWithOutline(rect, new Color(0f, 0f, 0f, 0f), Color.white);
        }
    }

    private static Rect RoomRect(WorldMapRoom room)
    {
        Vector3 position = room.transform.position;
        Vector3 scale = room.transform.lossyScale;
        return new Rect(position.x - Mathf.Abs(scale.x) * 0.5f, position.y - Mathf.Abs(scale.y) * 0.5f, Mathf.Abs(scale.x), Mathf.Abs(scale.y));
    }

    private static float BorderDistance(Vector3 direction, Vector2 halfSize)
    {
        float xDistance = Mathf.Abs(direction.x) < 0.0001f ? float.PositiveInfinity : halfSize.x / Mathf.Abs(direction.x);
        float yDistance = Mathf.Abs(direction.y) < 0.0001f ? float.PositiveInfinity : halfSize.y / Mathf.Abs(direction.y);
        return Mathf.Min(xDistance, yDistance);
    }

    private static Vector3[] MakeOrthogonalRoute(Vector3 start, Vector3 end)
    {
        float middleX = (start.x + end.x) * 0.5f;
        return new[]
        {
            start,
            new Vector3(middleX, start.y, start.z),
            new Vector3(middleX, end.y, end.z),
            end
        };
    }

    private static void DrawDottedRoute(Vector3[] points, float spacing)
    {
        for (int i = 0; i < points.Length - 1; i++)
            Handles.DrawDottedLine(points[i], points[i + 1], spacing);
    }

    private static Color GetLinkColor(WorldMapSceneConnection link)
    {
        if (link.hidden || link.kind == WorldMapConnectionKind.HiddenPassage) return LinkColors["hidden"];
        if (link.shortcut || link.kind == WorldMapConnectionKind.Shortcut) return LinkColors["shortcut"];
        if (link.requiresKnowledge != null && link.requiresKnowledge.Count > 0) return LinkColors["knowledge_gate"];
        switch (link.kind)
        {
            case WorldMapConnectionKind.HubReturn: return LinkColors["hub_return"];
            case WorldMapConnectionKind.AutomaticTransition: return LinkColors["automatic_transition"];
            case WorldMapConnectionKind.Walkway: return LinkColors["walkway"];
            default: return LinkColors["other"];
        }
    }

    private static void DrawLegend(SceneView sceneView)
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(12f, 12f, 236f, 164f), "World Map Draft · 图例", EditorStyles.helpBox);
        DrawLegendLine(LinkColors["walkway"], "普通通路", false);
        DrawLegendLine(LinkColors["knowledge_gate"], "知识锁（虚线）", true);
        DrawLegendLine(LinkColors["hidden"], "隐藏通路（点线）", true);
        DrawLegendLine(LinkColors["shortcut"], "捷径", true);
        GUILayout.Space(3f);
        GUILayout.Label("房间：W 拖动 · R 缩放", EditorStyles.miniLabel);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private static void DrawLegendLine(Color color, string label, bool dotted)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            Rect lineRect = GUILayoutUtility.GetRect(34f, 13f, GUILayout.Width(34f));
            Handles.BeginGUI();
            Handles.color = color;
            Vector3 left = new Vector3(lineRect.xMin, lineRect.center.y, 0f);
            Vector3 right = new Vector3(lineRect.xMax, lineRect.center.y, 0f);
            if (dotted) Handles.DrawDottedLine(left, right, 3f);
            else Handles.DrawAAPolyLine(2f, left, right);
            Handles.EndGUI();
            GUILayout.Label(label, EditorStyles.miniLabel);
        }
    }

    public static void FrameScene(Scene scene)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null || !scene.IsValid())
            return;
        WorldMapRoom[] rooms = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<WorldMapRoom>(true))
            .ToArray();
        if (rooms.Length == 0)
            return;
        Bounds bounds = new Bounds(rooms[0].transform.position, rooms[0].transform.lossyScale);
        foreach (WorldMapRoom room in rooms.Skip(1))
            bounds.Encapsulate(new Bounds(room.transform.position, room.transform.lossyScale));
        view.Frame(bounds, false);
    }
}
