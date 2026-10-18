using System;

namespace ARPG.Boss
{

    // 一套要按顺序播的 Animator 短名。多套时出招均匀随机选一套。
    // windows 为空则沿用招式行的窗口；需要独立判定或事件点时由分支提供。
    [Serializable]
    public class BossAnimSequence
    {
        public string[] states;
        public BossMoveWindow[] windows;
    }

}
