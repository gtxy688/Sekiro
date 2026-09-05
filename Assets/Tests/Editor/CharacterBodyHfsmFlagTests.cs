using System.Reflection;

using NUnit.Framework;
using UnityEngine;

using ARPG.FrameWork.Body;
using ARPG.FrameWork.States.Base;

namespace ARPG.Tests.Editor
{
    public sealed class CharacterBodyHfsmFlagTests
    {
        private GameObject bodyObject;
        private CharacterBody body;

        [SetUp]
        public void SetUp()
        {
            bodyObject = new GameObject("CharacterBodyHfsmFlagTests");
            bodyObject.AddComponent<Animator>();
            bodyObject.AddComponent<Rigidbody>();
            body = bodyObject.AddComponent<CharacterBody>();

            InvokePrivateAwake(body);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(bodyObject);
        }

        [Test]
        public void IsAttacking_is_true_for_the_current_attack_leaf()
        {
            body.MainStateMachine.ChangeState(new ProbeAttackState(body));

            Assert.That(body.IsAttacking, Is.True);
        }

        [Test]
        public void IsAttacking_remains_true_while_the_current_attack_leaf_exits()
        {
            var attack = new ProbeAttackState(body);
            body.MainStateMachine.ChangeState(attack);

            body.MainStateMachine.ChangeState(new PassiveState(body));

            Assert.That(attack.IsAttackingObservedDuringExit, Is.True);
            Assert.That(body.IsAttacking, Is.False);
        }

        [Test]
        public void IsAttacking_is_not_externally_writable()
        {
            PropertyInfo property = typeof(CharacterBody).GetProperty(nameof(CharacterBody.IsAttacking));

            Assert.That(property, Is.Not.Null);
            Assert.That(property.CanWrite, Is.False);
        }

        private static void InvokePrivateAwake(CharacterBody target)
        {
            MethodInfo awake = typeof(CharacterBody).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(target, null);
        }

        private sealed class ProbeAttackState : AttackStateBase
        {
            public bool IsAttackingObservedDuringExit { get; private set; }

            public ProbeAttackState(CharacterBody body) : base(body, null, null)
            {
            }

            // Deliberately bypasses AttackStateBase.OnEnter: the test must prove
            // IsAttacking comes from the current leaf, not an OnEnter side effect.
            public override void OnEnter()
            {
            }

            public override void OnExit()
            {
                base.OnExit();
                IsAttackingObservedDuringExit = body.IsAttacking;
            }

            protected override void ExitToIdle()
            {
            }

            protected override BaseState NewSelf(ARPG.Configs.AttackConfig next)
            {
                return new ProbeAttackState(body);
            }
        }

        private sealed class PassiveState : BaseState
        {
            public PassiveState(CharacterBody body) : base(body)
            {
            }
        }
    }
}
