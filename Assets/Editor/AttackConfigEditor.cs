using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AttackConfig))]
public class AttackConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("打开攻击时间轴"))
            AttackTimelineWindow.Open((AttackConfig)target);
    }
}
