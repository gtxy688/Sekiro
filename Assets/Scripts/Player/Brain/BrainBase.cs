using UnityEngine;

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
        bufferedCommand = cmd;
        bufferTimer = duration;
    }

    private void ProcessCommandBuffer()
    {
        // 如果缓冲池里没东西，直接跳过
        if (bufferedCommand == null) return;

        // 尝试把指令塞给身体
        // 还记得我们之前在 CharacterBody 里写的 TryExecuteCommand 返回的 bool 吗？
        bool isConsumed = body.TryExecuteCommand(bufferedCommand);

        if (isConsumed)
        {
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
                bufferedCommand = null;
            }
        }
    }
}