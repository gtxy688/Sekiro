using System.Collections.Generic;

using UnityEditor;
using UnityEngine;
namespace ARPG.Editor
{
    // 一键体检战斗配置：动画状态名、判定窗口时序、假红条、脉冲区间、音效/出箭时间点。
    // 走的规则和构建前校验是同一份（ArpgValidationRules），这里只是把它显示出来并支持跳转。
    public class ArpgValidatorWindow : EditorWindow
    {
        const string MenuPath = "Tools/战斗/战斗配置体检";

        List<ValidationIssue> issues = new List<ValidationIssue>();
        Vector2 scroll;
        bool onlyErrors;
        int errorCount;
        int warningCount;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            ArpgValidatorWindow w = GetWindow<ArpgValidatorWindow>("战斗配置体检");
            w.Run();
            w.Show();
        }

        void OnEnable()
        {
            // 打开即跑一次：这个窗口的定位是"改完配置顺手按一下"，不是常驻面板
            Run();
        }

        void Run()
        {
            issues = ArpgValidationRules.RunAll();
            issues.Sort((a, b) => ((int)a.Severity).CompareTo((int)b.Severity));
            errorCount = ArpgValidationRules.CountErrors(issues);
            warningCount = issues.Count - errorCount;
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重新校验", GUILayout.Height(26)))
                Run();
            onlyErrors = EditorGUILayout.Toggle("只看错误", onlyErrors, GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            MessageType type = errorCount > 0
                ? MessageType.Error
                : (warningCount > 0 ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.HelpBox($"错误 {errorCount} 条，警告 {warningCount} 条。", type);

            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < issues.Count; i++)
            {
                ValidationIssue issue = issues[i];
                if (onlyErrors && issue.Severity != ValidationSeverity.Error) continue;
                DrawIssue(issue);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawIssue(ValidationIssue issue)
        {
            bool isError = issue.Severity == ValidationSeverity.Error;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            Color oldColor = GUI.color;
            GUI.color = isError ? new Color(1f, 0.55f, 0.5f) : new Color(1f, 0.85f, 0.4f);
            EditorGUILayout.LabelField(isError ? "错误" : "警告", EditorStyles.boldLabel, GUILayout.Width(36));
            GUI.color = oldColor;

            EditorGUILayout.LabelField(issue.Where, EditorStyles.boldLabel);

            if (issue.Target != null && GUILayout.Button("定位", GUILayout.Width(44)))
            {
                Selection.activeObject = issue.Target;
                EditorGUIUtility.PingObject(issue.Target);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }
    }

}
