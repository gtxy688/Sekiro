using NUnit.Framework;
using Sekiro.Core.StateMachine;
using System;
using System.Collections.Generic;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// StateMachine 基础框架 EditMode 单元测试。
    /// 覆盖：添加状态、状态转换、生命周期回调、Update 执行、状态机引用访问、异常处理。
    /// </summary>
    [TestFixture]
    public class StateMachineTest
    {
        #region Test Helper States

        /// <summary>
        /// 记录生命周期调用次数的测试用状态
        /// </summary>
        private class TestStateA : State
        {
            public int EnterCount;
            public int ExecuteCount;
            public int ExitCount;

            public override void Enter() { EnterCount++; }
            public override void Execute() { ExecuteCount++; }
            public override void Exit() { ExitCount++; }
        }

        /// <summary>
        /// 第二个测试用状态，用于验证状态转换
        /// </summary>
        private class TestStateB : State
        {
            public int EnterCount;
            public int ExecuteCount;
            public int ExitCount;

            public override void Enter() { EnterCount++; }
            public override void Execute() { ExecuteCount++; }
            public override void Exit() { ExitCount++; }
        }

        /// <summary>
        /// 记录所属 StateMachine 引用的测试用状态
        /// </summary>
        private class StateAccessTestState : State
        {
            public StateMachine CapturedStateMachine;

            public override void Enter()
            {
                CapturedStateMachine = _stateMachine;
            }
        }

        #endregion

        #region Tests

        /// <summary>
        /// 测试用例 1：StateMachine 可以添加状态
        /// </summary>
        [Test]
        public void AddState_StateAddedSuccessfully_ReturnsStateInstance()
        {
            // Arrange
            var sm = new StateMachine();

            // Act
            var state = sm.AddState<TestStateA>();

            // Assert
            Assert.IsNotNull(state);
            Assert.IsInstanceOf<TestStateA>(state);
        }

        /// <summary>
        /// 测试用例 2：StateMachine 可以转换到指定状态
        /// </summary>
        [Test]
        public void TransitionTo_ValidState_CurrentStateChanges()
        {
            // Arrange
            var sm = new StateMachine();
            sm.AddState<TestStateA>();
            sm.AddState<TestStateB>();

            // Act
            sm.TransitionTo<TestStateA>();

            // Assert
            Assert.IsInstanceOf<TestStateA>(sm.GetCurrentState());

            // Act again
            sm.TransitionTo<TestStateB>();

            // Assert
            Assert.IsInstanceOf<TestStateB>(sm.GetCurrentState());
        }

        /// <summary>
        /// 测试用例 3：状态转换时调用旧状态的 Exit 和新状态的 Enter
        /// </summary>
        [Test]
        public void TransitionTo_CallsExitOnOldStateAndEnterOnNewState()
        {
            // Arrange
            var sm = new StateMachine();
            var stateA = sm.AddState<TestStateA>();
            var stateB = sm.AddState<TestStateB>();

            // Act - first transition (no previous state, Exit should not be called)
            sm.TransitionTo<TestStateA>();

            // Assert
            Assert.AreEqual(1, stateA.EnterCount, "First Enter should be called once");
            Assert.AreEqual(0, stateA.ExitCount, "No previous state, Exit should not be called");

            // Act - second transition
            sm.TransitionTo<TestStateB>();

            // Assert
            Assert.AreEqual(1, stateA.ExitCount, "Old state Exit should be called once");
            Assert.AreEqual(1, stateB.EnterCount, "New state Enter should be called once");
        }

        /// <summary>
        /// 测试用例 4：Update 调用当前状态的 Execute
        /// </summary>
        [Test]
        public void Update_CallsExecuteOnCurrentState()
        {
            // Arrange
            var sm = new StateMachine();
            var state = sm.AddState<TestStateA>();
            sm.TransitionTo<TestStateA>();

            // Act
            sm.Update();
            sm.Update();
            sm.Update();

            // Assert
            Assert.AreEqual(3, state.ExecuteCount, "Execute should be called 3 times");
        }

        /// <summary>
        /// 测试用例 5：状态可以访问 StateMachine 引用
        /// </summary>
        [Test]
        public void State_CanAccessStateMachineReference()
        {
            // Arrange
            var sm = new StateMachine();
            var state = sm.AddState<StateAccessTestState>();

            // Act
            sm.TransitionTo<StateAccessTestState>();

            // Assert
            Assert.IsNotNull(state.CapturedStateMachine,
                "State should have access to StateMachine reference");
            Assert.AreSame(sm, state.CapturedStateMachine,
                "State's StateMachine reference should be the same instance");
        }

        /// <summary>
        /// 测试用例 6：转换到不存在的状态会抛出异常
        /// </summary>
        [Test]
        public void TransitionTo_NonExistentState_ThrowsException()
        {
            // Arrange
            var sm = new StateMachine();

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() =>
            {
                sm.TransitionTo<TestStateA>();
            });
        }

        #endregion
    }
}
