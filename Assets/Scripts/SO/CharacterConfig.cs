using UnityEngine;
using UnityEngine.Serialization;

using ARPG.FrameWork.States.Air;
using ARPG.FrameWork.States.Ground;
namespace ARPG.Configs
{

    // 玩家和 Boss 的角色属性配置，供数值、移动和状态逻辑读取。
    // 最大值与初始次数在这里配置；当前 HP、架势等可变数据保存在各角色的 CombatStats。
    // 攻击伤害另由 AttackConfig、BossMoveTable 和结算默认值提供。
    [CreateAssetMenu(fileName = "NewCharacterConfig", menuName = "Combat/Character Config")]
    public class CharacterConfig : ScriptableObject
    {
        [Header("血量")]
        public int MaxHP = 1000;          // 最大血量
        public int HealAmount = 300;      // 葫芦每次回复量

        [Header("架势")]
        public float MaxPosture = 100f;   // 最大架势
        public float PostureDecayRate = 20f;  // 架势自然回复速度（每秒）
        public float PostureDecayDelay = 2f;  // 停止受击多少秒后开始回复

        [Header("受击")]
        [Tooltip("Boss 非重击：硬直结束秒数。玩家 Light：受击后摇，这么久后可垫步取消；不垫则动画仍播完再回 Idle。")]
        public float StunDuration = 0.5f;

        [Tooltip("玩家 Mid 受击后摇：这么久后可垫步取消。不垫则倒地播完再 Standing。0 = 倒地期间不能垫步。")]
        public float KnockdownStunDuration = 1.2f;

        [Tooltip("玩家 Heavy 受击后摇：这么久后可垫步取消。不垫则倒地播完再 Standing。0 = 倒地期间不能垫步。")]
        public float HeavyStunDuration = 1.2f;

        [Header("受击动画（接口预留，留空 = 回退到普通受击动画）")]
        public string HurtAnim_Normal = "Hurt_Light";      // 玩家裸吃 Light（旧名 Hurt_Ground）
        public string HurtAnim_Light2 = "Hurt_Light2";     // Light 连续受击
        public string HurtAnim_Mid = "Hurt_Mid";
        public string HurtAnim_Heavy = "Hurt_Heavy";       // 强力招式（击飞/倒地），空则用 HurtAnim_Normal
        public string HurtAnim_HeavyRepeat = "Hurt_HeavyRepeat";
        public string HurtAnim_Standing = "Standing";      // Mid/Heavy 倒地后起身
        public string HurtAnim_MidToGuard = "MidToGuard";
        public string HurtAnim_Guard = "Hurt_Guard";       // 格挡轻攻击，空则用 HurtAnim_Normal
        public string HurtAnim_GuardHeavy = "Hurt_GuardHeavy"; // 格挡重攻击（Knockback>0），空则用 HurtAnim_Guard
        public string HurtAnim_Deflected = "Deflected";    // 被完美弹反后的硬直，空则用 HurtAnim_Normal
        public string HurtAnim_Broken = "Stagger_Broken";  // 架势崩解倒地（占位名）；箭 Heavy 普通格挡也播这段

        [Tooltip("相对 Hurt_Mid 动画 0 点，秒。超过则不能 MidToGuard，进入躺地。")]
        public float HurtMidFallEndTime = 0.4f;

        [Tooltip("相对 Hurt_Heavy 动画 0 点，秒。超过才算躺地，Boss 可 Jump_Danger。Hurt_HeavyRepeat 已在躺地，不走此参数。")]
        public float HurtHeavyFallEndTime = 0.5f;

        [Tooltip("MidToGuard 动画开始后多久允许提前格挡/垫步。0 = 必须播完再行动。")]
        public float MidToGuardDeflectDodgeOpenTime = 0f;

        [Header("防御/弹反（M4）")]
        public float DeflectWindow = 0.3f;             // 完美弹反窗口（秒）
        public float DeflectPostureGain = 30f;         // 完美弹反成功：攻击者涨的架势
        public float GuardPostureFactor = 0.5f;        // 格挡时自己涨架势的比例（×对方架势伤害）
        public float ParriedDuration = 1.0f;          // 被完美弹反后的最小硬直（下限，动画播完仍保底）。M7 攻防转换：给弹反成功方稳定反击窗口，回合制才成立
        public float ParryCounterHitDelay = 0.1f;     // 被动完美弹反：以被弹方预计恢复 + 该值为反击命中目标；按首段 HitStartTime 倒推起手。长前摇/多段招不保证抢先
        [Min(0f), Tooltip("Boss 被玩家普通近战弹断后，估算玩家收刀与输入耗时；与玩家 LightAttack 首个有效判定开始时间相加。不是玩家实际输入计时，0.7 秒来自当前手动采样。")]
        public float PlayerParryAttackPreparationTime = 0.7f;
        [Min(0f), Tooltip("Boss 下一次攻击最早判定比玩家标准第一刀晚多少秒；只限制攻击，不限制硬直恢复后的格挡和移动。")]
        public float PlayerParryAttackHitBuffer = 0.1f;
        // 空按惩罚（主动弹反重构）：一次真实按下 = 一次尝试，只有"实际开启窗口但没弹中"才缩窗。
        // 旧的 DeflectMashLimit / DeflectMashWindow / DeflectMashPenalty 是按次数记账，
        // 会把还没得到结果的按下直接算成失败，已按"有效尝试的结果"改写为下面三个字段。
        [Tooltip("前几次连续空按不缩窗。宽容次数内连按不吃惩罚，准确连弹与乱按才区分得开")]
        public int DeflectEmptyGraceCount = 3;
        [Tooltip("超出宽容次数后，每个空按把下一次窗口乘上该系数，取值 (0, 1]")]
        [FormerlySerializedAs("DeflectMashPenalty")]
        public float DeflectEmptyPenalty = 0.75f;
        public float DeflectWindowMin = 0.1f;          // 弹反窗口下限
        [Tooltip("相邻两次实际开启弹反窗口的间隔超过该值就清除空按累计，窗口恢复正常")]
        [FormerlySerializedAs("DeflectMashWindow")]
        public float DeflectEmptyResetInterval = 0.5f;

