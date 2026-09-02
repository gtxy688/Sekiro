using ARPG.FrameWork.Body;
namespace ARPG.UI
{
    // 需要"钉在某个角色身上"的 View 的统一入口（危 / 治 / 回生三个汉字 + 锁定点）。
    //
    // 存在的唯一理由：CombatUIController 原来给四个跟随型 View 各写了一个 Bind 方法，
    // 四段逐行相同，只差类型名和物体名。要收成一个泛型方法，
    // 就得让它们在类型系统里有个公共上界——UIView 太宽（大部分 View 不跟随），
    // 所以补一个只有 BindFollowTarget 的窄接口。
    //
    // 契约：target 为空时保持原样，不清掉已绑好的目标。
    // 调用方常常在"还不知道目标是谁"的时机调一次，清空会让后续自动兜底也失效。
    public interface IFollowTargetView
    {
        void BindFollowTarget(CharacterBody target);
    }
}
