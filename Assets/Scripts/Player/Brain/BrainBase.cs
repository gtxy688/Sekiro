using UnityEngine;

using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
namespace ARPG.Player
{

    // 保存一条离散动作预输入，在有效期内持续尝试交给角色状态机。
    [RequireComponent(typeof(CharacterBody))]
    public abstract class BrainBase : MonoBehaviour
    {
        protected CharacterBody body;

        // 单槽缓冲：新的动作会覆盖尚未执行的旧动作。
        private ICommand bufferedCommand;
        private float bufferTimer;

        protected virtual void Awake()
        {
            body = GetComponent<CharacterBody>();
        }

        protected virtual void Update()
        {
            ProcessCommandBuffer();
        }

        // 记录最新动作；duration 是状态机暂时拒收时允许等待的时长。
        protected void BufferCommand(ICommand cmd, float duration = 0.2f)
        {
            bufferedCommand = cmd;
            bufferTimer = duration;
        }

        // 自动回防前检查缓冲，避免覆盖玩家主动按下的防御命令。
        protected bool HasBufferedCommand<T>() where T : ICommand
        {
            return bufferedCommand is T;
        }

        // 中断操作时，只丢弃指定类型的待执行动作。
        protected void DropBufferedCommand<T>() where T : ICommand
        {
            if (bufferedCommand is not T) return;

            bufferedCommand = null;
        }

        private void ProcessCommandBuffer()
        {
            if (bufferedCommand == null) return;

            // 状态机决定当前能否接受动作；接受后立即清空缓冲。
            bool isConsumed = body.TryExecuteCommand(bufferedCommand);

            if (isConsumed)
            {
                bufferedCommand = null;
            }
            else
            {
                // 暂时拒收时继续等待，超时则丢弃本次预输入。
                bufferTimer -= Time.deltaTime;
            
                if (bufferTimer <= 0)
                {
                    bufferedCommand = null;
                }
            }
        }
    }
}
