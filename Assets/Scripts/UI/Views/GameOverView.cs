using DG.Tweening;
using UnityEngine;

using ARPG.Mgr;
namespace ARPG.UI
{

    // 玩家真正死亡时显示提示，与回生和胜利界面共用暗金样式。
    public class GameOverView : UIView
    {
        [SerializeField] private TMPro.TextMeshProUGUI deathText;
        [SerializeField] private TMPro.TextMeshProUGUI hintText;
        [SerializeField] private CanvasGroup canvasGroup;

        public override void OnViewInit()
        {
            CombatPromptStyle.EnsureChrome(transform, deathText, hintText, new Vector2(500f, 280f));
            Transform panel = transform.Find("Panel");
            CombatPromptStyle.EnsureCloseButton(panel, CombatPromptStyle.QuitGame);
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        // 复战时隐藏真死面板，由 OnDisable 配对释放 ShowGameOver 占用的光标。
        public override void ResetForEncounter()
        {
            Hide();
        }

        public void ShowGameOver()
        {
            CursorController.PushUi();
            Show();
            OnViewInit();

            canvasGroup.DOKill();
            if (deathText != null) deathText.transform.DOKill();

            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.4f).SetUpdate(true).SetLink(gameObject);
            if (deathText != null)
            {
                deathText.color = CombatPromptStyle.Accent;
                deathText.transform.localScale = Vector3.one * 0.92f;
                deathText.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        private void OnDisable()
        {
            CursorController.PopUi();
            if (canvasGroup != null) canvasGroup.DOKill();
            if (deathText != null) deathText.transform.DOKill();
        }
    }

}
