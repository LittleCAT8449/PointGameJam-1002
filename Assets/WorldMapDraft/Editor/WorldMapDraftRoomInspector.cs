using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldMapRoom))]
[CanEditMultipleObjects]
public sealed class WorldMapDraftRoomInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("房间位置和大小由 Transform 控制。使用 Scene 的 Move (W) 与 Scale (R) 工具拖动或缩放。连接目标、类型和知识锁可在下方列表编辑。", MessageType.Info);
        DrawDefaultInspector();
    }
}
