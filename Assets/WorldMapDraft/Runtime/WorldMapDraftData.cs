using System;
using System.Collections.Generic;

[Serializable]
public sealed class WorldMapDraftDocument
{
    public WorldMapMeta meta = new WorldMapMeta();
    public List<WorldMapRegionData> regions = new List<WorldMapRegionData>();
    public List<WorldMapKnowledgeData> knowledge = new List<WorldMapKnowledgeData>();
    public List<WorldMapGateData> gates = new List<WorldMapGateData>();
    public List<WorldMapRoomData> rooms = new List<WorldMapRoomData>();
    public List<WorldMapConnectionData> connections = new List<WorldMapConnectionData>();
    public List<WorldMapLoopData> exploration_loops = new List<WorldMapLoopData>();
    public List<WorldMapEarlySightlineData> early_sightlines = new List<WorldMapEarlySightlineData>();
    public WorldMapLayoutData layout = new WorldMapLayoutData();
    public List<string> critical_path = new List<string>();
    public List<string> progression_assumptions = new List<string>();
    public WorldMapValidationSummary validation = new WorldMapValidationSummary();
}

[Serializable]
public sealed class WorldMapMeta
{
    public string title;
    public string status;
    public string[] basis;
    public string language;
    public int room_count;
    public int region_count;
    public int connection_count;
    public int exploration_loop_count;
    public string layout_note;
    public string canon_boundary;
    public string output_folder;
    public string schema_note;
    public string change_summary;
}

[Serializable]
public sealed class WorldMapRegionData
{
    public string id;
    public string name;
    public string relative_position;
    public string purpose;
    public string status;
    public string color_hex;
}

[Serializable]
public sealed class WorldMapKnowledgeData
{
    public string id;
    public string symbol;
    public string name;
    public string room;
    public int order;
    public bool critical;
    public List<string> requires_before = new List<string>();
    public string acquisition;
}

[Serializable]
public sealed class WorldMapGatePreviewData
{
    public string room;
    public List<string> known = new List<string>();
}

[Serializable]
public sealed class WorldMapGateData
{
    public string id;
    public List<string> requires = new List<string>();
    public string first_critical_use;
    public List<WorldMapGatePreviewData> previews = new List<WorldMapGatePreviewData>();
    public string signposting;
}

[Serializable]
public sealed class WorldMapPixelRect
{
    public float x;
    public float y;
    public float width;
    public float height;
}

[Serializable]
public sealed class WorldMapRoomPositionData
{
    public int grid_x;
    public int grid_y;
    public WorldMapPixelRect drawio_px = new WorldMapPixelRect();
}

[Serializable]
public sealed class WorldMapRoomData
{
    public string id;
    public string name;
    public string area_id;
    public WorldMapRoomPositionData position = new WorldMapRoomPositionData();
    public string function;
    public string pacing_beat;
    public List<string> grants_knowledge = new List<string>();
    public bool optional;
    public bool hidden;
    public List<string> knowledge_visible_before_mastery = new List<string>();
    public string recovery_and_return;
    public string region_id;
    public string region_name;
}

[Serializable]
public sealed class WorldMapConnectionData
{
    public string id;
    public string from;
    public string to;
    public string label;
    public string connection_type;
    public string directionality;
    public bool one_way;
    public List<string> requires_knowledge = new List<string>();
    public bool shortcut;
    public bool hidden;
    public bool optional;
    public string opened_from;
    public List<string> previewed_from = new List<string>();
    public string notes;
}

[Serializable]
public sealed class WorldMapLoopData
{
    public string id;
    public string name;
    public List<string> cycle = new List<string>();
    public string shortcut;
    public List<string> shortcut_path = new List<string>();
}

[Serializable]
public sealed class WorldMapEarlySightlineData
{
    public string from;
    public string target;
    public string clue;
}

[Serializable]
public sealed class WorldMapGridData
{
    public float step_x;
    public float step_y;
    public float room_w;
    public float room_h;
    public float origin_x;
    public float origin_y;
    public string unit;
}

[Serializable]
public sealed class WorldMapRegionArrangementData
{
    public string A1;
    public string A2;
    public string A3;
    public string A4;
    public string A5;
}

[Serializable]
public sealed class WorldMapLayoutData
{
    public string coordinate_system;
    public WorldMapGridData grid = new WorldMapGridData();
    public WorldMapRegionArrangementData region_arrangement = new WorldMapRegionArrangementData();
    public float[] diagram_room_size_px;
    public float unity_units_per_100_pixels = 1f;
}

[Serializable]
public sealed class WorldMapValidationSummary
{
    public int room_count;
    public int region_count;
    public int connection_count;
    public int exploration_loop_count;
    public bool all_rooms_reachable;
    public int reachable_room_count;
    public bool all_critical_path_rooms_reachable;
    public bool all_critical_knowledge_reachable_in_order;
    public int room_rectangle_overlap_count;
    public bool all_exploration_loops_complete;
    public int shortcut_loop_count;
    public string scope;
}
