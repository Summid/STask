using System;
using System.Diagnostics;

namespace SFramework.Threading.Tasks.Internal
{
    /// <summary>
    /// 值类型版本的 Stopwatch，在 ASP.NET Core 中已有实现
    /// <see href="https://github.com/dotnet/aspnetcore/blob/main/src/Shared/ValueStopwatch/ValueStopwatch.cs"/>
    /// </summary>
    public readonly struct ValueStopwatch
    {
        //Stopwatch.Frequency，一秒内有多少个 Stopwatch 的 tick
        //一个 Stopwatch 的 tick 包含了多少个 TimeSpan 的 tick
        private static double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        private readonly long startTimestamp;

        public static ValueStopwatch StartNew() => new ValueStopwatch(Stopwatch.GetTimestamp());//Stopwatch.GetTimestamp()当前Stopwatch已走过多少tick

        private ValueStopwatch(long startTimestamp)
        {
            this.startTimestamp = startTimestamp;
        }

        public TimeSpan Elapsed => TimeSpan.FromTicks(this.ElapsedTicks);

        public bool IsInvalid => this.startTimestamp == 0;

        /// <summary>
        /// Elapsed Ticks in TimeSpan
        /// </summary>
        public long ElapsedTicks
        {
            get
            {
                if (this.startTimestamp == 0)
                {
                    throw new InvalidOperationException("Detected invalid initialization(use 'default'), only to create from StartNew()");
                }

                long delta = Stopwatch.GetTimestamp() - this.startTimestamp;
                return (long)(delta * TimestampToTicks);//转换为 TimeSpan 的 tick
            }
        }
    }
}