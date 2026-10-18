using System.Collections.Generic;
using System.Text;

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace ARPG.Editor
{
    // 构建前运行共享配置校验，有错误就中止，避免缺动画等静默失效问题进入构建。
    public class ArpgBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            List<ValidationIssue> issues = ArpgValidationRules.RunAll();
            int errors = ArpgValidationRules.CountErrors(issues);

            if (errors == 0)
            {
                Debug.Log($"[构建校验] 通过：{issues.Count} 条警告，0 条错误。");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("战斗配置校验未通过，构建已中止：");
            for (int i = 0; i < issues.Count; i++)
            {
                ValidationIssue issue = issues[i];
                if (issue.Severity != ValidationSeverity.Error) continue;
                sb.AppendLine("  " + issue);
            }
            sb.AppendLine();
            sb.AppendLine("用 Tools/战斗/战斗配置体检 可以看到全部结果并跳转到对应资产。");

            string failureText = sb.ToString();
            Debug.LogError(failureText);

            // batchmode 下没有窗口可弹，所以只打日志 + 抛异常
            throw new BuildFailedException(failureText);
        }
    }

}
