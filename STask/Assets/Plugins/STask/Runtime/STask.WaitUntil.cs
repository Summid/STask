using SFramework.Threading.Tasks.Internal;
using System;
using System.Threading;

namespace SFramework.Threading.Tasks
{
    public partial struct STask
    {
        /// <summary> Wait until predicate return true </summary>
        public static STask WaitUntil(Func<bool> predicate, PlayerLoopTiming timing = PlayerLoopTiming.Update, CancellationToken cancellationToken = default(CancellationToken))
        {
            return new STask(WaitUntilPromise.Create(predicate, timing, cancellationToken, out var token), token);
        }

        private sealed class WaitUntilPromise : ISTaskSource, IPlayerLoopItem, ITaskPoolNode<WaitUntilPromise>
        {
            private static TaskPool<WaitUntilPromise> pool;
            private WaitUntilPromise nextNode;
            public ref WaitUntilPromise NextNode => ref this.nextNode;

            static WaitUntilPromise()
            {
                TaskPool.RegisterSizeGetter(typeof(WaitUntilPromise), () => pool.Size);
            }

            private Func<bool> predicate;
            private CancellationToken cancellationToken;

            private STaskCompletionSourceCore<object> core;

            private WaitUntilPromise() { }

            public static ISTaskSource Create(Func<bool> predicate, PlayerLoopTiming timing, CancellationToken cancellationToken, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AutoResetSTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);
                }

                if (!pool.TryPop(out WaitUntilPromise result))
                {
                    result = new WaitUntilPromise();
                }

                result.predicate = predicate;
                result.cancellationToken = cancellationToken;

                TaskTracker.TrackActiveTask(result, 3);

                PlayerLoopHelper.AddAction(timing, result);

                token = result.core.Version;
                return result;
            }

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

            public bool MoveNext()
            {
                if (this.cancellationToken.IsCancellationRequested)
                {
                    this.core.TrySetCanceled(this.cancellationToken);
                    return false;
                }

                try
                {
                    if (!this.predicate())
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    this.core.TrySetException(ex);
                    return false;
                }

                this.core.TrySetResult(null);
                return false;
            }

            private bool TryReturn()
            {
                TaskTracker.RemoveTracking(this);
                this.core.Reset();
                this.predicate = default;
                this.cancellationToken = default;
                return pool.TryPush(this);
            }
        }
    }
}