using ARPG.FrameWork.Body;
namespace ARPG.UI
{

    // 跟随角色的视图统一绑定接口，供控制器按类型约束统一初始化。
    // target 为空时保留当前绑定，避免尚未确定目标的调用清空已有引用。
    public interface IFollowTargetView
    {
        void BindFollowTarget(CharacterBody target);
    }
}
