using SFramework.Threading.Tasks.Internal;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace SFramework.Threading.Tasks
{
    public enum DelayType
    {
        /// <summary>use Time.DeltaTime</summary>
        DeltaTime,
        /// <summary>Ignore timescale, use Time.unscaledDeltaTime</summary>
        UnscaledDeltaTime,
        /// <summary>use Stopwatch.GetTimestamp()</summary>
        RealTime
    }

    public partial struct STask
    {
        #region NextFrame
        public static STask NextFrame()
        {
            return new STask(NextFramePromise.Create(PlayerLoopTiming.Update, CancellationToken.None, out short token), token);
        }

        public static STask NextFrame(PlayerLoopTiming timing)
        {
            return new STask(NextFramePromise.Create(timing, CancellationToken.None, out short token), token);
        }

        public static STask NextFrame(CancellationToken cancellationToken)
        {
            return new STask(NextFramePromise.Create(PlayerLoopTiming.Update, cancellationToken, out short token), token);
        }

        public static STask NextFrame(PlayerLoopTiming timing,CancellationToken cancellationToken)
        {
            return new STask(NextFramePromise.Create(timing, cancellationToken, out short token), token);
        }

        sealed class NextFramePromise : ISTaskSource, IPlayerLoopItem, ITaskPoolNode<NextFramePromise>
        {
            private static TaskPool<NextFramePromise> pool;
            private NextFramePromise nextNode;
            public ref NextFramePromise NextNode => ref this.nextNode;

            static NextFramePromise()
            {
                TaskPool.RegisterSizeGetter(typeof(NextFramePromise), () => pool.Size);
            }

            private int frameCount;
            private CancellationToken cancellationToken;
            private STaskCompletionSourceCore<AsyncUnit> core;

            private NextFramePromise() { }

            public static ISTaskSource Create(PlayerLoopTiming timing, CancellationToken cancellationToken, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AutoResetSTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);
                }

                if (!pool.TryPop(out NextFramePromise result))
                {
                    result = new NextFramePromise();
                }

                result.frameCount = PlayerLoopHelper.IsMainThread ? Time.frameCount : -1;
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
                    this.TryRetuen();
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

                if (this.frameCount == Time.frameCount)
                {
                    return true;
                }

                this.core.TrySetResult(AsyncUnit.Default);
                return false;
            }

            private bool TryRetuen()
            {
                this.core.Reset();
                this.cancellationToken = default;
                return pool.TryPush(this);
            }
        }
        #endregion

        #region Delay Frame
        public static STask DelayFrame(int delayFrameCount, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default)
        {
            if (delayFrameCount < 0)
            {
                throw new ArgumentOutOfRangeException("Delay does not allow minus delayFrameCount. delayFrameCount:" + delayFrameCount);
            }

            return new STask(DelayFramePromise.Create(delayFrameCount, delayTiming, cancellationToken, out var token), token);
        }

        sealed class DelayFramePromise : ISTaskSource, IPlayerLoopItem, ITaskPoolNode<DelayFramePromise>
        {
            private static TaskPool<DelayFramePromise> pool;
            private DelayFramePromise nextNode;
            public ref DelayFramePromise NextNode => ref this.nextNode;

            static DelayFramePromise()
            {
                TaskPool.RegisterSizeGetter(typeof(DelayFramePromise), () => pool.Size);
            }

            private int initialFrame;
            private int delayFrameCount;
            private CancellationToken cancellationToken;

            private int currentFrameCount;
            private STaskCompletionSourceCore<AsyncUnit> core;

            private DelayFramePromise() { }

            public static ISTaskSource Create(int delayFrameCount, PlayerLoopTiming timing, CancellationToken cancellationToken, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AutoResetSTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);
                }

                if (!pool.TryPop(out var result))
                {
                    result = new DelayFramePromise();
                }

                result.delayFrameCount = delayFrameCount;
                result.cancellationToken = cancellationToken;
                result.initialFrame = PlayerLoopHelper.IsMainThread ? Time.frameCount : -1;

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

                if (this.currentFrameCount == 0)
                {
                    if (this.delayFrameCount == 0) // same as Yield
                    {
                        this.core.TrySetResult(AsyncUnit.Default);
                        return false;
                    }

                    // skip in initial frame
                    if (this.initialFrame == Time.frameCount)
                    {
#if UNITY_EDITOR
                        if (PlayerLoopHelper.IsMainThread && !UnityEditor.EditorApplication.isPlaying)
                        {
                            //nothing happened
                        }
                        else
                        {
                            return true;
                        }
#else
                        reutrn true;
#endif
                    }
                }

                if (++this.currentFrameCount >= this.delayFrameCount)
                {
                    this.core.TrySetResult(AsyncUnit.Default);
                    return false;
                }

                return true;
            }

            private bool TryReturn()
            {
                this.core.Reset();
                this.currentFrameCount = default;
                this.delayFrameCount = default;
                this.cancellationToken = default;
                return pool.TryPush(this);
            }
        }

        #endregion

        #region Delay Time
        public static STask Delay(int millisecondsDelay, bool ignoreTimeScale = false, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default(CancellationToken))
        {
            TimeSpan delayTimeSpan = TimeSpan.FromMilliseconds(millisecondsDelay);
            return Delay(delayTimeSpan, ignoreTimeScale, delayTiming, cancellationToken);
        }

        public static STask Delay(TimeSpan delayTimeSpan, bool ignoreTimeScale, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default(CancellationToken))
        {
            DelayType delayType = ignoreTimeScale ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime;
            return Delay(delayTimeSpan, delayType, delayTiming, cancellationToken);
        }

        public static STask Delay(int millisecondsDelay, DelayType delayType, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default(CancellationToken))
        {
            TimeSpan delayTimeSpan = TimeSpan.FromMilliseconds(millisecondsDelay);
            return Delay(delayTimeSpan, delayType, delayTiming, cancellationToken);
        }

        public static STask Delay(TimeSpan delayTimeSpan, DelayType delayType, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (delayTimeSpan < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("Delay does not allow mins delayTimeSpan. delayTimespan:" + delayTimeSpan);
            }

#if UNITY_EDITOR
            //没在 Play 模式，强制使用 Realtime
            if (PlayerLoopHelper.IsMainThread && !UnityEditor.EditorApplication.isPlaying)
            {
                delayType = DelayType.RealTime;
            }
#endif

            switch (delayType)
            {
                case DelayType.UnscaledDeltaTime:
                    {
                        return new STask(DelayIgnoreTimeScalePromise.Create(delayTimeSpan, delayTiming, cancellationToken, out short token), token);
                    }
                case DelayType.RealTime:
                    {
                        return new STask(DelayRealtimePromise.Create(delayTimeSpan, delayTiming, cancellationToken, out short token), token);
                    }
                case DelayType.DeltaTime:
                default:
                    {
                        return new STask(DelayPromise.Create(delayTimeSpan, delayTiming, cancellationToken, out short token), token);
                    }
            }
        }

        public static STask WaitForSeconds(float duration, bool ignoreTimeScale = false, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default)
        {
            return Delay(Mathf.RoundToInt(1000 * duration), ignoreTimeScale, delayTiming, cancellationToken);
        }

        public static STask WaitForSeconds(int duration, bool ignoreTimeScale = false, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default)
        {
            return Delay(1000 * duration, ignoreTimeScale, delayTiming, cancellationToken);
        }

        private sealed class DelayPromise : ISTaskSource, IPlayerLoopItem, ITaskPoolNode<DelayPromise>
        {
            private static TaskPool<DelayPromise> pool;
            private DelayPromise nextNode;
            public ref DelayPromise NextNode => ref this.nextNode;

            static DelayPromise()
            {
                TaskPool.RegisterSizeGetter(typeof(DelayPromise), () => pool.Size);
            }

            private int initialFrame;
            private float delayTimeSpan;
            private float elapsed;
            private CancellationToken cancellationToken;

            STaskCompletionSourceCore<object> core;

            private DelayPromise() { }

            public static ISTaskSource Create(TimeSpan delayTimeSpan, PlayerLoopTiming timing, CancellationToken cancellationToken, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AutoResetSTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);
                }
                
                if(!pool.TryPop(out DelayPromise result))
                {
                    result = new DelayPromise();
                }

                result.elapsed = 0.0f;
                result.delayTimeSpan = (float)delayTimeSpan.TotalSeconds;
                result.cancellationToken = cancellationToken;
                result.initialFrame = PlayerLoopHelper.IsMainThread ? Time.frameCount : -1;

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
                if(this.cancellationToken.IsCancellationRequested)
                {
                    this.core.TrySetCanceled(this.cancellationToken);
                    return false;
                }

                if (this.elapsed == 0.0f)//刚开始
                {
                    if (this.initialFrame == Time.frameCount)
                    {
                        return true;
                    }
                }

                this.elapsed += Time.deltaTime;
                if (this.elapsed >= this.delayTimeSpan)
                {
                    this.core.TrySetResult(null);
                    return false;
                }

                return true;
            }

            private bool TryReturn()
            {
                this.core.Reset();
                this.delayTimeSpan = default;
                this.elapsed = default;
                this.cancellationToken = default;
                return pool.TryPush(this);
            }
        }

        private sealed class DelayIgnoreTimeScalePromise : ISTaskSource, IPlayerLoopItem, ITaskPoolNode<DelayIgnoreTimeScalePromise>
        {
            private static TaskPool<DelayIgnoreTimeScalePromise> pool;
            private DelayIgnoreTimeScalePromise nextNode;
            public ref DelayIgnoreTimeScalePromise NextNode => ref this.nextNode;

            static DelayIgnoreTimeScalePromise()
            {
                TaskPool.RegisterSizeGetter(typeof(DelayIgnoreTimeScalePromise), () => pool.Size);
            }

            float delayFrameTimeSpan;
            float elapsed;
            int initialFrame;
            CancellationToken cancellationToken;

            STaskCompletionSourceCore<object> core;

            DelayIgnoreTimeScalePromise() { }

            public static ISTaskSource Create(TimeSpan delayFrameTimeSpan, PlayerLoopTiming timing, CancellationToken cancellationToken, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AutoResetSTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);
                }

                if (!pool.TryPop(out DelayIgnoreTimeScalePromise result))
                {
                    result = new DelayIgnoreTimeScalePromise();
                }

                result.elapsed = 0.0f;
                result.delayFrameTimeSpan = (float)delayFrameTimeSpan.TotalSeconds;
                result.initialFrame = PlayerLoopHelper.IsMainThread ? Time.frameCount : -1;
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

                if (this.elapsed == 0.0f)
                {
                    if (this.initialFrame == Time.frameCount)
                    {
                        return true;
                    }
                }

                this.elapsed += Time.unscaledDeltaTime;
                if (this.elapsed >= this.delayFrameTimeSpan)
                {
                    this.core.TrySetResult(null);
                    return false;
                }

                return true;
            }

            private bool TryReturn()
            {
                this.core.Reset();
                this.delayFrameTimeSpan = default;
                this.elapsed = default;
                this.cancellationToken = default;
                return pool.TryPush(this);
            }
        }

        private sealed class DelayRealtimePromise : ISTaskSource, IPlayerLoopItem, ITaskPoolNode<DelayRealtimePromise>
        {
            private static TaskPool<DelayRealtimePromise> pool;
            private DelayRealtimePromise nextNode;
            public ref DelayRealtimePromise NextNode => ref this.nextNode;

            static DelayRealtimePromise()
            {
                TaskPool.RegisterSizeGetter(typeof(DelayRealtimePromise), () => pool.Size);
            }

            private long delayTimeSpanTicks;
            private ValueStopwatch stopwatch;
            private CancellationToken cancellationToken;

            STaskCompletionSourceCore<AsyncUnit> core;

            private DelayRealtimePromise() { }

            public static ISTaskSource Create(TimeSpan delayTimeSpan, PlayerLoopTiming timing, CancellationToken cancellationToken, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AutoResetSTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);
                }

                if (!pool.TryPop(out DelayRealtimePromise result))
                {
                    result = new DelayRealtimePromise();
                }

                result.stopwatch = ValueStopwatch.StartNew();
                result.delayTimeSpanTicks = delayTimeSpan.Ticks;
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
                    this.TryRetuen();
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

                if (this.stopwatch.IsInvalid)
                {
                    this.core.TrySetResult(AsyncUnit.Default);
                    return false;
                }

                if (this.stopwatch.ElapsedTicks >= this.delayTimeSpanTicks)
                {
                    this.core.TrySetResult(AsyncUnit.Default);
                    return false;
                }

                return true;
            }

            private bool TryRetuen()
            {
                this.core.Reset();
                this.stopwatch = default;
                this.cancellationToken = default;
                return pool.TryPush(this);
            }
        }
        #endregion
        
        #region Yield
        public static YieldAwaitable Yield()
        {
            // optimized for single continuation
            return new YieldAwaitable(PlayerLoopTiming.Update);
        }

        public static YieldAwaitable Yield(PlayerLoopTiming timing)
        {
            // optimized for single continuation
            return new YieldAwaitable(timing);
        }

        public static STask Yield(CancellationToken cancellationToken)
        {
            return new STask(YieldPromise.Create(PlayerLoopTiming.Update, cancellationToken, out var token), token);
        }

        public static STask Yield(PlayerLoopTiming timing, CancellationToken cancellationToken)
        {
            return new STask(YieldPromise.Create(timing, cancellationToken, out var token), token);
        }

        /// <summary>
        /// Same as STask.Yield(PlayerLoopTiming.LastFixedUpdate)
        /// </summary>
        /// <returns></returns>
        public static YieldAwaitable WaitForFixedUpdate()
        {
            // use LastFixedUpdate instead of FixedUpdate
            // https://github.com/Cysharp/UniTask/issues/377
            return STask.Yield(PlayerLoopTiming.LastFixedUpdate);
        }

        /// <summary>
        /// Same as UniTask.Yield(PlayerLoopTiming.LastFixedUpdate, cancellationToken)
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static STask WaitForFixedUpdate(CancellationToken cancellationToken)
        {
            return STask.Yield(PlayerLoopTiming.LastFixedUpdate, cancellationToken);
        }

        private sealed class YieldPromise : ISTaskSource, IPlayerLoopItem, ITaskPoolNode<YieldPromise>
        {
            private static TaskPool<YieldPromise> pool;
            private YieldPromise nextNode;
            public ref YieldPromise NextNode => ref this.nextNode;

            static YieldPromise()
            {
                TaskPool.RegisterSizeGetter(typeof(YieldPromise), () => pool.Size);
            }

            private CancellationToken cancellationToken;
            private STaskCompletionSourceCore<object> core;
            
            private YieldPromise() { }

            public static ISTaskSource Create(PlayerLoopTiming timing, CancellationToken cancellationToken, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AutoResetSTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);
                }

                if (!pool.TryPop(out var result))
                {
                    result = new YieldPromise();
                }

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

                this.core.TrySetResult(null);
                return false;
            }

            private bool TryReturn()
            {
                this.core.Reset();
                this.cancellationToken = default;
                return pool.TryPush(this);
            }
        }
        #endregion
        
        #region WaitForEndOfFrame
        public static STask WaitForEndOfFrame(MonoBehaviour coroutineRunner, CancellationToken cancellationToken = default)
        {
            return new STask(WaitForEndOfFramePromise.Create(coroutineRunner, cancellationToken, out var token), token);
        }
        
        #if UNITY_2023_1_OR_NEWER
        public static async STask WaitForEndOfFrame(CancellationToken cancellationToken = default)
        {
            await Awaitable.EndOfFrameAsync(cancellationToken);
        }
        #endif
        
        private sealed class WaitForEndOfFramePromise : ISTaskSource, ITaskPoolNode<WaitForEndOfFramePromise>, System.Collections.IEnumerator
        {
            private static TaskPool<WaitForEndOfFramePromise> pool;
            private WaitForEndOfFramePromise nextNode;
            public ref WaitForEndOfFramePromise NextNode => ref this.nextNode;

            static WaitForEndOfFramePromise()
            {
                TaskPool.RegisterSizeGetter(typeof(WaitForEndOfFramePromise), () => pool.Size);
            }

            private CancellationToken cancellationToken;
            private STaskCompletionSourceCore<object> core;
            
            private WaitForEndOfFramePromise() { }

            public static ISTaskSource Create(MonoBehaviour coroutineRunner, CancellationToken cancellationToken, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AutoResetSTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);
                }

                if (!pool.TryPop(out var result))
                {
                    result = new WaitForEndOfFramePromise();
                }

                result.cancellationToken = cancellationToken;

                coroutineRunner.StartCoroutine(result);

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

            private bool TryReturn()
            {
                this.core.Reset();
                this.Reset(); // Reset Enumerator
                this.cancellationToken = default;
                return pool.TryPush(this);
            }
            
            // Coroutine Runner implementation

            private static readonly WaitForEndOfFrame waitForEndOfFrameYieldInstruction = new WaitForEndOfFrame();
            private bool isFirst = true;

            object System.Collections.IEnumerator.Current => waitForEndOfFrameYieldInstruction;
            
            bool System.Collections.IEnumerator.MoveNext()
            {
                if (this.isFirst)
                {
                    this.isFirst = false;
                    return true; // start WaitForEndOfFrame
                }

                if (this.cancellationToken.IsCancellationRequested)
                {
                    this.core.TrySetCanceled(this.cancellationToken);
                    return false;
                }

                this.core.TrySetResult(null);
                return false;
            }
            
            public void Reset()
            {
                this.isFirst = true;
            }
        }
        #endregion
    }

    public readonly struct YieldAwaitable
    {
        private readonly PlayerLoopTiming timing;

        public YieldAwaitable(PlayerLoopTiming timing)
        {
            this.timing = timing;
        }

        public Awaiter GetAwaiter()
        {
            return new Awaiter(this.timing);
        }

        public STask ToSTask()
        {
            return STask.Yield(this.timing, CancellationToken.None);
        }

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            private readonly PlayerLoopTiming timing;

            public Awaiter(PlayerLoopTiming timing)
            {
                this.timing = timing;
            }

            public bool IsCompleted => false;

            public void GetResult() { }

            public void OnCompleted(Action continuation)
            {
                PlayerLoopHelper.AddContinuation(this.timing, continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                PlayerLoopHelper.AddContinuation(this.timing, continuation);
            }
        }
    }
}