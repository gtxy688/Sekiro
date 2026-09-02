using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using ARPG.Mgr;
namespace ARPG.UI
{

    // 胜利提示：Boss 命数清空。暗金面板，「再来一局」/「退出游戏」，右上角叉号退出。
    public class VictoryView : UIView
    {
        [SerializeField] private TMPro.TextMeshProUGUI victoryText;
        [SerializeField] private TMPro.TextMeshProUGUI hintText;
        [SerializeField] private CanvasGroup canvasGroup;

        private Button replayButton;
        private Button quitButton;

        public override void OnViewInit()
        {
            if (hintText == null)
                hintText = CombatPromptStyle.EnsureHint(transform, "击败 苇名弦一郎");
            CombatPromptStyle.EnsureChrome(transform, victoryText, hintText, new Vector2(560f, 360f), withActionButton: true);
            replayButton = CombatPromptStyle.EnsureActionButton(
                transform, "再来一局", "Replay", new Vector2(-140f, -108f), new Vector2(220f, 52f));
            quitButton = CombatPromptStyle.EnsureActionButton(
                transform, "退出游戏", "Quit", new Vector2(140f, -108f), new Vector2(220f, 52f));

            Transform panel = transform.Find("Panel");
            CombatPromptStyle.EnsureCloseButton(panel, CombatPromptStyle.QuitGame);

            if (replayButton != null)
            {
                replayButton.onClick.RemoveAllListeners();
                replayButton.onClick.AddListener(RestartScene);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(CombatPromptStyle.QuitGame);
            }

            WireNav();

            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        // 连战：胜利面板靠 Hide 收掉，输入封锁由 Controller 那边统一解。
        // 这里不碰 CombatInputGate / PlayerInput——那是 Controller 的职责，
        // 两边都管就会出现"View 解了锁但 Controller 又锁回去"的顺序依赖。
        public override void ResetForEncounter()
        {
            Hide();
        }

        public void ShowVictory()
        {
            Show();
            OnViewInit();

            canvasGroup.DOKill();
            if (victoryText != null) victoryText.transform.DOKill();

            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.35f).SetUpdate(true).SetLink(gameObject);
            if (victoryText != null)
            {
                victoryText.color = CombatPromptStyle.Accent;
                victoryText.transform.localScale = Vector3.one * 0.92f;
                victoryText.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            SelectReplay();
        }

        private void Update()
        {
            if (!isActiveAndEnabled) return;
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            CombatPromptStyle.ApplyButtonSelected(replayButton, selected != null && replayButton != null && selected == replayButton.gameObject);
            CombatPromptStyle.ApplyButtonSelected(quitButton, selected != null && quitButton != null && selected == quitButton.gameObject);
        }

        private void WireNav()
        {
            if (replayButton == null || quitButton == null) return;
            Navigation replayNav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = quitButton,
                selectOnLeft = quitButton
            };
            Navigation quitNav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = replayButton,
                selectOnRight = replayButton
            };
            replayButton.navigation = replayNav;
            quitButton.navigation = quitNav;
        }

        private void SelectReplay()
        {
            if (replayButton == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(replayButton.gameObject);
            StartCoroutine(SelectReplayNextFrame());
        }

        private IEnumerator SelectReplayNextFrame()
        {
            yield return null;
            if (replayButton == null || EventSystem.current == null) yield break;
            if (!replayButton.gameObject.activeInHierarchy) yield break;
            EventSystem.current.SetSelectedGameObject(replayButton.gameObject);
        }

        private void RestartScene()
        {
            CombatInputGate.SetBlocked(false);
            if (GamePause.IsPaused)
                GamePause.SetPaused(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

}
