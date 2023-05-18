using System;
using System.Threading;

namespace SFramework.Threading.Tasks
{
    public static partial class CancellationTokenSourceExtensions
    {
        private readonly static Action<object> CancelCancellationTokenSourceStateDelegate = new Action<object>(CancelCancellationTokenSourceState);

        private static void CancelCancellationTokenSourceState(object state)
        {
            var cts = (CancellationTokenSource)state;
            cts.Cancel();
        }

        /// <summary>
        /// 取代 <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/>，改为单线程
        /// </summary>
        public static IDisposable CancelAfterSilm(this CancellationTokenSource cts, TimeSpan delayTimeSpan, DelayType delayType = DelayType.DeltaTime, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update)
        {
            return PlayerLoopTimer.StartNew(delayTimeSpan, false, delayType, delayTiming, cts.Token, CancelCancellationTokenSourceStateDelegate, cts);
        }

        /// <summary>
        /// 取代 <see cref="CancellationTokenSource.CancelAfter(int)"/>，改为单线程
        /// </summary>
        public static IDisposable CancelAfterSilm(this CancellationTokenSource cts, int millisecondsDelay, DelayType delayType = DelayType.DeltaTime, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update)
        {
            return CancelAfterSilm(cts, TimeSpan.FromMilliseconds(millisecondsDelay), delayType, delayTiming);
        }
    }
}