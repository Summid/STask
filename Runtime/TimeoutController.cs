using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace SFramework.Threading.Tasks
{
    // CancellationTokenSource itself can not reuse but CancelAfter(Timeout.InfiniteTimeSpan) allows reuse if did not reach timeout.
    // Similar discussion:
    // https://github.com/dotnet/runtime/issues/4694
    // https://github.com/dotnet/runtime/issues/48492
    // This TimeoutController emulate similar implementation, using CancelAfterSlim; to achieve zero allocation timeout.

    // 优化每次调用异步方法时，创建CTS的性能开销：
    // TimeoutController timeoutController = new TimeoutController(); // setup to field for reuse.
    //async STask FooAsync()
    //{
    //    try
    //    {
    //        // you can pass timeoutController.Timeout(TimeSpan) to cancellationToken.
    //        await UnityWebRequest.Get("http://foo").SendWebRequest()
    //            .WithCancellation(timeoutController.Timeout(TimeSpan.FromSeconds(5)));
    //        timeoutController.Reset(); // call Reset(Stop timeout timer and ready for reuse) when succeed.
    //    }
    //    catch (OperationCanceledException ex)
    //    {
    //        if (timeoutController.IsTimeout())
    //        {
    //            UnityEngine.Debug.Log("timeout");
    //        }
    //    }
    //}

    // TimeoutController可与其他CTS一起用，使用 new TimeoutController(CancellationToken)
    //TimeoutController timeoutController;
    //CancellationTokenSource clickCancelSource;
    //void Start()
    //{
    //    this.clickCancelSource = new CancellationTokenSource();
    //    this.timeoutController = new TimeoutController(clickCancelSource);
    //}

    public sealed class TimeoutController : IDisposable
    {
        private readonly static Action<object> CancelCancellationTokenSourceStateDelegate = new Action<object>(CancelCancellationTokenSourceState);

        private static void CancelCancellationTokenSourceState(object state)
        {
            var cts = (CancellationTokenSource)state;
            cts.Cancel();
        }

        /// <summary> 默认CTS </summary>
        private CancellationTokenSource timeoutSource;
        /// <summary> 默认CTS与用户自定义CTS </summary>
        private CancellationTokenSource linkedSource;
        private PlayerLoopTimer timer;
        private bool isDisposed;

        private readonly DelayType delayType;
        private readonly PlayerLoopTiming delayTiming;
        /// <summary> 用户自定义CTS </summary>
        private readonly CancellationTokenSource originalLinkCancellationTokenSource;

        public TimeoutController(DelayType delayType = DelayType.DeltaTime, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update)
        {
            this.timeoutSource = new CancellationTokenSource();
            this.originalLinkCancellationTokenSource = null;
            this.linkedSource = null;
            this.delayType = delayType;
            this.delayTiming = delayTiming;
        }

        public TimeoutController(CancellationTokenSource linkCancellationTokenSource, DelayType delayType = DelayType.DeltaTime, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update)
        {
            this.timeoutSource = new CancellationTokenSource();
            this.originalLinkCancellationTokenSource = linkCancellationTokenSource;
            this.linkedSource = CancellationTokenSource.CreateLinkedTokenSource(this.timeoutSource.Token, linkCancellationTokenSource.Token);
            this.delayType = delayType;
            this.delayTiming = delayTiming;
        }

        public CancellationToken Timeout(int millisecondsTimeout)
        {
            return this.Timeout(TimeSpan.FromMilliseconds(millisecondsTimeout));
        }

        public CancellationToken Timeout(TimeSpan timeout)
        {
            if (this.originalLinkCancellationTokenSource != null && this.originalLinkCancellationTokenSource.IsCancellationRequested)
            {
                //用户自定义CTS已经被用户取消，则不能再重新使用
                return this.originalLinkCancellationTokenSource.Token;
            }

            //倒计时结束，创建新CTS和timer
            if (this.timeoutSource.IsCancellationRequested)
            {
                this.timeoutSource.Dispose();
                this.timeoutSource = new CancellationTokenSource();
                if (this.linkedSource != null)
                {
                    this.linkedSource.Cancel();
                    this.linkedSource.Dispose();
                    this.linkedSource = CancellationTokenSource.CreateLinkedTokenSource(this.timeoutSource.Token, this.originalLinkCancellationTokenSource.Token);
                }

                this.timer?.Dispose();
                this.timer = null;
            }

            var useSource = (this.linkedSource != null) ? this.linkedSource : this.timeoutSource;
            var token = useSource.Token;
            if (this.timer == null)
            {
                // Timer complete => timeoutSource.Cancel() => linkedSource will be canceld as well
                // when (linked)token is canceled => stop timer
                this.timer = PlayerLoopTimer.StartNew(timeout, false, this.delayType, this.delayTiming, token, CancelCancellationTokenSourceStateDelegate, this.timeoutSource);
            }
            else
            {
                this.timer.Restart(timeout);
            }

            return token;
        }

        public bool IsTimeout()
        {
            return this.timeoutSource.IsCancellationRequested;
        }

        public void Reset()
        {
            this.timer?.Stop();
        }

        public void Dispose()
        {
            if (this.isDisposed)
                return;

            try
            {
                // stop timer
                this.timer?.Dispose();

                // cancel and dispose
                this.timeoutSource.Cancel();
                this.timeoutSource.Dispose();
                if (this.linkedSource != null)
                {
                    this.linkedSource.Cancel();
                    this.linkedSource.Dispose();
                }
            }
            finally
            {
                this.isDisposed = true;
            }
        }
    }
}