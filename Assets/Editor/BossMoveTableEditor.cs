#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BossMoveTable))]
public class BossMoveTableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("打开招式伤害表"))
            BossMoveDamageWindow.Open((BossMoveTable)target);
        if (GUILayout.Button("打开攻击时间轴"))
            AttackTimelineWindow.Open((BossMoveTable)target);
        if (GUILayout.Button("填入弦一郎默认招式表"))
        {
            BossMoveTable t = (BossMoveTable)target;
            Undo.RecordObject(t, "Fill Genichiro moves");
            GenichiroMoveCatalog.Apply(t);
            EditorUtility.SetDirty(t);
        }
        EditorGUILayout.HelpBox("「填入弦一郎默认招式表」会覆盖已对过的判定和音效。", MessageType.Warning);
    }

    [MenuItem("ARPG/Create Genichiro Move Table")]
    static void CreateAsset()
    {
        const string path = "Assets/SO/Boss/GenichiroMoveTable.asset";
        BossMoveTable existing = AssetDatabase.LoadAssetAtPath<BossMoveTable>(path);
        if (existing != null)
        {
            Selection.activeObject = existing;
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/SO"))
            AssetDatabase.CreateFolder("Assets", "SO");
        if (!AssetDatabase.IsValidFolder("Assets/SO/Boss"))
            AssetDatabase.CreateFolder("Assets/SO", "Boss");

        BossMoveTable t = ScriptableObject.CreateInstance<BossMoveTable>();
        GenichiroMoveCatalog.Apply(t);
        AssetDatabase.CreateAsset(t, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = t;
    }
}
#endif
