using UnityEditor.Animations;
using UnityEngine;

public static class AttackTimelineClipFinder
{
    public static AnimationClip Find(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return null;
        return Find(animator.runtimeAnimatorController, stateName);
    }

    public static AnimationClip Find(RuntimeAnimatorController runtime, string stateName)
    {
        if (runtime == null || string.IsNullOrEmpty(stateName))
            return null;

        AnimatorOverrideController ov = runtime as AnimatorOverrideController;
        if (ov != null)
            runtime = ov.runtimeAnimatorController;

        AnimatorController ac = runtime as AnimatorController;
        if (ac == null) return null;

        int hash = Animator.StringToHash(stateName);
        for (int i = 0; i < ac.layers.Length; i++)
        {
            AnimationClip clip = FindInMachine(ac.layers[i].stateMachine, hash);
            if (clip != null) return clip;
        }
        return null;
    }

    static AnimationClip FindInMachine(AnimatorStateMachine machine, int shortNameHash)
    {
        if (machine == null) return null;

        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState st = states[i].state;
            if (st == null) continue;
            if (Animator.StringToHash(st.name) != shortNameHash) continue;
            return st.motion as AnimationClip;
        }

        ChildAnimatorStateMachine[] children = machine.stateMachines;
        for (int i = 0; i < children.Length; i++)
        {
            AnimationClip clip = FindInMachine(children[i].stateMachine, shortNameHash);
            if (clip != null) return clip;
        }
        return null;
    }
}
