using SFramework.Threading.Tasks.Internal;
using System;
using System.Runtime.CompilerServices;

namespace SFramework.Threading.Tasks.CompilerServices
{
    /// <summary>
    /// STaskVoid 状态机执行逻辑的对象
    /// </summary>
    internal interface IStateMachineRunner
    {
        Action MoveNext { get; }
        void Return();//结束就直接 return
    }

    /// <summary>
    /// （自定义）状态机执行逻辑的对象，实现 task-like object （STask） 的最基本功能，因此都是 Set 方法
    /// </summary>
    internal interface IStateMachineRunnerPromise : ISTaskSource
    {
        Action MoveNext { get; }
        STask Task { get; }
        void SetResult();
        void SetException(Exception exception);
    }

    /// <summary>
    /// （自定义）状态机执行逻辑的对象，实现 task-like object （STask） 的最基本功能，因此都是 Set 方法，带返回值
    /// </summary>
    internal interface IStateMachineRunnerPromise<T> : ISTaskSource<T>
    {
        Action MoveNext { get; }
        STask<T> Task { get; }
        void SetResult(T result);
        void SetException(Exception exception);
    }

    internal sealed class AsyncSTaskVoid<TStateMachine> : IStateMachineRunner, ITaskPoolNode<AsyncSTaskVoid<TStateMachine>>, ISTaskSource
        where TStateMachine : IAsyncStateMachine
    {
        private TStateMachine stateMachine;

        public AsyncSTaskVoid()
        {
            this.MoveNext = this.Run;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Run()
        {
            this.stateMachine.MoveNext();
        }

        #region Pool
        private static TaskPool<AsyncSTaskVoid<TStateMachine>> pool;

        public static void SetStateMachine(ref TStateMachine stateMachine, ref IStateMachineRunner runnerFieldRef)
        {
            if (!pool.TryPop(out AsyncSTaskVoid<TStateMachine> result))
            {
                result = new AsyncSTaskVoid<TStateMachine>();
            }
            TaskTracker.TrackActiveTask(result, 3);

            runnerFieldRef = result;// set runner before copied
            result.stateMachine = stateMachine;// copy struct StateMachine(in release build)
        }

        static AsyncSTaskVoid()
        {
            TaskPool.RegisterSizeGetter(typeof(AsyncSTaskVoid<TStateMachine>), () => pool.Size);
        }

        private AsyncSTaskVoid<TStateMachine> nextNode;
        public ref AsyncSTaskVoid<TStateMachine> NextNode => ref this.nextNode;
        #endregion

        #region IStateMachineRunner
        public Action MoveNext { get; }

        public void Return()
        {
            TaskTracker.RemoveTracking(this);
            this.stateMachine = default;
            pool.TryPush(this);
        }
        #endregion

        #region ISTaskSource
        public STaskStatus GetStatus(short token)
        {
            return STaskStatus.Pending;
        }
        public STaskStatus UnsafeGetStatus()
        {
            return STaskStatus.Pending;
        }

        public void OnCompleted(Action<object> continuation, object state, short token) { }

        public void GetResult(short token) { }
        #endregion
    }

    internal sealed class AsyncSTask<TStateMachine> : IStateMachineRunnerPromise, ISTaskSource, ITaskPoolNode<AsyncSTask<TStateMachine>>
        where TStateMachine : IAsyncStateMachine
    {
        private TStateMachine stateMachine;
        private STaskCompletionSourceCore<AsyncUnit> core;

        private AsyncSTask()
        {
            this.MoveNext = this.Run;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Run()
        {
            this.stateMachine.MoveNext();
        }

        #region Pool
        private static TaskPool<AsyncSTask<TStateMachine>> pool;

        public static void SetStateMachine(ref TStateMachine stateMachine, ref IStateMachineRunnerPromise runnerPromiseFieldRef)
        {
            if (!pool.TryPop(out AsyncSTask<TStateMachine> result))
            {
                result = new AsyncSTask<TStateMachine>();
            }
            TaskTracker.TrackActiveTask(result, 3);
            
            runnerPromiseFieldRef = result;// set runner before copied
            result.stateMachine = stateMachine;
        }

        private AsyncSTask<TStateMachine> nextNode;
        public ref AsyncSTask<TStateMachine> NextNode => ref this.nextNode;

        static AsyncSTask()//静态构造函数，只执行一次（实例化前或引用其他静态成员前调用一次）
        {
            TaskPool.RegisterSizeGetter(typeof(AsyncSTask<TStateMachine>), () => pool.Size);
        }
        #endregion

        private void Return()
        {
            TaskTracker.RemoveTracking(this);
            this.core.Reset();
            this.stateMachine = default;
            pool.TryPush(this);
        }

        private bool TryReturn()
        {
            TaskTracker.RemoveTracking(this);
            this.core.Reset();
            this.stateMachine = default;
            return pool.TryPush(this);
        }

        #region IStateMachineRunnerPromise
        public Action MoveNext { get; }

        public STask Task
        {
            get
            {
                return new STask(this, this.core.Version);
            }
        }

        public void SetException(Exception exception)
        {
            this.core.TrySetException(exception);
        }

        public void SetResult()
        {
            this.core.TrySetResult(AsyncUnit.Default);
        }
        #endregion

        #region ISTaskSource
        public STaskStatus GetStatus(short token)
        {
            return this.core.GetStatus(token);
        }

        public void OnCompleted(Action<object> continuation, object state, short token)
        {
            this.core.OnCompleted(continuation, state, token);
        }

        public void GetResult(short token)
        {
            try
            {
                _ = this.core.GetResult(token);
            }
            finally
            {
                this.TryReturn();//reset
            }
        }

        public STaskStatus UnsafeGetStatus()
        {
            return this.core.UnsafeGetStatus();
        }
        #endregion
    }

    internal sealed class AsyncSTask<TStateMachine, T> : IStateMachineRunnerPromise<T>, ISTaskSource<T>, ITaskPoolNode<AsyncSTask<TStateMachine, T>>
        where TStateMachine : IAsyncStateMachine
    {
        private TStateMachine stateMachine;
        private STaskCompletionSourceCore<T> core;

        private AsyncSTask()
        {
            this.MoveNext = this.Run;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Run()
        {
            this.stateMachine.MoveNext();
        }


        #region Pool
        private static TaskPool<AsyncSTask<TStateMachine, T>> pool;

        public static void SetStateMachine(ref TStateMachine stateMachine, ref IStateMachineRunnerPromise<T> runnerPromiseFieldRef)
        {
            if (!pool.TryPop(out var result))
            {
                result = new AsyncSTask<TStateMachine, T>();
            }
            TaskTracker.TrackActiveTask(result, 3);

            runnerPromiseFieldRef = result; // set runner before copied.
            result.stateMachine = stateMachine; // copy struct StateMachine(in release build).
        }

        private AsyncSTask<TStateMachine, T> nextNode;
        public ref AsyncSTask<TStateMachine, T> NextNode => ref this.nextNode;

        static AsyncSTask()//静态构造函数，只执行一次（实例化前或引用其他静态成员前调用一次）
        {
            TaskPool.RegisterSizeGetter(typeof(AsyncSTask<TStateMachine, T>), () => pool.Size);
        }
        #endregion

        private void Return()
        {
            TaskTracker.RemoveTracking(this);
            this.core.Reset();
            this.stateMachine = default;
            pool.TryPush(this);
        }

        private bool TryReturn()
        {
            TaskTracker.RemoveTracking(this);
            this.core.Reset();
            this.stateMachine = default;
            return pool.TryPush(this);
        }

        #region IStateMachineRunnerPromise<T>
        public Action MoveNext { get; }

        public STask<T> Task
        {
            get
            {
                return new STask<T>(this, this.core.Version);
            }
        }

        public void SetException(Exception exception)
        {
            this.core.TrySetException(exception);
        }

        public void SetResult(T result)
        {
            this.core.TrySetResult(result);
        }
        #endregion

        #region ISTaskSource<T>
        public T GetResult(short token)
        {
            try
            {
                return this.core.GetResult(token);
            }
            finally
            {
                this.TryReturn();
            }
        }

        void ISTaskSource.GetResult(short token)
        {
            this.GetResult(token);
        }

        public STaskStatus GetStatus(short token)
        {
            return this.core.GetStatus(token);
        }

        public STaskStatus UnsafeGetStatus()
        {
            return this.core.UnsafeGetStatus();
        }

        public void OnCompleted(Action<object> continuation, object state, short token)
        {
            this.core.OnCompleted(continuation, state, token);
        }
        #endregion
    }
}
