using System;

// 一套要按顺序播的 Animator 短名。多套时出招均匀随机选一套。
// windows 为空则用招式行 entry.windows；落地分叉时每套自带窗口（危字/判定不能共用）。
[Serializable]
public class BossAnimSequence
{
    public string[] states;
    public BossMoveWindow[] windows;
}
