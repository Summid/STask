using SFramework.Threading.Tasks.Internal;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace SFramework.Threading.Tasks
{
    public static partial class UnityAsyncExtensions
    {
        #region AsyncOperation

#if !UNITY_2023_1_OR_NEWER
        // unity2023.1 之后的版本 Unity 自带了 AsyncOperationAwaitableExtensions.GetAwaiter
        public static AsyncOperationAwaiter GetAwaiter(this AsyncOperation asyncOperation)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            return new AsyncOperationAwaiter(asyncOperation);
        }
#endif

        public static STask WithCancellation(this AsyncOperation asyncOperation, CancellationToken cancellationToken)
        {
            return ToSTask(asyncOperation, cancellationToken: cancellationToken);
        }

        public static STask WithCancellation(this AsyncOperation asyncOperation, CancellationToken cancellationToken, bool cancelImmediately)
        {
            return ToSTask(asyncOperation, cancellationToken: cancellationToken, cancelImmediately: cancelImmediately);
        }

        public static STask ToSTask(this AsyncOperation asyncOperation, IProgress<float> progress = null, PlayerLoopTiming timing = PlayerLoopTiming.Update, CancellationToken cancellationToken = default(CancellationToken), bool cancelImmediately = false)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            if (cancellationToken.IsCancellationRequested)
                return STask.FromCanceled(cancellationToken);
            if (asyncOperation.isDone)
                return STask.CompletedTask;
            return new STask(AsyncOperationConfiguredSource.Create(asyncOperation, timing, progress, cancellationToken, cancelImmediately, out short token), token);
        }

        public struct AsyncOperationAwaiter : ICriticalNotifyCompletion
        {
            private AsyncOperation asyncOperation;
            private Action<AsyncOperation> continuationAction;

            public AsyncOperationAwaiter(AsyncOperation asyncOperation)
            {
                this.asyncOperation = asyncOperation;
                this.continuationAction = null;
            }

            public bool IsCompleted => this.asyncOperation.isDone;

            public void GetResult()
            {
                if (this.continuationAction != null)
                {
                    this.asyncOperation.completed -= this.continuationAction;
                    this.continuationAction = null;
                    this.asyncOperation = null;
                }
                else
                {
                    this.asyncOperation = null;
                }
            }

            public void OnCompleted(Action continuation)
            {
                this.UnsafeOnCompleted(continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                Error.ThrowWhenContinuationIsAlreadyRegistered(this.continuationAction);
                this.continuationAction = PooledDelegate<AsyncOperation>.Create(continuation);
                this.asyncOperation.completed += this.continuationAction;
            }
        }

        private sealed class AsyncOperationConfiguredSource : ISTaskSource, IPlayerLoopItem, ITaskPoolNode<AsyncOperationConfiguredSource>
        {
            private static TaskPool<AsyncOperationConfiguredSource> pool;
            private AsyncOperationConfiguredSource nodeNode;
            public ref AsyncOperationConfiguredSource NextNode => ref this.nodeNode;

            static AsyncOperationConfiguredSource()
            {
                TaskPool.RegisterSizeGetter(typeof(AsyncOperationConfiguredSource), () => pool.Size);
            }

            private AsyncOperation asyncOperation;
            private IProgress<float> progress;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;
            private bool cancelImmediately;
            private bool completed;

            private STaskCompletionSourceCore<AsyncUnit> core;

            private Action<AsyncOperation> continuationAction;

            private AsyncOperationConfiguredSource()
            {
                this.continuationAction = this.Continuation;
            }

            public static ISTaskSource Create(AsyncOperation asyncOperation, PlayerLoopTiming timing, IProgress<float> progress, CancellationToken cancellationToken, bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AutoResetSTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);
                }

                if (!pool.TryPop(out AsyncOperationConfiguredSource result))
                {
                    result = new AsyncOperationConfiguredSource();
                }

                result.asyncOperation = asyncOperation;
                result.progress = progress;
                result.cancellationToken = cancellationToken;
                result.cancelImmediately = cancelImmediately;
                result.completed = false;

                asyncOperation.completed += result.continuationAction;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                {
                    result.cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(state =>
                    {
                        var source = (AsyncOperationConfiguredSource)state;
                        source.core.TrySetCanceled(source.cancellationToken);
                    }, result);
                }

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
                    if (!(this.cancelImmediately && this.cancellationToken.IsCancellationRequested))
                    {
                        this.TryReturn();
                    }
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
                // Already completed
                if (this.completed || this.asyncOperation == null)
                {
                    return false;
                }

                if (this.cancellationToken.IsCancellationRequested)
                {
                    this.core.TrySetCanceled(this.cancellationToken);
                    return false;
                }

                if (this.progress != null)
                {
                    this.progress.Report(this.asyncOperation.progress);
                }

                if (this.asyncOperation.isDone)
                {
                    this.core.TrySetResult(AsyncUnit.Default);
                    return false;
                }

                return true;
            }

            private bool TryReturn()
            {
                TaskTracker.RemoveTracking(this);
                this.core.Reset();
                this.asyncOperation.completed -= this.continuationAction;
                this.asyncOperation = default;
                this.progress = default;
                this.cancellationToken = default;
                this.cancellationTokenRegistration.Dispose();
                this.cancelImmediately = default;
                return pool.TryPush(this);
            }

            private void Continuation(AsyncOperation _)
            {
                if (this.completed)
                {
                    return;
                }
                this.completed = true;
                if (this.cancellationToken.IsCancellationRequested)
                {
                    this.core.TrySetCanceled(this.cancellationToken);
                }
                else
                {
                    this.core.TrySetResult(AsyncUnit.Default);
                }
            }
        }

        #endregion

        #region AssetBundleRequest

        public static AssetBundleRequestAwaiter GetAwaiter(this AssetBundleRequest asyncOperation)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            return new AssetBundleRequestAwaiter(asyncOperation);
        }

        public static STask<UnityEngine.Object> WithCancellation(this AssetBundleRequest asyncOperation, CancellationToken cancellationToken)
        {
            return ToSTask(asyncOperation, cancellationToken: cancellationToken);
        }

        public static STask<UnityEngine.Object> WithCancellation(this AssetBundleRequest asyncOperation, CancellationToken cancellationToken, bool cancelImmediately)
        {
            return ToSTask(asyncOperation, cancellationToken: cancellationToken, cancelImmediately: cancelImmediately);
        }

        public static STask<UnityEngine.Object> ToSTask(this AssetBundleRequest asyncOperation, IProgress<float> progress = null, PlayerLoopTiming timing = PlayerLoopTiming.Update, CancellationToken cancellationToken = default(CancellationToken),
            bool cancelImmediately = false)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            if (cancellationToken.IsCancellationRequested) return STask.FromCanceled<UnityEngine.Object>(cancellationToken);
            if (asyncOperation.isDone) return STask.FromResult(asyncOperation.asset);
            return new STask<UnityEngine.Object>(AssetBundleRequestConfiguredSource.Create(asyncOperation, timing, progress, cancellationToken, cancelImmediately, out var token), token);
        }

        public struct AssetBundleRequestAwaiter : ICriticalNotifyCompletion
        {
            private AssetBundleRequest asyncOperation;
            private Action<AsyncOperation> continuationAction;

            public AssetBundleRequestAwaiter(AssetBundleRequest asyncOperation)
            {
                this.asyncOperation = asyncOperation;
                this.continuationAction = null;
            }

            public bool IsCompleted => this.asyncOperation.isDone;

            public UnityEngine.Object GetResult()
            {
                if (this.continuationAction != null)
                {
                    this.asyncOperation.completed -= this.continuationAction;
                    this.continuationAction = null;
                    UnityEngine.Object result = this.asyncOperation.asset;
                    this.asyncOperation = null;
                    return result;
                }
                else
                {
                    UnityEngine.Object result = this.asyncOperation.asset;
                    this.asyncOperation = null;
                    return result;
                }
            }

            public void OnCompleted(Action continuation)
            {
                this.UnsafeOnCompleted(continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                Error.ThrowWhenContinuationIsAlreadyRegistered(this.continuationAction);
                this.continuationAction = PooledDelegate<AsyncOperation>.Create(continuation);
                this.asyncOperation.completed += this.continuationAction;
            }
        }

        private sealed class AssetBundleRequestConfiguredSource : ISTaskSource<UnityEngine.Object>, IPlayerLoopItem, ITaskPoolNode<AssetBundleRequestConfiguredSource>
        {
            private static TaskPool<AssetBundleRequestConfiguredSource> pool;
            private AssetBundleRequestConfiguredSource nextNode;
            public ref AssetBundleRequestConfiguredSource NextNode => ref this.nextNode;

            static AssetBundleRequestConfiguredSource()
            {
                TaskPool.RegisterSizeGetter(typeof(AssetBundleRequestConfiguredSource), () => pool.Size);
            }

            private AssetBundleRequest asyncOperation;
            private IProgress<float> progress;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;
            private bool cancelImmediately;
            private bool completed;

            private STaskCompletionSourceCore<UnityEngine.Object> core;

            private Action<AsyncOperation> continuationAction;

            private AssetBundleRequestConfiguredSource()
            {
                this.continuationAction = this.Continuation;
            }

            public static ISTaskSource<UnityEngine.Object> Create(AssetBundleRequest asyncOperation, PlayerLoopTiming timing, IProgress<float> progress, CancellationToken cancellationToken, bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AutoResetSTaskCompletionSource<UnityEngine.Object>.CreateFromCanceled(cancellationToken, out token);
                }

                if (!pool.TryPop(out AssetBundleRequestConfiguredSource result))
                {
                    result = new AssetBundleRequestConfiguredSource();
                }

                result.asyncOperation = asyncOperation;
                result.progress = progress;
                result.cancellationToken = cancellationToken;
                result.cancelImmediately = cancelImmediately;
                result.completed = false;

                asyncOperation.completed += result.continuationAction;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                {
                    result.cancellationTokenRegistration = result.cancellationToken.RegisterWithoutCaptureExecutionContext(state =>
                    {
                        var source = (AssetBundleRequestConfiguredSource)state;
                        source.core.TrySetCanceled(source.cancellationToken);
                    }, result);
                }

                TaskTracker.TrackActiveTask(result, 3);

                PlayerLoopHelper.AddAction(timing, result);

                token = result.core.Version;
                return result;
            }

            public UnityEngine.Object GetResult(short token)
            {
                try
                {
                    return this.core.GetResult(token);
                }
                finally
                {
                    if (!(this.cancelImmediately && this.cancellationToken.IsCancellationRequested))
                    {
                        this.TryReturn();
                    }
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

            public bool MoveNext()
            {
                // Already completed
                if (this.completed || this.asyncOperation == null)
                {
                    return false;
                }

                if (this.cancellationToken.IsCancellationRequested)
                {
                    this.core.TrySetCanceled(this.cancellationToken);
                    return false;
                }

                if (this.progress != null)
                {
                    this.progress.Report(this.asyncOperation.progress);
                }

                if (this.asyncOperation.isDone)
                {
                    this.core.TrySetResult(this.asyncOperation.asset);
                    return false;
                }

                return true;
            }

            private bool TryReturn()
            {
                TaskTracker.RemoveTracking(this);
                this.core.Reset();
                this.asyncOperation.completed -= this.continuationAction;
                this.asyncOperation = default;
                this.progress = default;
                this.cancellationToken = default;
                this.cancellationTokenRegistration.Dispose();
                this.cancelImmediately = default;
                return pool.TryPush(this);
            }

            private void Continuation(AsyncOperation _)
            {
                if (this.completed)
                {
                    return;
                }
                this.completed = true;
                if (this.cancellationToken.IsCancellationRequested)
                {
                    this.core.TrySetCanceled(this.cancellationToken);
                }
                else
                {
                    this.core.TrySetResult(this.asyncOperation.asset);
                }
            }
        }

        #endregion

        #region AssetBundleCreateRequest

        public static AssetBundleCreateRequestAwaiter GetAwaiter(this AssetBundleCreateRequest asyncOperation)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            return new AssetBundleCreateRequestAwaiter(asyncOperation);
        }

        public static STask<AssetBundle> WithCancellation(this AssetBundleCreateRequest asyncOperation, CancellationToken cancellationToken)
        {
            return ToSTask(asyncOperation, cancellationToken: cancellationToken);
        }

        public static STask<AssetBundle> WithCancellation(this AssetBundleCreateRequest asyncOperation, CancellationToken cancellationToken, bool cancelImmediately)
        {
            return ToSTask(asyncOperation, cancellationToken: cancellationToken, cancelImmediately: cancelImmediately);
        }

        public static STask<AssetBundle> ToSTask(this AssetBundleCreateRequest asyncOperation, IProgress<float> progress = null, PlayerLoopTiming timing = PlayerLoopTiming.Update, CancellationToken cancellationToken = default(CancellationToken),
            bool cancelImmediately = false)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            if (cancellationToken.IsCancellationRequested) return STask.FromCanceled<AssetBundle>(cancellationToken);
            if (asyncOperation.isDone) return STask.FromResult(asyncOperation.assetBundle);
            return new STask<AssetBundle>(AssetBundleCreateRequestConfiguredSource.Create(asyncOperation, timing, progress, cancellationToken, cancelImmediately, out var token), token);
        }

        public struct AssetBundleCreateRequestAwaiter : ICriticalNotifyCompletion
        {
            private AssetBundleCreateRequest asyncOperation;
            private Action<AsyncOperation> continuationAction;

            public AssetBundleCreateRequestAwaiter(AssetBundleCreateRequest asyncOperation)
            {
                this.asyncOperation = asyncOperation;
                this.continuationAction = null;
            }

            public bool IsCompleted => this.asyncOperation.isDone;

            public AssetBundle GetResult()
            {
                if (this.continuationAction != null)
                {
                    this.asyncOperation.completed -= this.continuationAction;
                    this.continuationAction = null;
                    AssetBundle result = this.asyncOperation.assetBundle;
                    this.asyncOperation = null;
                    return result;
                }
                else
                {
                    AssetBundle result = this.asyncOperation.assetBundle;
                    this.asyncOperation = null;
                    return result;
                }
            }

            public void OnCompleted(Action continuation)
            {
                this.UnsafeOnCompleted(continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                Error.ThrowWhenContinuationIsAlreadyRegistered(this.continuationAction);
                this.continuationAction = PooledDelegate<AsyncOperation>.Create(continuation);
                this.asyncOperation.completed += this.continuationAction;
            }
        }

        private sealed class AssetBundleCreateRequestConfiguredSource : ISTaskSource<AssetBundle>, IPlayerLoopItem, ITaskPoolNode<AssetBundleCreateRequestConfiguredSource>
        {
            private static TaskPool<AssetBundleCreateRequestConfiguredSource> pool;
            private AssetBundleCreateRequestConfiguredSource nextNode;
            public ref AssetBundleCreateRequestConfiguredSource NextNode => ref this.nextNode;

            static AssetBundleCreateRequestConfiguredSource()
            {
                TaskPool.RegisterSizeGetter(typeof(AssetBundleCreateRequestConfiguredSource), () => pool.Size);
            }

            private AssetBundleCreateRequest asyncOperation;
            private IProgress<float> progress;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;
            private bool cancelImmediately;
            private bool completed;

            private STaskCompletionSourceCore<AssetBundle> core;

            private Action<AsyncOperation> continuationAction;

            private AssetBundleCreateRequestConfiguredSource()
            {
                this.continuationAction = this.Continuation;
            }

            public static ISTaskSource<AssetBundle> Create(AssetBundleCreateRequest asyncOperation, PlayerLoopTiming timing, IProgress<float> progress, CancellationToken cancellationToken, bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AutoResetSTaskCompletionSource<AssetBundle>.CreateFromCanceled(cancellationToken, out token);
                }

                if (!pool.TryPop(out AssetBundleCreateRequestConfiguredSource result))
                {
                    result = new AssetBundleCreateRequestConfiguredSource();
                }

                result.asyncOperation = asyncOperation;
                result.progress = progress;
                result.cancellationToken = cancellationToken;
                result.cancelImmediately = cancelImmediately;
                result.completed = false;

                asyncOperation.completed += result.continuationAction;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                {
                    result.cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(state =>
                    {
                        var source = (AssetBundleCreateRequestConfiguredSource)state;
                        source.core.TrySetCanceled(source.cancellationToken);
                    }, result);
                }

                TaskTracker.TrackActiveTask(result, 3);

                PlayerLoopHelper.AddAction(timing, result);

                token = result.core.Version;
                return result;
            }

            public AssetBundle GetResult(short token)
            {
                try
                {
                    return this.core.GetResult(token);
                }
                finally
                {
                    if (!(this.cancelImmediately && this.cancellationToken.IsCancellationRequested))
                    {
                        this.TryReturn();
                    }
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

            public bool MoveNext()
            {
                // Already completed
                if (this.completed || this.asyncOperation == null)
                {
                    return false;
                }

                if (this.cancellationToken.IsCancellationRequested)
                {
                    this.core.TrySetCanceled(this.cancellationToken);
                    return false;
                }

                if (this.progress != null)
                {
                    this.progress.Report(this.asyncOperation.progress);
                }

                if (this.asyncOperation.isDone)
                {
                    this.core.TrySetResult(this.asyncOperation.assetBundle);
                    return false;
                }

                return true;
            }

            private bool TryReturn()
            {
                TaskTracker.RemoveTracking(this);
                this.core.Reset();
                this.asyncOperation.completed -= this.continuationAction;
                this.asyncOperation = default;
                this.progress = default;
                this.cancellationToken = default;
                this.cancellationTokenRegistration.Dispose();
                this.cancelImmediately = default;
                return pool.TryPush(this);
            }

            private void Continuation(AsyncOperation _)
            {
                if (this.completed)
                {
                    return;
                }
                this.completed = true;
                if (this.cancellationToken.IsCancellationRequested)
                {
                    this.core.TrySetCanceled(this.cancellationToken);
                }
                else
                {
                    this.core.TrySetResult(this.asyncOperation.assetBundle);
                }
            }
        }

        #endregion
    }
}