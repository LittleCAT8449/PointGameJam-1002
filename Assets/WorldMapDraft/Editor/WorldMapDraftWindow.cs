using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WorldMapDraftWindow : EditorWindow
{
    [MenuItem("Tools/World Map Draft/Open Map Editor")]
    public static void Open()
    {
        GetWindow<WorldMapDraftWindow>("World Map Draft");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Unity World Map Draft", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "使用 Moremap 的 39 房间 / 5 区域版本。Scene 中的矩形使用 Unity Transform 拖动和缩放；相邻关系与知识锁在房间 Inspector 中编辑。",
            MessageType.Info);

        if (GUILayout.Button("一键生成 Moremap 独立场景", GUILayout.Height(32)))
            WorldMapDraftGenerator.GenerateFromSource();

        GUILayout.Space(8f);
        EditorGUILayout.LabelField("JSON", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("导入 JSON 并生成新场景"))
                ImportJson();
            if (GUILayout.Button("导出当前场景 JSON"))
                ExportJson();
        }

        if (GUILayout.Button("校验当前场景地图"))
            ValidateActiveGraph();

        GUILayout.Space(8f);
        EditorGUILayout.LabelField("Scene 操作", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("选中房间后使用 W 拖动位置、R 缩放矩形；房间标签和连接线会随布局更新。", EditorStyles.wordWrappedLabel);
    }

    private static void ImportJson()
    {
        string path = EditorUtility.OpenFilePanel("导入世界地图 JSON", Application.dataPath, "json");
        if (string.IsNullOrEmpty(path))
            return;
        try
        {
            WorldMapDraftDocument document = WorldMapDraftGenerator.ReadDocument(path);
            WorldMapDraftValidationResult result = WorldMapDraftValidator.Validate(document);
            if (!result.IsValid)
            {
                EditorUtility.DisplayDialog("World Map Draft", "JSON 校验失败：\n- " + string.Join("\n- ", result.errors), "OK");
                return;
            }
            WorldMapDraftGenerator.GenerateScene(document);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("World Map Draft", "导入失败：\n" + exception.Message, "OK");
        }
    }

    private static void ExportJson()
    {
        WorldMapDraftGraph graph = FindGraphInActiveScene();
        if (graph == null)
        {
            EditorUtility.DisplayDialog("World Map Draft", "当前活动场景中没有地图根节点。", "OK");
            return;
        }

        WorldMapDraftDocument document = WorldMapDraftGenerator.BuildDocument(graph);
        WorldMapDraftValidationResult result = WorldMapDraftValidator.Validate(document);
        if (!result.IsValid && !EditorUtility.DisplayDialog("World Map Draft", "当前地图存在校验错误，仍要导出吗？\n\n" + string.Join("\n", result.errors.Take(8)), "仍然导出", "取消"))
            return;

        string path = EditorUtility.SaveFilePanelInProject(
            "导出世界地图 JSON",
            "WorldMapDraft_export",
            "json",
            "选择导出位置",
            WorldMapDraftGenerator.DataFolder);
        if (string.IsNullOrEmpty(path))
            return;

        string absolutePath = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path));
        File.WriteAllText(absolutePath, JsonUtility.ToJson(document, true));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[WorldMapDraft] JSON exported: " + path);
    }

    private static void ValidateActiveGraph()
    {
        WorldMapDraftGraph graph = FindGraphInActiveScene();
        if (graph == null)
        {
            EditorUtility.DisplayDialog("World Map Draft", "当前活动场景中没有地图根节点。", "OK");
            return;
        }
        WorldMapDraftDocument document = WorldMapDraftGenerator.BuildDocument(graph);
        WorldMapDraftValidationResult result = WorldMapDraftValidator.Validate(document);
        string report = WorldMapDraftValidator.ToMarkdown(document, result);
        Debug.Log(report);
        EditorUtility.DisplayDialog("World Map Draft", result.IsValid ? "地图校验通过。详情见 Console。" : "地图校验发现问题。详情见 Console。", "OK");
    }

    private static WorldMapDraftGraph FindGraphInActiveScene()
    {
        Scene active = SceneManager.GetActiveScene();
        return Resources.FindObjectsOfTypeAll<WorldMapDraftGraph>()
            .FirstOrDefault(graph => graph != null && graph.gameObject.scene == active && graph.gameObject.scene.IsValid());
    }
}
