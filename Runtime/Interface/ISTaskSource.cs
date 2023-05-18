using System;
using System.Runtime.CompilerServices;

namespace SFramework.Threading.Tasks
{
    public enum STaskStatus
    {
        /// <summary>任务执行中</summary>
        Pending = 0,
        /// <summary>任务执行成功</summary>
        Succeeded = 1,
        /// <summary>任务执行失败</summary>
        Faulted = 2,
        /// <summary>任务被取消</summary>
        Canceled = 3,
    }

    /// <summary>
    /// 在STask中（配合 Awaiter）干活的对象，类似 IValueTaskSource
    /// 实现该接口可修改STask的行为（任务何时结束，任务结果是多少），以此来扩展STask
    /// </summary>
    public interface ISTaskSource
    {
        STaskStatus GetStatus(short token);
        void OnCompleted(Action<object> continuation, object state, short token);
        void GetResult(short token);

        STaskStatus UnsafeGetStatus();//仅供 debug 使用
    }

    /// <summary>
    /// 有返回值版<see cref="ISTaskSource"/>，覆盖GetResult(short token)；
    /// 协变接口（out 修饰 T），支持 ISTaskSource[Base] = new IStaskSource[Derive]，T 只能用作函数返回值
    /// <see href="https://learn.microsoft.com/zh-cn/dotnet/csharp/programming-guide/concepts/covariance-contravariance/">参考链接</see>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ISTaskSource<out T> : ISTaskSource
    {
        new T GetResult(short token);
    }

    public static class STaskStatusExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCompleted(this STaskStatus status)
        {
            return status != STaskStatus.Pending;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCompletedSuccessfully(this STaskStatus status)
        {
            return status == STaskStatus.Succeeded;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCanceled(this STaskStatus status)
        {
            return status == STaskStatus.Canceled;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFaulted(this STaskStatus status)
        {
            return status == STaskStatus.Faulted;
        }
    }
}