        public float GuardPostureRecoveryMultiplier = 5f; // 按住格挡 2s 后架势回复倍率

        [Header("架势（M9）")]
        public bool PostureDecayInverse = false;   // true=非线性（架势越高回越慢，Boss 用）；false=线性

        [Tooltip("Stagger_Broken 动画开始后多久允许提前取消硬直：可切入 DeflectState（格挡/抬刀）或 DodgeState（垫步）。0 = 动画播完才能行动。")]
        public float BrokenDeflectDodgeOpenTime = 0f;

        [Tooltip("箭 Heavy 格挡（Stagger_Broken）或完美弹反（Deflect_HeavyArrow）动画开始后，多久允许格挡/垫步提前结束。0 = 必须播完。")]
        public float ArrowHeavyDeflectDodgeOpenTime = 0f;

        [Header("命数（Boss 用，一阶段 2 条命）")]
        public int LifeCount = 1;      // 总命数（玩家填 1，Boss 填 2）

        [Header("闪避")]
        public float DodgeDuration = 0.5f; // 垫步持续时长（位移由动画根运动驱动，这里只控制总时长）
        public float DodgeIFrame = 0.3f;  // 垫步无敌帧时长（M4）

        [Tooltip("玩家 Mid/Heavy 倒地取消到闪避时的固定融合秒数。0 = 沿用普通闪避进入策略。")]
        [Range(0f, 0.25f)]
        public float KnockdownToDodgeBlendDuration = 0.12f;

        [Header("识破（M17）")]
        public float MikiriDuration = 0.8f;      // 踩刀动画时长
        public float MikiriPostureGain = 30f;    // 识破成功涨攻击者架势

        [Header("Boss 被动防御（M7 攻防转换）")]
        [Tooltip("Boss 被动格挡的连续计数阈值：连续格挡满该次数后，下一次受击升级为完美弹反。2 = 第 3 刀必弹反。0 = 每次被动防御都是完美弹反")]
        public int PassiveDeflectThreshold = 2;
        [Tooltip("放下防御 / 停止被压制这么久（秒）后，清零被动格挡连续计数")]
        public float PassiveDeflectResetWindow = 2.5f;

        [Header("移动")]
        // 移动速度由动画 Root 曲线决定（全权根运动），这里只留转身速度
        public float RotationSpeed = 720f;

        [Header("跳跃")]
        [Tooltip("false = 不进 AirState、不接跳跃（Boss 用）")]
        public bool UseAirState = true;

        // 只狼跳跃高度写在 TAE 里，hkx 的 Root Y 几乎不离地，所以用初速度补高度
        public float JumpSpeed = 6f;

        [Header("跳跃动画时机（代码切 Jump / Jumping / Fall）")]
        [Tooltip("Jump 播到这个归一化时间就切 Jumping。起跳还在播人已经在空中 → 调小（0.4~0.7）")]
        [Range(0.1f, 1f)]
        public float JumpToJumpingNormalized = 0.55f;

        [Tooltip("Jump2 切 Jumping 的归一化时间。比 Jump 略早，避免二段踢僵在半空")]
        [Range(0.1f, 1f)]
        public float Jump2ToJumpingNormalized = 0.42f;

        [Tooltip("过最高点（速度转负）也立刻切 Jumping。起跳太长、空中还在抬腿时勾上")]
        public bool SwitchJumpingAtApex = true;

        [Tooltip("离地还有这么高就开始播 Fall。落地动作含下坠后半段 → 加大（0.2~0.6）；Fall 只是触地缓冲 → 填 0")]
        public float LandEarlyHeight = 0.35f;

        [Tooltip("Jump ↔ Jumping 融合秒数")]
        public float JumpAnimBlend = 0.08f;

        [Tooltip("切到 Fall 的融合秒数")]
        public float LandAnimBlend = 0.05f;

        [Tooltip("下落速度超过该值且近地时提前切 Fall（更顺，0 = 只靠 LandEarlyHeight / 接地）")]
        public float FallStartDownSpeed = 1.5f;

        [Header("攻击输入")]
        public float AttackHoldDuration = 0.3f; // 按住攻击达到该时长后自动触发突刺

        [Header("葫芦/复活（玩家专属，Boss 用不到）")]
        public int GourdCount = 10;       // 初始葫芦次数
        public int ReviveCount = 1;       // 复活次数

        [Header("喝药惩罚（Boss）")]
        [Tooltip("检测到玩家喝药后，延迟这么久才开始播 Bow_Heavy。Boss 正在放招时等招打完再射，不打断。箭大约再过招式表出箭点（约 1.5s）飞出。调大可让葫芦播完后仍来得及弹反。0 = 空闲时立刻出招。")]
        public float HealPunishDelay = 0.5f;
    }

}
