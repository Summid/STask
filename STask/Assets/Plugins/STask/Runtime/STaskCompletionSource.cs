using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace SFramework.Threading.Tasks
{
    public interface IResolvePromise
    {
        bool TrySetResult();
    }

    public interface IResolvePromise<T>
    {
        bool TrySetResult(T value);
    }

    public interface IRejectPromise
    {
        bool TrySetException(Exception exception);
    }

    public interface ICancelPromise
    {
        bool TrySetCanceled(CancellationToken cancellationToken = default);
    }

    public interface IPromise : IResolvePromise, IRejectPromise, ICancelPromise
    {
    }

    public interface IPromise<T> : IResolvePromise<T>, IRejectPromise, ICancelPromise
    {
    }

    internal class ExceptionHolder
    {
        private ExceptionDispatchInfo exception;
        private bool calledGet = false;

        public ExceptionHolder(ExceptionDispatchInfo exception)
        {
            this.exception = exception;
        }

        public ExceptionDispatchInfo GetException()
        {
            if (!this.calledGet)
            {
                this.calledGet = true;
                GC.SuppressFinalize(this);//通知GC不再调用析构函数，防止在Finalize线程调用析构函数之后，GC再重复调用
            }
            return this.exception;
        }

        ~ExceptionHolder()
        {
            if (!this.calledGet)//未处理异常，抛出来
            {
                STaskScheduler.PublishUnobservedTaskException(this.exception.SourceException);
            }
        }
    }

    //与通用逻辑分开，避免不必要的拷贝
    internal static class STaskCompletionSourceCoreShared
    {
        internal static readonly Action<object> s_sentinel = CompletionSentinel;

        private static void CompletionSentinel(object _)
        {
            throw new InvalidOperationException("The sentinel delegate should never be invoked.");
        }
    }

    /// <summary>
    /// 实现接口 <see cref="ISTaskSource"/> 的核心逻辑；
    /// </summary>
    /// <remarks>
    /// 类似 ManualResetValueTaskSourceCore 实现 IValueTaskSource 的核心逻辑，与之不同的是这里不处理执行上下文；
    /// 实现 <see cref="ISTaskSource"/> 接口的类底层还需通过 <see cref="STaskCompletionSourceCore{TResult}"/> 来处理统一逻辑（主要是token，因为token保证其可复用）；
    /// 为什么要复用：解决当异步方法同步完成时仍有内存开销的问题；
    /// token 与这里的 <see cref="version"/> 是同一个意思
    /// </remarks>
    /// <typeparam name="TResult"></typeparam>
    /// <seealso href="https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/shared/System/Threading/Tasks/Sources/ManualResetValueTaskSourceCore.cs"/>参考
    [StructLayout(LayoutKind.Auto)]
    public struct STaskCompletionSourceCore<TResult>
    {
        //Struct Size: TResult + (8 + 2 + 1 + 1 + 8 + 8)
        
        private TResult result;
        private object error;//ExceptionHolder 或 OperationCanceledException
        private short version;
        private bool hasUnhandledError;
        private int completedCount;//0: completed == false
        private Action<object> continuation;
        private object continuationState;

        public void Reset()
        {
            this.ReportUnhandledError();

            unchecked
            {
                this.version += 1;//自增version
            }
            this.completedCount = 0;
            this.result = default;
            this.error = null;
            this.hasUnhandledError = false;
            this.continuation = null;
            this.continuationState = null;
        }

        private void ReportUnhandledError()
        {
            if (this.hasUnhandledError)
            {
                try
                {
                    if (this.error is OperationCanceledException oc)
                    {
                        STaskScheduler.PublishUnobservedTaskException(oc);
                    }
                    else if (this.error is ExceptionHolder e)
                    {
                        STaskScheduler.PublishUnobservedTaskException(e.GetException().SourceException);
                    }
                }
                catch { }
            }
        }

        internal void MarkHandled()
        {
            this.hasUnhandledError = false;
        }

        /// <summary>
        /// 成功执行完毕
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool TrySetResult(TResult result)
        {
            //completedCount is 0 before invoke Increment Method，只有未被使用过的才能设置 result，即只能设置一次结果（不论是 succussful/exception/canceled）
            if (Interlocked.Increment(ref this.completedCount) == 1)
            {
                this.result = result;

                //if continuation is not null(has set in OnCompleted correctly), invoke continuation,
                //if continuation is null, set continuation to s_sentinel in order to avoid invoke it
                if (this.continuation != null || Interlocked.CompareExchange(ref this.continuation, STaskCompletionSourceCoreShared.s_sentinel, null) != null)
                {
                    this.continuation(this.continuationState);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 执行中遇到异常
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public bool TrySetException(Exception error)
        {
            if (Interlocked.Increment(ref this.completedCount) == 1)
            {
                this.hasUnhandledError = true;
                if (this.error is OperationCanceledException)
                {
                    this.error = error;
                }
                else
                {
                    this.error = new ExceptionHolder(ExceptionDispatchInfo.Capture(error));
                }

                if (this.continuation != null || Interlocked.CompareExchange(ref this.continuation, STaskCompletionSourceCoreShared.s_sentinel, null) != null)
                {
                    this.continuation(this.continuationState);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 执行时被取消
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public bool TrySetCanceled(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref this.completedCount) == 1)
            {
                this.hasUnhandledError = true;
                this.error = new OperationCanceledException(cancellationToken);

                if (this.continuation != null || Interlocked.CompareExchange(ref this.continuation, STaskCompletionSourceCoreShared.s_sentinel, null) != null)
                {
                    this.continuation(this.continuationState);
                    return true;
                }
            }

            return false;
        }

        /// <summary> 获取当前操作的 Version(token) </summary>
        public short Version => this.version;


        /// <summary>
        /// 获取执行状态
        /// </summary>
        /// <param name="token"> 执行<see cref="STask"/>构造方法时传递的值 </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public STaskStatus GetStatus(short token)
        {
            this.ValidateToken(token);
            return (this.continuation == null || (this.completedCount == 0)) ? STaskStatus.Pending
                : (this.error == null) ? STaskStatus.Succeeded
                : (this.error is OperationCanceledException) ? STaskStatus.Canceled
                : STaskStatus.Faulted;
        }

        /// <summary>
        /// 不检查 token 是否合法，获取执行状态
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public STaskStatus UnsafeGetStatus()
        {
            return (this.continuation == null || (this.completedCount == 0)) ? STaskStatus.Pending
                : (this.error == null) ? STaskStatus.Succeeded
                : (this.error is OperationCanceledException) ? STaskStatus.Canceled
                : STaskStatus.Faulted;
        }

        /// <summary>
        /// 获取执行结果
        /// </summary>
        /// <param name="token"> 执行<see cref="STask"/>构造方法时传递的值 </param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public TResult GetResult(short token)
        {
            this.ValidateToken(token);
            if (this.completedCount == 0)
            {
                throw new InvalidOperationException("Not yet completed, STask only allow to use await.");
            }

            if(this.error != null)
            {
                this.hasUnhandledError = false;
                if (this.error is OperationCanceledException oce)
                {
                    throw oce;
                }
                else if (this.error is ExceptionHolder eh)
                {
                    eh.GetException().Throw();
                }

                throw new InvalidOperationException("Critical: invalid exception type was held.");
            }

            return this.result;
        }

        /// <summary>
        /// 调度 continuation
        /// </summary>
        /// <param name="continuation">当执行完毕后，被调用的回调</param>
        /// <param name="state">回调的参数，真正的 STask 的 continuation（普通情况下），具体查看 STask.Awaiter.OnCompleted()</param>
        /// <param name="token"> 执行<see cref="STask"/>构造方法时传递的值 </param>
        public void OnCompleted(Action<object> continuation,object state,short token /*, ValueTaskSourceOnCompletedFlags flags */)
        {
            if (continuation == null)
            {
                throw new ArgumentNullException(nameof(continuation));
            }
            this.ValidateToken(token);

            //不使用 ValueTaskSourceOnCompletedFlags 形参，不处理执行上下文或同步上下文

            //异步方法执行情况：
            //PatternA: GetStatus = Pending => OnCompleted => TrhSet*** => GetResult
            //PatternB: TrySet*** => GetStatus = !Pending => GetResult   此时 this.continuation 为
            //PatternC: GetStatus = Pending => OnCompleted/TrySet (race condition) => GetResult
            //C.1 win OnCompleted => TrySet invoke saved continuation
            //C.2 win TrySet => should invoke continuation here

            //重点在第三种情况
            //若先执行的是 OnCompleted ，那么 continuation 会在这里保存下来，供 TrySet 方法执行
            //若先执行的是 TrySet ，那么在这里执行 continuation

            object oldContinuation = this.continuation;
            if (oldContinuation == null)//还没设置 continuation
            {
                this.continuationState = state;
                oldContinuation = Interlocked.CompareExchange(ref this.continuation, continuation, null);
                //oldContinuation 仍然为 null，this.continuation 为形参 continuation
                //在 TrySet 中调用 continuation
            }

            if (oldContinuation != null)
            {
                //先执行的 TrySet，此时 oldContinuation 为 s_sentinel，调用回调
                //若 oldContinuation != s_sentinel，表示多次 await 了同一个 STask，这是不允许的
                if (!ReferenceEquals(oldContinuation, STaskCompletionSourceCoreShared.s_sentinel))
                {
                    throw new InvalidOperationException("Already continuation registered, can not await twice or get Status after await.");
                }

                continuation(state);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateToken(short token)
        {
            if (token != this.version)
            {
                throw new InvalidOperationException("Token version is not matched, can not await twice or get Status after await.");
            }
        }
    }

    /// <summary>
    /// 用于构造<see cref="STask"/>，内部由<see cref="STaskCompletionSourceCore{TResult}"/>实现，获取结果(await)后自动设置其<see cref="STaskCompletionSourceCore{TResult}.Version"/>
    /// </summary>
    public class AutoResetSTaskCompletionSource : ISTaskSource, ITaskPoolNode<AutoResetSTaskCompletionSource>, IPromise
    {
        static TaskPool<AutoResetSTaskCompletionSource> pool;
        AutoResetSTaskCompletionSource nextNode;
        public ref AutoResetSTaskCompletionSource NextNode => ref this.nextNode;

        static AutoResetSTaskCompletionSource()
        {
            TaskPool.RegisterSizeGetter(typeof(AutoResetSTaskCompletionSource), () => pool.Size);
        }

        private STaskCompletionSourceCore<AsyncUnit> core;

        AutoResetSTaskCompletionSource()
        {
        }

        [DebuggerHidden]
        public static AutoResetSTaskCompletionSource Create()
        {
            if (!pool.TryPop(out var result))
            {
                result = new AutoResetSTaskCompletionSource();
            }
            return result;
        }

        [DebuggerHidden]
        public static AutoResetSTaskCompletionSource CreateFromCanceled(CancellationToken cancellationToken, out short token)
        {
            var source = Create();
            source.TrySetCanceled(cancellationToken);
            token = source.core.Version;
            return source;
        }

        [DebuggerHidden]
        public static AutoResetSTaskCompletionSource CreateFromException(Exception exception, out short token)
        {
            var source = Create();
            source.TrySetException(exception);
            token = source.core.Version;
            return source;
        }

        [DebuggerHidden]
        public static AutoResetSTaskCompletionSource CreateCompleted(out short token)
        {
            var source = Create();
            source.TrySetResult();
            token = source.core.Version;
            return source;
        }

        public STask Task
        {
            [DebuggerHidden]
            get
            {
                return new STask(this, this.core.Version);
            }
        }

        [DebuggerHidden]
        public bool TrySetResult()
        {
            return this.core.TrySetResult(AsyncUnit.Default);
        }

        [DebuggerHidden]
        public bool TrySetCanceled(CancellationToken cancellationToken = default)
        {
            return this.core.TrySetCanceled(cancellationToken);
        }

        [DebuggerHidden]
        public bool TrySetException(Exception exception)
        {
            return this.core.TrySetException(exception);
        }

        [DebuggerHidden]
        public void GetResult(short token)
        {
            try
            {
                this.core.GetResult(token);
            }
            finally
            {
                this.TryReturn();
            }

        }

        [DebuggerHidden]
        public STaskStatus GetStatus(short token)
        {
            return this.core.GetStatus(token);
        }

        [DebuggerHidden]
        public STaskStatus UnsafeGetStatus()
        {
            return this.core.UnsafeGetStatus();
        }

        [DebuggerHidden]
        public void OnCompleted(Action<object> continuation, object state, short token)
        {
            this.core.OnCompleted(continuation, state, token);
        }

        [DebuggerHidden]
        bool TryReturn()
        {
            this.core.Reset();
            return pool.TryPush(this);
        }
    }

    /// <summary>
    /// 用于构造<see cref="STask{T}"/>，内部由<see cref="STaskCompletionSourceCore{TResult}"/>实现，获取结果(await)后自动设置其<see cref="STaskCompletionSourceCore{TResult}.Version"/>
    /// </summary>
    public class AutoResetSTaskCompletionSource<T> : ISTaskSource<T>, ITaskPoolNode<AutoResetSTaskCompletionSource<T>>, IPromise<T>
    {
        static TaskPool<AutoResetSTaskCompletionSource<T>> pool;
        AutoResetSTaskCompletionSource<T> nextNode;
        public ref AutoResetSTaskCompletionSource<T> NextNode => ref this.nextNode;

        static AutoResetSTaskCompletionSource()
        {
            TaskPool.RegisterSizeGetter(typeof(AutoResetSTaskCompletionSource<T>), () => pool.Size);
        }

        STaskCompletionSourceCore<T> core;

        AutoResetSTaskCompletionSource()
        {
        }

        [DebuggerHidden]
        public static AutoResetSTaskCompletionSource<T> Create()
        {
            if (!pool.TryPop(out var result))
            {
                result = new AutoResetSTaskCompletionSource<T>();
            }
            return result;
        }

        [DebuggerHidden]
        public static AutoResetSTaskCompletionSource<T> CreateFromCanceled(CancellationToken cancellationToken, out short token)
        {
            var source = Create();
            source.TrySetCanceled(cancellationToken);
            token = source.core.Version;
            return source;
        }

        [DebuggerHidden]
        public static AutoResetSTaskCompletionSource<T> CreateFromException(Exception exception, out short token)
        {
            var source = Create();
            source.TrySetException(exception);
            token = source.core.Version;
            return source;
        }

        [DebuggerHidden]
        public static AutoResetSTaskCompletionSource<T> CreateFromResult(T result, out short token)
        {
            var source = Create();
            source.TrySetResult(result);
            token = source.core.Version;
            return source;
        }

        public STask<T> Task
        {
            [DebuggerHidden]
            get
            {
                return new STask<T>(this, this.core.Version);
            }
        }

        [DebuggerHidden]
        public bool TrySetResult(T result)
        {
            return this.core.TrySetResult(result);
        }

        [DebuggerHidden]
        public bool TrySetCanceled(CancellationToken cancellationToken = default)
        {
            return this.core.TrySetCanceled(cancellationToken);
        }

        [DebuggerHidden]
        public bool TrySetException(Exception exception)
        {
            return this.core.TrySetException(exception);
        }

        [DebuggerHidden]
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

        [DebuggerHidden]
        void ISTaskSource.GetResult(short token)
        {
            this.GetResult(token);
        }

        [DebuggerHidden]
        public STaskStatus GetStatus(short token)
        {
            return this.core.GetStatus(token);
        }

        [DebuggerHidden]
        public STaskStatus UnsafeGetStatus()
        {
            return this.core.UnsafeGetStatus();
        }

        [DebuggerHidden]
        public void OnCompleted(Action<object> continuation, object state, short token)
        {
            this.core.OnCompleted(continuation, state, token);
        }

        [DebuggerHidden]
        bool TryReturn()
        {
            this.core.Reset();
            return pool.TryPush(this);
        }
    }
}