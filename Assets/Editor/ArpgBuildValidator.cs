using System.Collections.Generic;
using System.Text;

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace ARPG.Editor
{
    // 构建前挡住非法战斗配置。
    //
    // 为什么必须让构建失败而不是只打警告：这类错误运行时不报错——
    // 招式表的动画状态名写错，那个招就静默地永远不触发，只有打到那一招才会发现。
    // 警告日志在构建输出里一闪而过，等于没有。
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
