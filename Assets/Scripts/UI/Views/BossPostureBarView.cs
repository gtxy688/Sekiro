using UnityEngine;
using UnityEngine.UI;

namespace ARPG.UI
{

    // 架势条：宽度映射架势。尖刺跟着条一起显示，不跟满条绑定。
    // 从未涨过架势 → 隐藏；涨过后归零，持续 5 秒再隐藏。
    public class BossPostureBarView : UIView
    {
        [Header("架势条")]
        [SerializeField] private Image leftFill;
        [SerializeField] private Image rightFill;
        [SerializeField] private Image spike;
        [SerializeField] private float hideDelay = 5f;

        private float leftMaxWidth;
        private float rightMaxWidth;
        private bool everHadPosture;
        private float zeroTimer;

        public override void OnViewInit()
        {
            if (leftFill != null) leftMaxWidth = leftFill.rectTransform.sizeDelta.x;
            if (rightFill != null) rightMaxWidth = rightFill.rectTransform.sizeDelta.x;
            zeroTimer = 0f;
            everHadPosture = false;
            HideBar();
        }

        private void Update()
        {
            if (!everHadPosture) return;
            if (zeroTimer <= 0f) return;

            zeroTimer -= Time.deltaTime;
            if (zeroTimer <= 0f) HideBar();
        }

        // 连战：新 Boss 开局必须是"从没涨过架势"的状态。
        //
        // everHadPosture 不清 → 上一场打满过架势，新 Boss 一上来条就常驻挂在屏幕顶部；
        // zeroTimer 不清 → 切场瞬间可能残留一个倒计时，条平白淡出一次。
        // 这两个字段都是"上一场的历史"，不属于任何角色，Controller 推值时推不到。
        public override void ResetForEncounter()
        {
            everHadPosture = false;
            zeroTimer = 0f;
            HideBar();
        }

        public void SetPosture(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            SetWidth(leftFill, leftMaxWidth * ratio);
            SetWidth(rightFill, rightMaxWidth * ratio);

            if (ratio > 0.001f)
            {
                everHadPosture = true;
                zeroTimer = 0f;
                ShowBar();
                return;
            }

            // 还没涨过架势：保持隐藏。涨过之后归零：开始 5 秒倒计时
            if (everHadPosture) zeroTimer = hideDelay;
            else HideBar();
        }

        private void ShowBar()
        {
            gameObject.SetActive(true);
            if (spike != null) spike.gameObject.SetActive(true);
        }

        private void HideBar()
        {
            zeroTimer = 0f;
            if (spike != null) spike.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        private static void SetWidth(Image img, float width)
        {
            if (img == null) return;
            Vector2 size = img.rectTransform.sizeDelta;
            size.x = width;
            img.rectTransform.sizeDelta = size;
        }
    }

}
