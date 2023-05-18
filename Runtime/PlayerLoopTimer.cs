using SFramework.Threading.Tasks.Internal;
using System;
using System.Threading;
using UnityEngine;

namespace SFramework.Threading.Tasks
{
    public abstract class PlayerLoopTimer : IDisposable, IPlayerLoopItem
    {
        private readonly CancellationToken cancellationToken;
        private readonly Action<object> timerCallback;
        private readonly object state;
        private readonly PlayerLoopTiming playerLoopTiming;
        private readonly bool periodic;

        private bool isRunning;
        private bool tryStop;
        private bool isDisposed;

        protected PlayerLoopTimer(bool periodic, PlayerLoopTiming playerLoopTiming, CancellationToken cancellationToken, Action<object> timerCallback, object state)
        {
            this.periodic = periodic;
            this.playerLoopTiming = playerLoopTiming;
            this.cancellationToken = cancellationToken;
            this.timerCallback = timerCallback;
            this.state = state;
        }

        public static PlayerLoopTimer Create(TimeSpan interval, bool periodic, DelayType delayType, PlayerLoopTiming playerLoopTiming, CancellationToken cancellationToken, Action<object> timerCallback, object state)
        {
#if UNITY_EDITOR
            //编辑器下强制使用RealTime
            if (PlayerLoopHelper.IsMainThread && !UnityEditor.EditorApplication.isPlaying)
            {
                delayType = DelayType.RealTime;
            }
#endif

            switch (delayType)
            {
                case DelayType.UnscaledDeltaTime:
                    return new IgnoreTimeScalePlayerLoopTimer(interval, periodic, playerLoopTiming, cancellationToken, timerCallback, state);
                case DelayType.RealTime:
                    return new RealtimePlayerLoopTimer(interval, periodic, playerLoopTiming, cancellationToken, timerCallback, state);
                case DelayType.DeltaTime:
                default:
                    return new DeltaTimePlayerLoopTimer(interval, periodic, playerLoopTiming, cancellationToken, timerCallback, state);
            }
        }

        public static PlayerLoopTimer StartNew(TimeSpan interval, bool periodic, DelayType delayType, PlayerLoopTiming playerLoopTiming, CancellationToken cancellationToken, Action<object> timerCallback, object state)
        {
            PlayerLoopTimer timer = Create(interval,periodic,delayType,playerLoopTiming, cancellationToken, timerCallback, state);
            timer.Restart();
            return timer;
        }

        /// <summary>
        /// Restart(Reset and Start) timer
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public void Restart()
        {
            if (this.isDisposed)
                throw new ObjectDisposedException(null);

            this.ResetCore(null);//初始化
            if (!this.isRunning)
            {
                this.isRunning = true;
                PlayerLoopHelper.AddAction(this.playerLoopTiming, this);
            }
            this.tryStop = false;
        }

        /// <summary>
        /// Restart(Reset and Start) and change interval
        /// </summary>
        /// <param name="interval"></param>
        /// <exception cref="ObjectDisposedException"></exception>
        public void Restart(TimeSpan interval)
        {
            if(this.isDisposed)
                throw new ObjectDisposedException(null);

            this.ResetCore(interval);//初始化
            if (!this.isRunning)
            {
                this.isRunning = true;
                PlayerLoopHelper.AddAction(this.playerLoopTiming, this);
            }
            this.tryStop = false;
        }

        /// <summary>
        /// Stop timer
        /// </summary>
        public void Stop()
        {
            this.tryStop = true;
        }

        protected abstract void ResetCore(TimeSpan? newInterval);

        protected abstract bool MoveNextCore();

        public void Dispose()
        {
            this.isDisposed = true;
        }

        bool IPlayerLoopItem.MoveNext()
        {
            if (this.isDisposed || this.tryStop || this.cancellationToken.IsCancellationRequested)
            {
                this.isRunning = false;
                return false;
            }

            if (!this.MoveNextCore())//迭代（计时）结束，调用回调
            {
                this.timerCallback(this.state);

                if (this.periodic)
                {
                    //周期性计时器，重新计时
                    this.ResetCore(null);
                    return true;
                }
                else
                {
                    //一次性计时器，生命周期结束
                    this.isRunning = false;
                    return false;
                }
            }

            return true;
        }
    }

    public sealed class DeltaTimePlayerLoopTimer : PlayerLoopTimer
    {
        private int initialFrame;
        private float elapsed;
        private float interval;

        public DeltaTimePlayerLoopTimer(TimeSpan interval, bool periodic, PlayerLoopTiming playerLoopTiming, CancellationToken cancellationToken, Action<object> timerCallback, object state)
            : base(periodic, playerLoopTiming, cancellationToken, timerCallback, state)
        {
            this.ResetCore(interval);
        }

        protected override bool MoveNextCore()
        {
            if (this.elapsed == 0.0f)
            {
                if (this.initialFrame == Time.frameCount)
                {
                    return true;
                }
            }

            this.elapsed += Time.deltaTime;
            if (this.elapsed >= this.interval)
            {
                return false;
            }

            return true;
        }

        protected override void ResetCore(TimeSpan? newInterval)
        {
            this.elapsed = 0.0f;
            this.initialFrame = PlayerLoopHelper.IsMainThread ? Time.frameCount : -1;
            if (newInterval != null)
            {
                this.interval = (float)newInterval.Value.TotalSeconds;
            }
        }
    }

    public sealed class IgnoreTimeScalePlayerLoopTimer : PlayerLoopTimer
    {
        private int initialFrame;
        private float elapsed;
        private float interval;

        public IgnoreTimeScalePlayerLoopTimer(TimeSpan interval, bool periodic, PlayerLoopTiming playerLoopTiming, CancellationToken cancellationToken, Action<object> timerCallback, object state)
            : base(periodic, playerLoopTiming, cancellationToken, timerCallback, state)
        {
            this.ResetCore(interval);
        }

        protected override bool MoveNextCore()
        {
            if (this.elapsed == 0.0f)
            {
                if (this.initialFrame == Time.frameCount)
                {
                    return true;
                }
            }

            this.elapsed += Time.unscaledDeltaTime;
            if (this.elapsed >= this.interval)
            {
                return false;
            }

            return true;
        }

        protected override void ResetCore(TimeSpan? newInterval)
        {
            this.elapsed = 0.0f;
            this.initialFrame = PlayerLoopHelper.IsMainThread ? Time.frameCount : -1;
            if (newInterval != null)
            {
                this.interval = (float)newInterval.Value.TotalSeconds;
            }
        }
    }

    public sealed class RealtimePlayerLoopTimer : PlayerLoopTimer
    {
        private ValueStopwatch stopwatch;
        /// <summary> ticks in TimeSpan </summary>
        private long intervalTicks;

        public RealtimePlayerLoopTimer(TimeSpan interval, bool periodic, PlayerLoopTiming playerLoopTiming, CancellationToken cancellationToken, Action<object> timerCallback, object state)
            : base(periodic, playerLoopTiming, cancellationToken, timerCallback, state)
        {
            this.ResetCore(interval);
        }

        protected override bool MoveNextCore()
        {
            if (this.stopwatch.ElapsedTicks >= this.intervalTicks)
            {
                return false;
            }

            return true;
        }

        protected override void ResetCore(TimeSpan? newInterval)
        {
            this.stopwatch = ValueStopwatch.StartNew();
            if (newInterval != null)
            {
                this.intervalTicks = newInterval.Value.Ticks;
            }
        }
    }
}