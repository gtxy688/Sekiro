using System;
using UnityEngine;

// 单段判定窗，字段与 AttackConfig 同义，烘焙时拷过去。
// 无判定段：hitStartTime == recoverStart == comboWindowEnd == stateDuration。
[Serializable]
public class BossMoveWindow
{
    public float hitStartTime = 0.2f;
    public float recoverStart = 0.8f;
    public float comboWindowEnd = 0.9f;
    public float stateDuration = 1.8f;
    public float rotateEnd = 0.3f;
    public float transitionDuration = 0.1f;

    [Tooltip("一条 Clip 内多次出伤。空 = 只用 hitStartTime/recoverStart 一刀")]
    public HitPulse[] hitPulses;

    [Tooltip("相对本段动画 0 点。clip 为空则跳过")]
    public AttackSfxCue[] sfxCues;
}
