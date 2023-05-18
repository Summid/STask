using System;
using System.Threading;

namespace SFramework.Threading.Tasks
{
    public partial struct STask
    {
        /// <summary>
        /// 向PlayerLoop中添加迭代方法
        /// </summary>
        /// <param name="onUpdate"></param>
        /// <param name="timing"></param>
        /// <param name="cancellationToken"></param>
        public static void UpdateTask(Action onUpdate, PlayerLoopTiming timing, CancellationToken cancellationToken)
        {
            if (onUpdate == null)
            {
                UnityEngine.Debug.LogWarning("UpdateTask onUpdate delegate is empty.");
                return;
            }
            new STask(UpdatePromise.Create(onUpdate, timing, cancellationToken, out short token), token);
        }

        sealed class UpdatePromise : ISTaskSource, IPlayerLoopItem, ITaskPoolNode<UpdatePromise>
        {
            private static TaskPool<UpdatePromise> pool;
            private UpdatePromise nextNode;
            public ref UpdatePromise NextNode => ref this.nextNode;

            static UpdatePromise()
            {
                TaskPool.RegisterSizeGetter(typeof(UpdatePromise), () => pool.Size);
            }

            private CancellationToken cancellationToken;
            private STaskCompletionSourceCore<AsyncUnit> core;
            private Action onUpdate;

            private UpdatePromise() { }

            public static ISTaskSource Create(Action onUpdate, PlayerLoopTiming timing, CancellationToken cancellationToken, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AutoResetSTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);
                }

                if (!pool.TryPop(out UpdatePromise result))
                {
                    result = new UpdatePromise();
                }

                result.onUpdate = onUpdate;
                result.cancellationToken = cancellationToken;

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
                if (this.cancellationToken.IsCancellationRequested || this.onUpdate == null)
                {
                    this.core.TrySetCanceled(this.cancellationToken);
                    return false;
                }

                if (PlayerLoopHelper.IsMainThread)
                {
                    try
                    {
                        this.onUpdate?.Invoke();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        this.core.TrySetException(ex);
                        return false;
                    }
                }

                this.core.TrySetResult(AsyncUnit.Default);
                return false;
            }

            private bool TryReturn()
            {
                this.core.Reset();
                this.cancellationToken = default;
                return pool.TryPush(this);
            }
        }
    }
}