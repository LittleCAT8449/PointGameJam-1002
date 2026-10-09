using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldMapDraftGenerator
{
    public const string RootFolder = "Assets/WorldMapDraft";
    public const string SourceJsonPath = RootFolder + "/Data/world_map_moremap_source.json";
    public const string ScenesFolder = RootFolder + "/Scenes";
    public const string DataFolder = RootFolder + "/Data";
    public const float DefaultUnitsPer100Pixels = 1f;

    private static readonly Dictionary<string, string> RegionPalette = new Dictionary<string, string>
    {
        { "A1", "#4F91C7" },
        { "A2", "#48A887" },
        { "A3", "#92B44F" },
        { "A4", "#B36BC2" },
        { "A5", "#D99843" }
    };

    [MenuItem("Tools/World Map Draft/Generate Moremap Scene (39 Rooms)")]
    public static void GenerateFromSource()
    {
        try
        {
            WorldMapDraftDocument document = ReadDocument(SourceJsonPath);
            GenerateScene(document);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("World Map Draft", "生成失败：\n" + exception.Message, "OK");
        }
    }

    public static WorldMapDraftDocument ReadDocument(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("地图 JSON 不存在。", path);
        WorldMapDraftDocument document = JsonUtility.FromJson<WorldMapDraftDocument>(File.ReadAllText(path));
        if (document == null)
            throw new InvalidDataException("无法读取地图 JSON。");
        Normalize(document);
        return document;
    }

    public static void GenerateScene(WorldMapDraftDocument document)
    {
        Normalize(document);
        WorldMapDraftValidationResult validation = WorldMapDraftValidator.Validate(document);
        if (!validation.IsValid)
            throw new InvalidDataException("地图拓扑检查未通过：\n- " + string.Join("\n- ", validation.errors));

        Directory.CreateDirectory(Path.Combine(Application.dataPath, "WorldMapDraft/Scenes"));
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "WorldMapDraft/Data/Generated"));
        string scenePath = AssetDatabase.GenerateUniqueAssetPath(ScenesFolder + "/WorldMapDraft_Moremap_39R.unity");
        string assetStem = Path.GetFileNameWithoutExtension(scenePath);
        string jsonPath = AssetDatabase.GenerateUniqueAssetPath(DataFolder + "/Generated/" + assetStem + ".json");
        string reportPath = AssetDatabase.GenerateUniqueAssetPath(DataFolder + "/Generated/" + assetStem + "_validation.md");

        Scene scene = CreateIndependentScene();
        try
        {
            scene.name = assetStem;
            BuildSceneObjects(scene, document);
            string jsonAbsolutePath = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, jsonPath));
            File.WriteAllText(jsonAbsolutePath, JsonUtility.ToJson(document, true));
            string reportAbsolutePath = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, reportPath));
            File.WriteAllText(reportAbsolutePath, WorldMapDraftValidator.ToMarkdown(document, validation));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new IOException("无法保存新地图场景：" + scenePath);
            SceneManager.SetActiveScene(scene);
            Selection.activeGameObject = GameObject.Find("World Map - Moremap 39 Rooms");
            WorldMapDraftSceneView.FrameScene(scene);
            Debug.Log($"[WorldMapDraft] Scene: {scenePath}\n[WorldMapDraft] JSON: {jsonPath}\n[WorldMapDraft] Validation: {reportPath}");
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("World Map Draft", $"已生成 {document.rooms.Count} 个房间、{document.regions.Count} 个区域的独立场景。\n\n场景：{scenePath}\nJSON：{jsonPath}\n检查报告：{reportPath}", "OK");
        }
        catch
        {
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(scene, true);
            throw;
        }
    }

    private static Scene CreateIndependentScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        bool isUntitled = string.IsNullOrEmpty(activeScene.path);
        bool hasSceneContent = isUntitled && activeScene.GetRootGameObjects().Length > 0;

        if (isUntitled && !hasSceneContent && SceneManager.sceneCount == 1)
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        if (hasSceneContent)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("当前场景尚未保存，已取消生成以保留场景内容。");
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                throw new OperationCanceledException("请先保存当前场景，再生成独立地图场景。");
        }

        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
    }

    public static WorldMapDraftDocument BuildDocument(WorldMapDraftGraph graph)
    {
        if (graph == null)
            throw new ArgumentNullException(nameof(graph));
        WorldMapDraftDocument document = new WorldMapDraftDocument
        {
            meta = graph.meta ?? new WorldMapMeta(),
            regions = graph.regions ?? new List<WorldMapRegionData>(),
            knowledge = graph.knowledge ?? new List<WorldMapKnowledgeData>(),
            gates = graph.gates ?? new List<WorldMapGateData>(),
            exploration_loops = graph.explorationLoops ?? new List<WorldMapLoopData>(),
            early_sightlines = graph.earlySightlines ?? new List<WorldMapEarlySightlineData>(),
            layout = graph.layout ?? new WorldMapLayoutData(),
            critical_path = graph.criticalPath ?? new List<string>(),
            progression_assumptions = graph.progressionAssumptions ?? new List<string>()
        };

        Dictionary<string, WorldMapRoom> roomById = graph.rooms
            .Where(room => room != null && !string.IsNullOrWhiteSpace(room.roomId))
            .GroupBy(room => room.roomId)
            .ToDictionary(group => group.Key, group => group.First());

        float unitScale = Mathf.Max(0.0001f, graph.unityUnitsPer100Pixels);
        foreach (WorldMapRoom room in roomById.Values.OrderBy(room => room.roomId, StringComparer.Ordinal))
        {
            Vector3 p = room.transform.localPosition;
            Vector2 size = room.RoomSize;
            float centerX = graph.sourcePixelCenterX + p.x / unitScale * 100f;
            float centerY = graph.sourcePixelCenterY - p.y / unitScale * 100f;
            WorldMapRoomData roomData = new WorldMapRoomData
            {
                id = room.roomId,
                name = room.roomName,
                area_id = room.regionId,
                region_id = room.regionId,
                region_name = room.regionName,
                function = room.function,
                pacing_beat = room.pacingBeat,
                grants_knowledge = new List<string>(room.grantsKnowledge ?? new List<string>()),
                optional = room.optional,
                hidden = room.hidden,
                knowledge_visible_before_mastery = new List<string>(room.knowledgeVisibleBeforeMastery ?? new List<string>()),
                recovery_and_return = room.recoveryAndReturn,
                position = new WorldMapRoomPositionData
                {
                    drawio_px = new WorldMapPixelRect
                    {
                        x = centerX - size.x / unitScale * 50f,
                        y = centerY - size.y / unitScale * 50f,
                        width = size.x / unitScale * 100f,
                        height = size.y / unitScale * 100f
                    }
                }
            };
            if (document.layout != null && document.layout.grid != null && document.layout.grid.step_x > 0f && document.layout.grid.step_y > 0f)
            {
                roomData.position.grid_x = Mathf.RoundToInt((roomData.position.drawio_px.x - document.layout.grid.origin_x) / document.layout.grid.step_x);
                roomData.position.grid_y = Mathf.RoundToInt((roomData.position.drawio_px.y - document.layout.grid.origin_y) / document.layout.grid.step_y);
            }
            document.rooms.Add(roomData);

            foreach (WorldMapSceneConnection connection in room.connections ?? new List<WorldMapSceneConnection>())
            {
                if (connection == null || connection.targetRoom == null)
                    continue;
                document.connections.Add(new WorldMapConnectionData
                {
                    id = connection.id,
                    from = room.roomId,
                    to = connection.targetRoom.roomId,
                    label = connection.label,
                    connection_type = string.IsNullOrWhiteSpace(connection.sourceType) ? ConnectionKindToSource(connection.kind) : connection.sourceType,
                    directionality = connection.directionality,
                    one_way = connection.oneWay,
                    requires_knowledge = new List<string>(connection.requiresKnowledge ?? new List<string>()),
                    shortcut = connection.shortcut,
                    hidden = connection.hidden,
                    optional = connection.optional,
                    opened_from = connection.openedFrom,
                    previewed_from = new List<string>(connection.previewedFrom ?? new List<string>()),
                    notes = connection.notes
                });
            }
        }

        document.connections = document.connections.OrderBy(edge => edge.id, StringComparer.Ordinal).ToList();
        Normalize(document);
        WorldMapDraftValidator.Validate(document);
        return document;
    }

    public static void Normalize(WorldMapDraftDocument document)
    {
        if (document.meta == null) document.meta = new WorldMapMeta();
        if (document.regions == null) document.regions = new List<WorldMapRegionData>();
        if (document.knowledge == null) document.knowledge = new List<WorldMapKnowledgeData>();
        if (document.gates == null) document.gates = new List<WorldMapGateData>();
        if (document.rooms == null) document.rooms = new List<WorldMapRoomData>();
        if (document.connections == null) document.connections = new List<WorldMapConnectionData>();
        if (document.exploration_loops == null) document.exploration_loops = new List<WorldMapLoopData>();
        if (document.early_sightlines == null) document.early_sightlines = new List<WorldMapEarlySightlineData>();
        if (document.layout == null) document.layout = new WorldMapLayoutData();
        if (document.layout.grid == null) document.layout.grid = new WorldMapGridData();
        if (document.layout.region_arrangement == null) document.layout.region_arrangement = new WorldMapRegionArrangementData();
        if (document.critical_path == null) document.critical_path = new List<string>();
        if (document.progression_assumptions == null) document.progression_assumptions = new List<string>();
        foreach (WorldMapRoomData room in document.rooms)
        {
            if (room.position == null) room.position = new WorldMapRoomPositionData();
            if (room.position.drawio_px == null) room.position.drawio_px = new WorldMapPixelRect();
            if (room.grants_knowledge == null) room.grants_knowledge = new List<string>();
            if (room.knowledge_visible_before_mastery == null) room.knowledge_visible_before_mastery = new List<string>();
        }
        foreach (WorldMapKnowledgeData item in document.knowledge)
        {
            if (item.requires_before == null) item.requires_before = new List<string>();
        }
        foreach (WorldMapConnectionData edge in document.connections)
        {
            if (edge.requires_knowledge == null) edge.requires_knowledge = new List<string>();
            if (edge.previewed_from == null) edge.previewed_from = new List<string>();
        }
        foreach (WorldMapLoopData loop in document.exploration_loops)
        {
            if (loop.cycle == null) loop.cycle = new List<string>();
            if (loop.shortcut_path == null) loop.shortcut_path = new List<string>();
        }

        foreach (WorldMapRegionData region in document.regions)
            if (region != null && string.IsNullOrWhiteSpace(region.color_hex) && RegionPalette.TryGetValue(region.id, out string color))
                region.color_hex = color;
        document.meta.output_folder = RootFolder;
        document.layout.unity_units_per_100_pixels = DefaultUnitsPer100Pixels;
    }

    private static void BuildSceneObjects(Scene scene, WorldMapDraftDocument document)
    {
        GameObject rootObject = new GameObject("World Map - Moremap 39 Rooms");
        SceneManager.MoveGameObjectToScene(rootObject, scene);
        WorldMapDraftGraph graph = rootObject.AddComponent<WorldMapDraftGraph>();
        graph.meta = document.meta;
        graph.regions = document.regions;
        graph.knowledge = document.knowledge;
        graph.gates = document.gates;
        graph.explorationLoops = document.exploration_loops;
        graph.earlySightlines = document.early_sightlines;
        graph.layout = document.layout;
        graph.criticalPath = document.critical_path;
        graph.progressionAssumptions = document.progression_assumptions;
        graph.unityUnitsPer100Pixels = DefaultUnitsPer100Pixels;

        float minX = document.rooms.Min(room => room.position.drawio_px.x);
        float minY = document.rooms.Min(room => room.position.drawio_px.y);
        float maxX = document.rooms.Max(room => room.position.drawio_px.x + room.position.drawio_px.width);
        float maxY = document.rooms.Max(room => room.position.drawio_px.y + room.position.drawio_px.height);
        graph.sourcePixelCenterX = (minX + maxX) * 0.5f;
        graph.sourcePixelCenterY = (minY + maxY) * 0.5f;

        GameObject roomsObject = new GameObject("Rooms");
        roomsObject.transform.SetParent(rootObject.transform, false);
        Dictionary<string, WorldMapRoom> roomObjects = new Dictionary<string, WorldMapRoom>();

        foreach (WorldMapRoomData data in document.rooms)
        {
            GameObject roomObject = new GameObject(data.id + " - " + data.name);
            roomObject.transform.SetParent(roomsObject.transform, false);
            float centerX = data.position.drawio_px.x + data.position.drawio_px.width * 0.5f;
            float centerY = data.position.drawio_px.y + data.position.drawio_px.height * 0.5f;
            roomObject.transform.localPosition = new Vector3(
                (centerX - graph.sourcePixelCenterX) / 100f * graph.unityUnitsPer100Pixels,
                (graph.sourcePixelCenterY - centerY) / 100f * graph.unityUnitsPer100Pixels,
                0f);
            roomObject.transform.localScale = new Vector3(
                data.position.drawio_px.width / 100f * graph.unityUnitsPer100Pixels,
                data.position.drawio_px.height / 100f * graph.unityUnitsPer100Pixels,
                1f);
            WorldMapRoom room = roomObject.AddComponent<WorldMapRoom>();
            room.roomId = data.id;
            room.roomName = data.name;
            room.regionId = string.IsNullOrEmpty(data.region_id) ? data.area_id : data.region_id;
            room.regionName = string.IsNullOrEmpty(data.region_name) ? FindRegionName(document.regions, room.regionId) : data.region_name;
            room.function = data.function;
            room.pacingBeat = data.pacing_beat;
            room.grantsKnowledge = new List<string>(data.grants_knowledge);
            room.optional = data.optional;
            room.hidden = data.hidden;
            room.knowledgeVisibleBeforeMastery = new List<string>(data.knowledge_visible_before_mastery);
            room.recoveryAndReturn = data.recovery_and_return;
            roomObjects.Add(data.id, room);
            graph.rooms.Add(room);
        }

        foreach (WorldMapConnectionData edge in document.connections)
        {
            WorldMapSceneConnection connection = new WorldMapSceneConnection
            {
                id = edge.id,
                targetRoom = roomObjects[edge.to],
                label = edge.label,
                kind = ParseConnectionKind(edge),
                sourceType = edge.connection_type,
                directionality = edge.directionality,
                oneWay = edge.one_way,
                requiresKnowledge = new List<string>(edge.requires_knowledge),
                shortcut = edge.shortcut,
                hidden = edge.hidden,
                optional = edge.optional,
                openedFrom = edge.opened_from,
                previewedFrom = new List<string>(edge.previewed_from),
                notes = edge.notes
            };
            roomObjects[edge.from].connections.Add(connection);
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static string FindRegionName(List<WorldMapRegionData> regions, string id)
    {
        WorldMapRegionData region = regions.FirstOrDefault(item => item != null && item.id == id);
        return region != null ? region.name : id;
    }

    public static WorldMapConnectionKind ParseConnectionKind(WorldMapConnectionData edge)
    {
        if (edge.hidden || string.Equals(edge.connection_type, "hidden", StringComparison.OrdinalIgnoreCase)) return WorldMapConnectionKind.HiddenPassage;
        if (edge.shortcut || string.Equals(edge.connection_type, "shortcut", StringComparison.OrdinalIgnoreCase)) return WorldMapConnectionKind.Shortcut;
        if (string.Equals(edge.connection_type, "knowledge_gate", StringComparison.OrdinalIgnoreCase)) return WorldMapConnectionKind.KnowledgeGate;
        if (string.Equals(edge.connection_type, "hub_return", StringComparison.OrdinalIgnoreCase)) return WorldMapConnectionKind.HubReturn;
        if (string.Equals(edge.connection_type, "automatic_transition", StringComparison.OrdinalIgnoreCase)) return WorldMapConnectionKind.AutomaticTransition;
        if (string.Equals(edge.connection_type, "walkway", StringComparison.OrdinalIgnoreCase)) return WorldMapConnectionKind.Walkway;
        return WorldMapConnectionKind.Other;
    }

    public static string ConnectionKindToSource(WorldMapConnectionKind kind)
    {
        switch (kind)
        {
            case WorldMapConnectionKind.KnowledgeGate: return "knowledge_gate";
            case WorldMapConnectionKind.HiddenPassage: return "hidden";
            case WorldMapConnectionKind.Shortcut: return "shortcut";
            case WorldMapConnectionKind.HubReturn: return "hub_return";
            case WorldMapConnectionKind.AutomaticTransition: return "automatic_transition";
            case WorldMapConnectionKind.Walkway: return "walkway";
            default: return "other";
        }
    }
}
