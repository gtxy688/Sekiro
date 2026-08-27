using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 胜利提示：Boss 命数清空。外观跟暂停设置页同一套暗金，带「再来一局」。
public class VictoryView : UIView
{
    [SerializeField] private TMPro.TextMeshProUGUI victoryText;
    [SerializeField] private TMPro.TextMeshProUGUI hintText;
    [SerializeField] private CanvasGroup canvasGroup;

    private Button replayButton;

    public override void OnViewInit()
    {
        if (hintText == null)
            hintText = CombatPromptStyle.EnsureHint(transform, "击败 苇名弦一郎");
        CombatPromptStyle.EnsureChrome(transform, victoryText, hintText, new Vector2(500f, 340f), withActionButton: true);
        replayButton = CombatPromptStyle.EnsureActionButton(transform, "再来一局");
        if (replayButton != null)
        {
            replayButton.onClick.RemoveAllListeners();
            replayButton.onClick.AddListener(RestartScene);
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    public void ShowVictory()
    {
        Show();
        OnViewInit();

        canvasGroup.DOKill();
        if (victoryText != null) victoryText.transform.DOKill();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.35f).SetUpdate(true);
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
        if (!isActiveAndEnabled || replayButton == null) return;
        bool selected = EventSystem.current != null
                        && EventSystem.current.currentSelectedGameObject == replayButton.gameObject;
        CombatPromptStyle.ApplyButtonSelected(replayButton, selected);
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
