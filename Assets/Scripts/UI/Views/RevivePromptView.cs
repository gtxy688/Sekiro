using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using ARPG.Mgr;
using ARPG.Player;
namespace ARPG.UI
{

    // 回生提示：层级固定在 GameScene/CombatCanvas/RespawnPanel。
    // 运行时只改 CanvasGroup / 颜色 / 文案，禁止创建或销毁子物体。
    public class RevivePromptView : UIView
    {
        public static readonly Color DeathRed = new Color(0.62f, 0.16f, 0.16f, 0.92f);
        public static readonly Color VignetteColor = new Color(0.06f, 0.01f, 0.01f, 0.5f);
        public static readonly Color ChoiceIdle = new Color(0.92f, 0.9f, 0.88f, 0.95f);
        // 对齐 RevivePendingState 倒地时长，变暗结束才出选项
        public const float DeathFadeDuration = 1.2f;
        private const float ChoiceFadeDuration = 0.3f;

        [SerializeField] private TextMeshProUGUI deathKanji;
        [SerializeField] private TextMeshProUGUI deathSub;
        [SerializeField] private TextMeshProUGUI giveUpLabel;
        [SerializeField] private TextMeshProUGUI reviveLabel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private CanvasGroup contentGroup;
        [SerializeField] private Image vignette;
        [SerializeField] private Button giveUpButton;
        [SerializeField] private Button reviveButton;

        private Action onGiveUp;
        private Action onRevive;
        private Tween fadeTween;
        private Tween vignetteTween;
        private Tween contentTween;
        private bool fadeDone;
        private bool choiceReady;
        private bool choicesShown;

        private PlayerInput playerInput;
        private bool listeningControls;

        public void BindPlayerInput(PlayerInput input)
        {
            SetPlayerInput(input);
        }

        // 倒地提示是"一段流程的中间态"：只由「回生成功」或「真死」收尾。
        // 连战切场两条都不走，不主动收尾的话上一场的「死」字和选项会挂在新一场画面上，
        // 而且光标还被 PushUi 占着，玩家动不了鼠标。
        //
        // HidePrompt 里重复 PopUi 是安全的：CursorController 的计数有 >0 保护。
        public override void ResetForEncounter()
        {
            HidePrompt();
        }

        // 倒地开始：只铺暗红压暗，不显示「死」和选项
        public void BeginDeathFade(PlayerInput input = null)
        {
            fadeDone = false;
            choiceReady = false;
            choicesShown = false;
            onGiveUp = null;
            onRevive = null;
            SetPlayerInput(input);

            Show();
            KillTweens();
            SetContentVisible(false);

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;

            Color start = VignetteColor;
            start.a = 0f;
            vignette.color = start;
            vignetteTween = vignette.DOFade(VignetteColor.a, DeathFadeDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    fadeDone = true;
                    TryRevealChoices();
                });
        }

        // 倒地结束：记下回调，等压暗完成后再弹出选项并开输入
        public void ShowChoices(Action giveUp, Action revive)
        {
            onGiveUp = giveUp;
            onRevive = revive;
            choiceReady = true;
            TryRevealChoices();
        }

        public void HidePrompt()
        {
            CursorController.PopUi();
            fadeDone = false;
            choiceReady = false;
            choicesShown = false;
            UnbindControlsChanged();
            KillTweens();

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            fadeTween = canvasGroup.DOFade(0f, 0.15f).SetUpdate(true).OnComplete(Hide);
        }

        private void TryRevealChoices()
        {
            if (choicesShown || !fadeDone || !choiceReady) return;
            choicesShown = true;

            WireButtons();
            RefreshPrompts();
            SetContentVisible(true);

            contentGroup.alpha = 0f;
            contentTween = contentGroup.DOFade(1f, ChoiceFadeDuration).SetUpdate(true);

            deathKanji.transform.DOKill();
            deathKanji.transform.localScale = Vector3.one * 0.94f;
            deathKanji.transform.DOScale(Vector3.one, ChoiceFadeDuration).SetEase(Ease.OutCubic).SetUpdate(true);

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            CursorController.PushUi();
        }

        private void SetContentVisible(bool visible)
        {
            contentGroup.alpha = visible ? 1f : 0f;
            contentGroup.interactable = visible;
            contentGroup.blocksRaycasts = visible;
        }

        private void KillTweens()
        {
            if (canvasGroup != null) canvasGroup.DOKill();
            if (contentGroup != null) contentGroup.DOKill();
            if (vignette != null) vignette.DOKill();
            if (deathKanji != null) deathKanji.transform.DOKill();
            fadeTween = null;
            vignetteTween = null;
            contentTween = null;
        }

        private void WireButtons()
        {
            giveUpButton.onClick.RemoveAllListeners();
            giveUpButton.onClick.AddListener(() => onGiveUp?.Invoke());

            reviveButton.onClick.RemoveAllListeners();
            reviveButton.onClick.AddListener(() => onRevive?.Invoke());
        }

        private void SetPlayerInput(PlayerInput input)
        {
            if (input == null)
                input = FindObjectOfType<PlayerInput>();

            if (playerInput == input && listeningControls)
            {
                InputRebindService.Load(playerInput.actions);
                return;
            }

            UnbindControlsChanged();
            playerInput = input;

            playerInput.onControlsChanged += HandleControlsChanged;
            listeningControls = true;
            InputRebindService.Load(playerInput.actions);
        }

        private void UnbindControlsChanged()
        {
            if (playerInput != null && listeningControls)
                playerInput.onControlsChanged -= HandleControlsChanged;
            listeningControls = false;
        }

        private void HandleControlsChanged(PlayerInput _)
        {
            RefreshPrompts();
        }

        private void RefreshPrompts()
        {
            string group = InputRebindService.ResolveControlGroup(playerInput);
            string deflectName = string.Empty;
            string attackName = string.Empty;

            InputAction deflect = playerInput.actions.FindAction("Deflect");
            InputAction attack = playerInput.actions.FindAction("Attack");
            deflectName = InputRebindService.GetBindingDisplay(deflect, group);
            attackName = InputRebindService.GetBindingDisplay(attack, group);

            giveUpLabel.text = FormatPrompt(deflectName, "就此死去");
            reviveLabel.text = FormatPrompt(attackName, "起死回生");
        }

        private static string FormatPrompt(string key, string action)
        {
            if (string.IsNullOrEmpty(key) || key == "—")
                return action;
            return key + "    " + action;
        }

        private void OnDestroy()
        {
            UnbindControlsChanged();
        }
    }

}
