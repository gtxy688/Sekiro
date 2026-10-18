using UnityEditor;
using UnityEngine;

using ARPG.Configs;
namespace ARPG.Editor
{

    // 在默认攻击配置 Inspector 中提供时间轴入口。
    [CustomEditor(typeof(AttackConfig))]
    public class AttackConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("打开攻击时间轴"))
                AttackTimelineWindow.Open((AttackConfig)target);
        }
    }

}
