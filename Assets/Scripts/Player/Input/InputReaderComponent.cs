using Sekiro.Core.Input;
using UnityEngine;

namespace Sekiro.Player.Input
{
    /// <summary>
    /// InputReader 的 MonoBehaviour 包装器，挂载在玩家 GameObject 上。
    /// 负责创建 InputReader 实例、管理生命周期，并暴露查询方法给 PlayerController。
    /// </summary>
    /// <remarks>
    /// <para>挂载方式：将此类挂载到玩家 GameObject 上，Inspector 中可配置输入提供者和按键映射。</para>
    /// <para>PlayerController 通过 GetComponent&lt;InputReaderComponent&gt;() 获取引用，调用查询方法。</para>
    /// </remarks>
    public class InputReaderComponent : MonoBehaviour
    {
        [Header("输入配置")]
        [Tooltip("自定义输入提供者（留空则自动检测：优先新 Input System，备选旧 Input Manager）")]
        [SerializeField]
        private MonoBehaviour _customProvider;

        [Header("旧 Input Manager 按键映射（不使用新 Input System 时生效）")]
        [SerializeField]
        private KeyCode _dodgeKey = KeyCode.LeftShift;

        [SerializeField]
        private KeyCode _jumpKey = KeyCode.Space;

        [SerializeField]
        private KeyCode _healKey = KeyCode.E;

        /// <summary>
        /// 内部 InputReader 实例
        /// </summary>
        private InputReader _inputReader;

        /// <summary>
        /// 获取内部 InputReader 实例（供 PlayerController 使用）
        /// </summary>
        public InputReader Reader => _inputReader;

        /// <summary>
        /// 初始化 InputReader，优先使用自定义提供者，否则使用旧 Input Manager
        /// </summary>
        private void Awake()
        {
            IInputProvider provider = null;

            // 尝试使用 Inspector 中配置的自定义提供者
            if (_customProvider is IInputProvider customProvider)
            {
                provider = customProvider;
            }
            else
            {
                // 默认使用旧 Input Manager，按键可通过 Inspector 配置
                provider = CreateConfiguredLegacyProvider();
            }

            _inputReader = new InputReader(provider);
        }

        /// <summary>
        /// 每帧更新输入状态
        /// </summary>
        private void Update()
        {
            _inputReader?.Update();
        }

        #region 查询方法代理（供 PlayerController 直接调用）

        /// <summary>攻击键是否在本帧被按下</summary>
        public bool IsAttackPressed() => _inputReader != null && _inputReader.IsAttackPressed();

        /// <summary>弹刀键是否在本帧被按下</summary>
        public bool IsDeflectPressed() => _inputReader != null && _inputReader.IsDeflectPressed();

        /// <summary>弹刀键是否被按住</summary>
        public bool IsDeflectHeld() => _inputReader != null && _inputReader.IsDeflectHeld();

        /// <summary>获取 WASD 移动输入向量</summary>
        public Vector2 GetMoveInput() => _inputReader != null ? _inputReader.GetMoveInput() : Vector2.zero;

        /// <summary>闪避键是否在本帧被按下</summary>
        public bool IsDodgePressed() => _inputReader != null && _inputReader.IsDodgePressed();

        /// <summary>跳跃键是否在本帧被按下</summary>
        public bool IsJumpPressed() => _inputReader != null && _inputReader.IsJumpPressed();

        /// <summary>回血键是否在本帧被按下</summary>
        public bool IsHealPressed() => _inputReader != null && _inputReader.IsHealPressed();

        /// <summary>锁定键是否在本帧被按下</summary>
        public bool IsLockOnPressed() => _inputReader != null && _inputReader.IsLockOnPressed();

        #endregion

        /// <summary>
        /// 创建使用 Inspector 配置按键的旧 Input Manager 提供者
        /// </summary>
        private LegacyInputProvider CreateConfiguredLegacyProvider()
        {
            var mappings = new System.Collections.Generic.Dictionary<CombatInput, LegacyInputProvider.ButtonMapping>
            {
                { CombatInput.Attack, LegacyInputProvider.ButtonMapping.FromMouse(0) },
                { CombatInput.Deflect, LegacyInputProvider.ButtonMapping.FromMouse(1) },
                { CombatInput.Dodge, LegacyInputProvider.ButtonMapping.FromKey(_dodgeKey) },
                { CombatInput.Mikiri, LegacyInputProvider.ButtonMapping.FromKey(_dodgeKey) },
                { CombatInput.Jump, LegacyInputProvider.ButtonMapping.FromKey(_jumpKey) },
                { CombatInput.Heal, LegacyInputProvider.ButtonMapping.FromKey(_healKey) },
                { CombatInput.LockOn, LegacyInputProvider.ButtonMapping.FromMouse(2) },
            };

            return new LegacyInputProvider(mappings);
        }
    }
}
