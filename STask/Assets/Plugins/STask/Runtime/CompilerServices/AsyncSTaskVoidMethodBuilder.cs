using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SFramework.Threading.Tasks.CompilerServices
{
    [StructLayout(LayoutKind.Auto)]
    public struct AsyncSTaskVoidMethodBuilder
    {
        private IStateMachineRunner runner;

        // 1. Static Create method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncSTaskVoidMethodBuilder Create()
        {
            return default;
        }

        // 2. TaskLike Task property(void)
        public STaskVoid Task
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return default;
            }
        }

        // 3. SetException
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetException(Exception exception)
        {
            if (this.runner != null)
            {
                this.runner.Return();
                this.runner = null;
            }

            STaskScheduler.PublishUnobservedTaskException(exception);//直接抛出异常
        }

        // 4. SetResult
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetResult()
        {
            if (this.runner != null)
            {
                this.runner.Return();
                this.runner = null;
            }
        }

        // 5. AwaitOnCompleted
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            if (this.runner == null)
            {
                AsyncSTaskVoid<TStateMachine>.SetStateMachine(ref stateMachine, ref this.runner);
            }

            awaiter.OnCompleted(this.runner.MoveNext);
        }

        // 6. AwaitUnsafeOnCompleted
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            if (this.runner == null)
            {
                AsyncSTaskVoid<TStateMachine>.SetStateMachine(ref stateMachine, ref this.runner);
            }

            awaiter.UnsafeOnCompleted(this.runner.MoveNext);
        }

        // 7. Start
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();
        }

        // 8. SetStateMachine
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            // don't use boxed stateMachine
        }
    }
}