using UnityEngine;

using ARPG.FrameWork.States.Air;
using ARPG.FrameWork.States.Ground;
namespace ARPG.Configs
{

    // 角色战斗属性配置（SO）：玩家和 Boss 共用 CharacterBody，数值各自配置一份
    // 战斗数值全部走 SO，禁止在 .cs 里硬编码
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
        public float ParryCounterHitDelay = 0.1f;     // 完美弹反后，反击命中时刻 = 被弹方硬直结束 + 该值（推荐 0~0.15）。命中早于玩家攻击前摇 → 贪刀必被罚；发起时刻由代码按反击招 HitStartTime 倒推
        public float DeflectMashLimit = 3;             // 抖刀惩罚：0.5s 内连点次数阈值
        public float DeflectMashWindow = 0.5f;         // 抖刀判定窗口
        public float DeflectMashPenalty = 0.75f;       // 每次超限惩罚系数
        public float DeflectWindowMin = 0.1f;          // 弹反窗口下限
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
