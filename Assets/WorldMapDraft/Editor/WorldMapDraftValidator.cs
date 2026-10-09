using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public sealed class WorldMapDraftValidationResult
{
    public readonly List<string> errors = new List<string>();
    public readonly List<string> warnings = new List<string>();
    public int reachableRoomCount;
    public int overlapCount;
    public int completedLoopCount;

    public bool IsValid => errors.Count == 0;
}

public static class WorldMapDraftValidator
{
    public static WorldMapDraftValidationResult Validate(WorldMapDraftDocument document)
    {
        WorldMapDraftValidationResult result = new WorldMapDraftValidationResult();
        if (document == null)
        {
            result.errors.Add("地图 JSON 为空或格式无法解析。");
            return result;
        }

        List<WorldMapRoomData> rooms = document.rooms ?? new List<WorldMapRoomData>();
        List<WorldMapRegionData> regions = document.regions ?? new List<WorldMapRegionData>();
        List<WorldMapConnectionData> connections = document.connections ?? new List<WorldMapConnectionData>();
        List<WorldMapKnowledgeData> knowledge = document.knowledge ?? new List<WorldMapKnowledgeData>();
        List<WorldMapLoopData> loops = document.exploration_loops ?? new List<WorldMapLoopData>();

        Dictionary<string, WorldMapRoomData> roomById = new Dictionary<string, WorldMapRoomData>();
        foreach (WorldMapRoomData room in rooms)
        {
            if (room == null || string.IsNullOrWhiteSpace(room.id))
            {
                result.errors.Add("发现没有房间 ID 的房间。");
                continue;
            }
            if (roomById.ContainsKey(room.id))
                result.errors.Add("房间 ID 重复：" + room.id);
            else
                roomById.Add(room.id, room);
        }

        HashSet<string> regionIds = new HashSet<string>();
        foreach (WorldMapRegionData region in regions)
        {
            if (region == null || string.IsNullOrWhiteSpace(region.id) || !regionIds.Add(region.id))
                result.errors.Add("区域 ID 为空或重复。");
        }

        HashSet<string> knowledgeIds = new HashSet<string>();
        foreach (WorldMapKnowledgeData item in knowledge)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id) || !knowledgeIds.Add(item.id))
                result.errors.Add("知识 ID 为空或重复。");
            else if (!roomById.ContainsKey(item.room))
                result.errors.Add($"知识“{item.name}”的获取房间 {item.room} 不存在。");
        }

        HashSet<string> connectionIds = new HashSet<string>();
        foreach (WorldMapConnectionData edge in connections)
        {
            if (edge == null || string.IsNullOrWhiteSpace(edge.id) || !connectionIds.Add(edge.id))
            {
                result.errors.Add("连接 ID 为空或重复。");
                continue;
            }
            if (!roomById.ContainsKey(edge.from) || !roomById.ContainsKey(edge.to))
                result.errors.Add($"连接 {edge.id} 的端点无效：{edge.from} → {edge.to}。");
            foreach (string required in edge.requires_knowledge ?? new List<string>())
                if (!knowledgeIds.Contains(required) && !knowledge.Any(k => k != null && k.id == required))
                    result.errors.Add($"连接 {edge.id} 要求未定义知识：{required}。");
        }

        foreach (WorldMapRoomData room in rooms)
        {
            if (room == null)
                continue;
            string assignedRegion = string.IsNullOrEmpty(room.region_id) ? room.area_id : room.region_id;
            if (!regionIds.Contains(assignedRegion))
                result.errors.Add($"房间 {room.id} 引用未定义区域：{assignedRegion}。");
            foreach (string grant in room.grants_knowledge ?? new List<string>())
                if (!knowledge.Any(k => k != null && k.id == grant))
                    result.errors.Add($"房间 {room.id} 发放未定义知识：{grant}。");
        }

        foreach (WorldMapKnowledgeData item in knowledge)
        {
            if (item == null)
                continue;
            foreach (string prerequisite in item.requires_before ?? new List<string>())
                if (!knowledgeIds.Contains(prerequisite))
                    result.errors.Add($"知识 {item.id} 的前置知识未定义：{prerequisite}。");
        }

        if (rooms.Count == 0)
        {
            result.errors.Add("地图没有房间。");
            return result;
        }

        string startId = document.critical_path != null && document.critical_path.Count > 0
            ? document.critical_path[0]
            : rooms[0].id;
        if (!roomById.ContainsKey(startId))
        {
            result.errors.Add("关键路径起点不存在：" + startId);
            return result;
        }

        HashSet<string> allKnown = new HashSet<string>();
        HashSet<string> reachable = FindReachable(startId, connections, roomById, allKnown, acquireKnowledge: true);
        result.reachableRoomCount = reachable.Count;
        if (reachable.Count != roomById.Count)
            result.errors.Add($"有 {roomById.Count - reachable.Count} 个房间在全部可取得知识下仍不可达。");

        HashSet<string> acquiredInOrder = new HashSet<string>();
        if (roomById[startId].grants_knowledge != null)
            foreach (string grant in roomById[startId].grants_knowledge)
                acquiredInOrder.Add(grant);
        foreach (WorldMapKnowledgeData item in knowledge.Where(k => k != null).OrderBy(k => k.order))
        {
            if (!roomById.ContainsKey(item.room))
                continue;
            HashSet<string> before = FindReachable(startId, connections, roomById, acquiredInOrder, acquireKnowledge: false);
            if (!before.Contains(item.room))
                result.errors.Add($"知识“{item.name}”无法在取得自身之前到达（房间 {item.room}）。");
            foreach (string prerequisite in item.requires_before ?? new List<string>())
                if (!acquiredInOrder.Contains(prerequisite))
                    result.errors.Add($"知识顺序错误：取得“{item.name}”前缺少 {prerequisite}。");
            acquiredInOrder.Add(item.id);
        }

        foreach (string criticalRoom in document.critical_path ?? new List<string>())
            if (!reachable.Contains(criticalRoom))
                result.errors.Add("关键路径房间不可达：" + criticalRoom);

        result.overlapCount = CountRectangleOverlaps(rooms);
        if (result.overlapCount > 0)
            result.warnings.Add($"有 {result.overlapCount} 对房间矩形重叠；地图仍可编辑，但建议调整布局。 ");

        foreach (WorldMapLoopData loop in loops)
        {
            if (loop == null || loop.cycle == null || loop.cycle.Count < 3 || loop.cycle[0] != loop.cycle[loop.cycle.Count - 1])
            {
                result.errors.Add("探索回环格式无效：" + (loop == null ? "(null)" : loop.id));
                continue;
            }
            bool complete = true;
            for (int i = 0; i < loop.cycle.Count - 1; i++)
                if (!HasConnection(loop.cycle[i], loop.cycle[i + 1], connections))
                    complete = false;
            if (complete)
                result.completedLoopCount++;
            else
                result.errors.Add("探索回环缺少直接连接：" + loop.id + " · " + loop.name);
        }

        foreach (WorldMapEarlySightlineData sightline in document.early_sightlines ?? new List<WorldMapEarlySightlineData>())
        {
            if (sightline == null || !roomById.ContainsKey(sightline.from))
                result.errors.Add("提前预览记录引用了不存在的房间。");
            else if (!reachable.Contains(sightline.from))
                result.errors.Add("提前预览所在房间不可达：" + sightline.from);
        }

        if (result.completedLoopCount != loops.Count)
            result.errors.Add("探索回环检查未全部通过。");

        document.meta.room_count = rooms.Count;
        document.meta.region_count = regions.Count;
        document.meta.connection_count = connections.Count;
        document.meta.exploration_loop_count = loops.Count;
        document.validation = new WorldMapValidationSummary
        {
            room_count = rooms.Count,
            region_count = regions.Count,
            connection_count = connections.Count,
            exploration_loop_count = loops.Count,
            all_rooms_reachable = result.reachableRoomCount == rooms.Count,
            reachable_room_count = result.reachableRoomCount,
            all_critical_path_rooms_reachable = (document.critical_path ?? new List<string>()).All(reachable.Contains),
            all_critical_knowledge_reachable_in_order = !result.errors.Any(e => e.Contains("知识“")),
            room_rectangle_overlap_count = result.overlapCount,
            all_exploration_loops_complete = result.completedLoopCount == loops.Count,
            shortcut_loop_count = loops.Count(l => l != null && !string.IsNullOrEmpty(l.shortcut)),
            scope = "房间拓扑、知识锁顺序、回环与抽象矩形布局；不代表可玩关卡的跳跃几何验证。"
        };
        return result;
    }

    private static HashSet<string> FindReachable(
        string startId,
        List<WorldMapConnectionData> connections,
        Dictionary<string, WorldMapRoomData> roomById,
        HashSet<string> known,
        bool acquireKnowledge)
    {
        HashSet<string> reached = new HashSet<string> { startId };
        bool changed;
        do
        {
            changed = false;
            if (acquireKnowledge)
            {
                foreach (string roomId in reached.ToArray())
                    foreach (string grant in roomById[roomId].grants_knowledge ?? new List<string>())
                        changed |= known.Add(grant);
            }

            foreach (WorldMapConnectionData edge in connections)
            {
                if (edge == null || edge.requires_knowledge == null || !edge.requires_knowledge.All(known.Contains))
                    continue;
                if (reached.Contains(edge.from) && roomById.ContainsKey(edge.to))
                    changed |= reached.Add(edge.to);
                if (!edge.one_way && reached.Contains(edge.to) && roomById.ContainsKey(edge.from))
                    changed |= reached.Add(edge.from);
            }
        } while (changed);
        return reached;
    }

    private static bool HasConnection(string from, string to, List<WorldMapConnectionData> connections)
    {
        return connections.Any(edge => edge != null &&
            ((edge.from == from && edge.to == to) || (!edge.one_way && edge.from == to && edge.to == from)));
    }

    private static int CountRectangleOverlaps(List<WorldMapRoomData> rooms)
    {
        int overlaps = 0;
        for (int i = 0; i < rooms.Count; i++)
        for (int j = i + 1; j < rooms.Count; j++)
        {
            WorldMapPixelRect a = rooms[i]?.position?.drawio_px;
            WorldMapPixelRect b = rooms[j]?.position?.drawio_px;
            if (a == null || b == null)
                continue;
            bool separated = a.x + a.width <= b.x || b.x + b.width <= a.x || a.y + a.height <= b.y || b.y + b.height <= a.y;
            if (!separated)
                overlaps++;
        }
        return overlaps;
    }

    public static string ToMarkdown(WorldMapDraftDocument document, WorldMapDraftValidationResult result)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("# World Map Draft Validation");
        text.AppendLine();
        text.AppendLine($"- Map: {document.meta.title}");
        text.AppendLine($"- Rooms: {document.rooms.Count} / {document.regions.Count} regions / {document.connections.Count} connections");
        text.AppendLine($"- Knowledge: {document.knowledge.Count}; exploration loops: {document.exploration_loops.Count}");
        text.AppendLine($"- Reachable rooms: {result.reachableRoomCount}/{document.rooms.Count}");
        text.AppendLine($"- Completed loops: {result.completedLoopCount}/{document.exploration_loops.Count}");
        text.AppendLine($"- Rectangle overlaps: {result.overlapCount}");
        text.AppendLine($"- Result: {(result.IsValid ? "PASS" : "FAIL")}");
        text.AppendLine();
        text.AppendLine("## Errors");
        if (result.errors.Count == 0)
            text.AppendLine("- None");
        else
            foreach (string error in result.errors)
                text.AppendLine("- " + error);
        text.AppendLine();
        text.AppendLine("## Warnings");
        if (result.warnings.Count == 0)
            text.AppendLine("- None");
        else
            foreach (string warning in result.warnings)
                text.AppendLine("- " + warning);
        text.AppendLine();
        text.AppendLine("This is an abstract room-map check. It does not validate platforming geometry or player movement metrics.");
        return text.ToString();
    }
}
