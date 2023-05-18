using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace SFramework.Threading.Tasks.CompilerServices
{
    [StructLayout(LayoutKind.Auto)]
    public struct AsyncSTaskMethodBuilder
    {
        IStateMachineRunnerPromise runnerPromise;
        Exception ex;

        /// <summary>
        /// 1.Static Create Method
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncSTaskMethodBuilder Create()
        {
            return default;
        }

        /// <summary>
        /// 2.task-like 对象属性；Start 方法返回后将访问 builder.Task
        /// </summary>
        public STask Task
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (this.runnerPromise != null)
                {
                    return this.runnerPromise.Task;
                }
                else if (this.ex != null)
                {
                    return STask.FromException(this.ex);
                }
                else
                {
                    return STask.CompletedTask;
                }
            }
        }

        /// <summary>
        /// 3. SetException；发生异常后将调用该方法
        /// </summary>
        /// <param name="exception"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetException(Exception exception)
        {
            if (this.runnerPromise == null)
            {
                this.ex = exception;
            }
            else
            {
                this.runnerPromise.SetException(exception);
            }
        }

        /// <summary>
        /// 4. SetResult；状态机任务执行完毕后调用
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetResult()
        {
            if (this.runnerPromise != null)
            {
                this.runnerPromise.SetResult();
            }
        }

        /// <summary>
        /// 5. AwaitOnCompleted
        /// </summary>
        /// <typeparam name="TAwaiter"></typeparam>
        /// <typeparam name="TStateMachine"></typeparam>
        /// <param name="awaiter"></param>
        /// <param name="stateMachine"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitOnCompleted<TAwaiter,TStateMachine>(ref TAwaiter awaiter,ref TStateMachine stateMachine)
            where TAwaiter:INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            if (this.runnerPromise == null)
            {
                AsyncSTask<TStateMachine>.SetStateMachine(ref stateMachine, ref this.runnerPromise);
            }

            awaiter.OnCompleted(this.runnerPromise.MoveNext);//awaiter任务执行完毕，推动状态机运行
        }

        /// <summary>
        /// 6. AwaitUnsafeOnCompleted；
        /// 状态机遇到 await 关键字后，通过 GetAwaiter() 获取Awaiter，若其 IsCompleted 为 false，调用该方法注册 Awaiter 任务结束后的回调（builder.MoveNext）
        /// 至于调用 AwaitUnsafeOnCompleted 还是 AwaitOnCpmpleted，由 Awaiter 实现的接口决定
        /// </summary>
        /// <typeparam name="TAwaiter"></typeparam>
        /// <typeparam name="TStateMachine"></typeparam>
        /// <param name="awaiter"></param>
        /// <param name="stateMachine"></param>
        [SecuritySafeCritical]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitUnsafeOnCompleted<TAwaiter,TStateMachine>(ref TAwaiter awaiter,ref TStateMachine stateMachine)
            where TAwaiter:ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            if (this.runnerPromise == null)
            {
                AsyncSTask<TStateMachine>.SetStateMachine(ref stateMachine, ref this.runnerPromise);
            }

            awaiter.UnsafeOnCompleted(this.runnerPromise.MoveNext);
        }

        /// <summary>
        /// 7. Start
        /// </summary>
        /// <typeparam name="TStateMachine"></typeparam>
        /// <param name="stateMachine"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();//异步方法开始，启动状态机，一般会执行到第一个 await 关键字之前
        }

        /// <summary>
        /// 8. SetStateMachine
        /// </summary>
        /// <param name="stateMachine"></param>
        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            //由于性能问题，这里拒绝使用装箱之后的状态机，所以 nothing happened
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct AsyncSTaskMethodBuilder<T>
    {
        private IStateMachineRunnerPromise<T> runnerPromise;
        private Exception ex;
        private T result;

        // 1. Static Create method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncSTaskMethodBuilder<T> Create()
        {
            return default;
        }

        // 2. TaskLike Task property
        public STask<T> Task
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (this.runnerPromise != null)
                {
                    return this.runnerPromise.Task;
                }
                else if (this.ex != null)
                {
                    return STask.FromException<T>(this.ex);
                }
                else
                {
                    return STask.FromResult(this.result);
                }
            }
        }

        // 3. SetException
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetException(Exception exception)
        {
            if (this.runnerPromise == null)
            {
                this.ex = exception;
            }
            else
            {
                this.runnerPromise.SetException(exception);
            }
        }

        // 4. SetResult
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetResult(T result)
        {
            if(this.runnerPromise == null)
            {
                this.result = result;
            }
            else
            {
                this.runnerPromise.SetResult(result);
            }
        }

        // 5. AwaitOnCompleted
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            if (this.runnerPromise == null)
            {
                AsyncSTask<TStateMachine, T>.SetStateMachine(ref stateMachine, ref this.runnerPromise);
            }

            awaiter.OnCompleted(this.runnerPromise.MoveNext);
        }

        // 6. AwaitUnsafeOnCompleted
        [SecuritySafeCritical]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            if (this.runnerPromise == null)
            {
                AsyncSTask<TStateMachine, T>.SetStateMachine(ref stateMachine, ref this.runnerPromise);
            }

            awaiter.UnsafeOnCompleted(this.runnerPromise.MoveNext);
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
            //由于性能问题，这里拒绝使用装箱之后的状态机，所以 nothing happened
        }
    }
}