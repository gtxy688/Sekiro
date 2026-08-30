using UnityEditor;
using UnityEngine;

using ARPG.Configs;
namespace ARPG.Editor
{

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
