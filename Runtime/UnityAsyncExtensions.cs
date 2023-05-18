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

        public static STask ToSTask(this AsyncOperation asyncOperation, IProgress<float> progress = null, PlayerLoopTiming timing = PlayerLoopTiming.Update, CancellationToken cancellationToken = default(CancellationToken))
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            if (cancellationToken.IsCancellationRequested)
                return STask.FromCanceled(cancellationToken);
            if (asyncOperation.isDone)
                return STask.CompletedTask;
            return new STask(AsyncOperationConfiguredSource.Create(asyncOperation, timing, progress, cancellationToken, out short token), token);
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

            STaskCompletionSourceCore<AsyncUnit> core;

            private AsyncOperationConfiguredSource() { }

            public static ISTaskSource Create(AsyncOperation asyncOperation, PlayerLoopTiming timing, IProgress<float> progress, CancellationToken cancellationToken, out short token)
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
                this.core.Reset();
                this.asyncOperation = default;
                this.progress = default;
                this.cancellationToken = default;
                return pool.TryPush(this);
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

        public static STask<UnityEngine.Object> ToSTask(this AssetBundleRequest asyncOperation, IProgress<float> progress = null, PlayerLoopTiming timing = PlayerLoopTiming.Update, CancellationToken cancellationToken = default(CancellationToken))
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            if (cancellationToken.IsCancellationRequested) return STask.FromCanceled<UnityEngine.Object>(cancellationToken);
            if (asyncOperation.isDone) return STask.FromResult(asyncOperation.asset);
            return new STask<UnityEngine.Object>(AssetBundleRequestConfiguredSource.Create(asyncOperation, timing, progress, cancellationToken, out var token), token);
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

            STaskCompletionSourceCore<UnityEngine.Object> core;

            private AssetBundleRequestConfiguredSource() { }

            public static ISTaskSource<UnityEngine.Object> Create(AssetBundleRequest asyncOperation, PlayerLoopTiming timing, IProgress<float> progress, CancellationToken cancellationToken, out short token)
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

            public bool MoveNext()
            {
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

            bool TryReturn()
            {
                this.core.Reset();
                this.asyncOperation = default;
                this.progress = default;
                this.cancellationToken = default;
                return pool.TryPush(this);
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

        public static STask<AssetBundle> ToSTask(this AssetBundleCreateRequest asyncOperation, IProgress<float> progress = null, PlayerLoopTiming timing = PlayerLoopTiming.Update, CancellationToken cancellationToken = default(CancellationToken))
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            if (cancellationToken.IsCancellationRequested) return STask.FromCanceled<AssetBundle>(cancellationToken);
            if (asyncOperation.isDone) return STask.FromResult(asyncOperation.assetBundle);
            return new STask<AssetBundle>(AssetBundleCreateRequestConfiguredSource.Create(asyncOperation, timing, progress, cancellationToken, out var token), token);
        }

        public struct AssetBundleCreateRequestAwaiter : ICriticalNotifyCompletion
        {
            AssetBundleCreateRequest asyncOperation;
            Action<AsyncOperation> continuationAction;

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

        sealed class AssetBundleCreateRequestConfiguredSource : ISTaskSource<AssetBundle>, IPlayerLoopItem, ITaskPoolNode<AssetBundleCreateRequestConfiguredSource>
        {
            static TaskPool<AssetBundleCreateRequestConfiguredSource> pool;
            AssetBundleCreateRequestConfiguredSource nextNode;
            public ref AssetBundleCreateRequestConfiguredSource NextNode => ref this.nextNode;

            static AssetBundleCreateRequestConfiguredSource()
            {
                TaskPool.RegisterSizeGetter(typeof(AssetBundleCreateRequestConfiguredSource), () => pool.Size);
            }

            AssetBundleCreateRequest asyncOperation;
            IProgress<float> progress;
            CancellationToken cancellationToken;

            STaskCompletionSourceCore<AssetBundle> core;

            AssetBundleCreateRequestConfiguredSource()
            {

            }

            public static ISTaskSource<AssetBundle> Create(AssetBundleCreateRequest asyncOperation, PlayerLoopTiming timing, IProgress<float> progress, CancellationToken cancellationToken, out short token)
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

            public bool MoveNext()
            {
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

            bool TryReturn()
            {
                this.core.Reset();
                this.asyncOperation = default;
                this.progress = default;
                this.cancellationToken = default;
                return pool.TryPush(this);
            }
        }

        #endregion
    }
}