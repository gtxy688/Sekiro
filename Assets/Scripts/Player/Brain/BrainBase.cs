using UnityEngine;

using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
namespace ARPG.Player
{

    // 处理指令预输入
    [RequireComponent(typeof(CharacterBody))]
    public abstract class BrainBase : MonoBehaviour
    {
        protected CharacterBody body;

        // 指令缓冲池
        private ICommand bufferedCommand;
        private float bufferTimer;

        protected virtual void Awake()
        {
            body = GetComponent<CharacterBody>();
        }

        protected virtual void Update()
        {
            // 每帧检查缓冲池
            ProcessCommandBuffer();
        }

        // 供子类调用的缓冲方法
        // duration 就是《只狼》里的预输入窗口期，动作游戏通常设为 0.2 秒左右
        protected void BufferCommand(ICommand cmd, float duration = 0.2f)
        {
            if (body.EnableStateDebugLog)
            {
                Debug.Log(
                    $"[CombatTiming][CommandQueued] t={Time.time:F4}s frame={Time.frameCount} {body.name} " +
                    $"指令={cmd?.GetType().Name ?? "<none>"}，" +
                    $"被覆盖指令={bufferedCommand?.GetType().Name ?? "<none>"}，" +
                    $"缓冲时长={duration:F3}s，状态={body.CurrentStatePath}", body);
            }
            bufferedCommand = cmd;
            bufferTimer = duration;
        }

        // 缓冲池里是否已有指定类型的指令。防御用它避免自动回防请求顶掉玩家真实按下。
        protected bool HasBufferedCommand<T>() where T : ICommand
        {
            return bufferedCommand is T;
        }

        // 定向清理：输入被暂停 / 结算 / 生命周期阻断时，丢掉尚未消费的指定指令。
        // 通用单槽缓冲保持不变，只有清理需要按类型定位——不为此把整套命令系统改成队列。
        protected void DropBufferedCommand<T>() where T : ICommand
        {
            if (bufferedCommand is not T) return;

            if (body != null && body.EnableStateDebugLog)
            {
                Debug.Log(
                    $"[CombatTiming][CommandDropped] t={Time.time:F4}s frame={Time.frameCount} {body.name} " +
                    $"指令={typeof(T).Name}，状态={body.CurrentStatePath}", body);
            }
            bufferedCommand = null;
        }

        private void ProcessCommandBuffer()
        {
            // 如果缓冲池里没东西，直接跳过
            if (bufferedCommand == null) return;

            // 尝试把指令塞给身体
            // 还记得我们之前在 CharacterBody 里写的 TryExecuteCommand 返回的 bool 吗？
            bool debugTiming = body.EnableStateDebugLog;
            string commandName = debugTiming ? bufferedCommand.GetType().Name : null;
            string stateBefore = debugTiming ? body.CurrentStatePath : null;
            bool isConsumed = body.TryExecuteCommand(bufferedCommand);

            if (isConsumed)
            {
                if (debugTiming)
                {
                    Debug.Log(
                        $"[CombatTiming][CommandConsumed] t={Time.time:F4}s frame={Time.frameCount} {body.name} " +
                        $"指令={commandName}，状态前={stateBefore}，状态后={body.CurrentStatePath}", body);
                }
                // 指令被成功消耗（状态机切了状态，或者放行了指令），清空缓冲池
                bufferedCommand = null;
            }
            else
            {
                // 指令被拒收（比如正在大硬直中），继续留在缓冲池里等待
                bufferTimer -= Time.deltaTime;
            
                // 如果超时了还没执行出去，就丢弃掉
                if (bufferTimer <= 0)
                {
                    if (debugTiming)
                    {
                        Debug.Log(
                            $"[CombatTiming][CommandExpired] t={Time.time:F4}s frame={Time.frameCount} {body.name} " +
                            $"指令={commandName}，状态={body.CurrentStatePath}", body);
                    }
                    bufferedCommand = null;
                }
            }
        }
    }
}
