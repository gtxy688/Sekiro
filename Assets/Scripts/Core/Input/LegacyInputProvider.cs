using System.Collections.Generic;
using UnityEngine;

namespace Sekiro.Core.Input
{
    /// <summary>
    /// 旧 Input Manager 提供者，使用 Unity 内置 Input 系统（Input.GetKey / GetMouseButtonDown）。
    /// 作为未安装新 Input System 包时的默认备选方案。
    /// 按键映射通过 <see cref="ButtonMapping"/> 配置，不硬编码在逻辑中。
    /// </summary>
    public class LegacyInputProvider : IInputProvider
    {
        /// <summary>
        /// 单个战斗输入的按键映射，支持 KeyCode 和鼠标按键
        /// </summary>
        [System.Serializable]
        public struct ButtonMapping
        {
            /// <summary>键盘按键（可选，None 表示不使用键盘）</summary>
            public KeyCode key;

            /// <summary>鼠标按键索引（-1 表示不使用鼠标，0=左键，1=右键，2=中键）</summary>
            public int mouseButton;

            /// <summary>
            /// 创建键盘按键映射
            /// </summary>
            public static ButtonMapping FromKey(KeyCode key)
            {
                return new ButtonMapping { key = key, mouseButton = -1 };
            }

            /// <summary>
            /// 创建鼠标按键映射
            /// </summary>
            public static ButtonMapping FromMouse(int mouseButton)
            {
                return new ButtonMapping { key = KeyCode.None, mouseButton = mouseButton };
            }

            /// <summary>
            /// 检查本帧是否按下
            /// </summary>
            public bool IsPressed()
            {
                bool keyDown = key != KeyCode.None && UnityEngine.Input.GetKeyDown(key);
                bool mouseDown = mouseButton >= 0 && UnityEngine.Input.GetMouseButtonDown(mouseButton);
                return keyDown || mouseDown;
            }

            /// <summary>
            /// 检查是否按住
            /// </summary>
            public bool IsHeld()
            {
                bool keyHeld = key != KeyCode.None && UnityEngine.Input.GetKey(key);
                bool mouseHeld = mouseButton >= 0 && UnityEngine.Input.GetMouseButton(mouseButton);
                return keyHeld || mouseHeld;
            }
        }

        private readonly Dictionary<CombatInput, ButtonMapping> _mappings;
        private readonly string _horizontalAxis;
        private readonly string _verticalAxis;

        /// <summary>
        /// 创建旧 Input Manager 提供者，使用默认按键映射：
        /// <list type="bullet">
        ///   <item>攻击 = 鼠标左键</item>
        ///   <item>弹刀 = 鼠标右键</item>
        ///   <item>闪避/识破 = LeftShift</item>
        ///   <item>跳跃 = Space</item>
        ///   <item>回血 = E</item>
        ///   <item>锁定 = 鼠标中键</item>
        ///   <item>移动 = Horizontal / Vertical 轴（WASD）</item>
        /// </list>
        /// </summary>
        public LegacyInputProvider()
            : this(
                new Dictionary<CombatInput, ButtonMapping>
                {
                    { CombatInput.Attack, ButtonMapping.FromMouse(0) },
                    { CombatInput.Deflect, ButtonMapping.FromMouse(1) },
                    { CombatInput.Dodge, ButtonMapping.FromKey(KeyCode.LeftShift) },
                    { CombatInput.Mikiri, ButtonMapping.FromKey(KeyCode.LeftShift) },
                    { CombatInput.Jump, ButtonMapping.FromKey(KeyCode.Space) },
                    { CombatInput.Heal, ButtonMapping.FromKey(KeyCode.E) },
                    { CombatInput.LockOn, ButtonMapping.FromMouse(2) },
                },
                "Horizontal",
                "Vertical"
            )
        {
        }

        /// <summary>
        /// 创建旧 Input Manager 提供者，使用自定义按键映射
        /// </summary>
        /// <param name="mappings">战斗输入到按键的映射字典</param>
        /// <param name="horizontalAxis">水平移动轴名（Input Manager 中配置）</param>
        /// <param name="verticalAxis">垂直移动轴名（Input Manager 中配置）</param>
        public LegacyInputProvider(
            Dictionary<CombatInput, ButtonMapping> mappings,
            string horizontalAxis = "Horizontal",
            string verticalAxis = "Vertical")
        {
            _mappings = mappings ?? new Dictionary<CombatInput, ButtonMapping>();
            _horizontalAxis = horizontalAxis;
            _verticalAxis = verticalAxis;
        }

        /// <summary>
        /// 检查指定战斗输入是否在本帧被按下
        /// </summary>
        public bool IsPressed(CombatInput input)
        {
            if (_mappings.TryGetValue(input, out ButtonMapping mapping))
                return mapping.IsPressed();
            return false;
        }

        /// <summary>
        /// 检查指定战斗输入是否被按住
        /// </summary>
        public bool IsHeld(CombatInput input)
        {
            if (_mappings.TryGetValue(input, out ButtonMapping mapping))
                return mapping.IsHeld();
            return false;
        }

        /// <summary>
        /// 获取 WASD 移动输入向量
        /// </summary>
        public Vector2 GetMoveInput()
        {
            float x = UnityEngine.Input.GetAxisRaw(_horizontalAxis);
            float y = UnityEngine.Input.GetAxisRaw(_verticalAxis);
            return new Vector2(x, y).normalized;
        }

        /// <summary>
        /// 每帧更新（旧 Input Manager 不需要手动更新）
        /// </summary>
        public void Update()
        {
            // 旧 Input Manager 由 Unity 自动更新，无需手动处理
        }
    }
}
