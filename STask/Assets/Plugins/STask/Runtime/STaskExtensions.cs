using SFramework.Threading.Tasks.Internal;
using System;
using static SFramework.Threading.Tasks.STask;

namespace SFramework.Threading.Tasks
{
    public static partial class STaskExtensions
    {
        /// <summary>
        /// 直接调用 STask 方法不等待时使用，保证 STask 最后重置 token
        /// </summary>
        /// <param name="task"></param>
        public static void Forget(this STask task)
        {
            //不使用 await 等待 STask 方法时，状态机不会调用 awaiter 中的 (Unsafe)OnCompleted 方法
            //因此我们手动注册回调，回调中调用 awaiter.GetResult 重置 token （当 awaiter 已完成时也重置）

            Awaiter awaiter = task.GetAwaiter();
            if (awaiter.IsCompleted)
            {
                try
                {
                    awaiter.GetResult();
                }
                catch (Exception ex)
                {
                    STaskScheduler.PublishUnobservedTaskException(ex);
                }
            }
            else
            {
                awaiter.SourceOnCompleted(state =>
                {
                    using (StateTuple<Awaiter> t = (StateTuple<Awaiter>)state)//用完后自动调用 StateTuple.Dispose()
                    {
                        try
                        {
                            t.Item1.GetResult();
                        }
                        catch (Exception ex)
                        {
                            STaskScheduler.PublishUnobservedTaskException(ex);
                        }
                    }
                }, StateTuple.Create(awaiter));
            }
        }

        /// <summary>
        /// 直接调用 STask 方法不等待时使用，保证 STask 最后重置 token
        /// </summary>
        /// <param name="task"></param>
        public static void Forget<T>(this STask<T> task)
        {
            //不使用 await 等待 STask 方法时，状态机不会调用 awaiter 中的 (Unsafe)OnCompleted 方法
            //因此我们手动注册回调，回调中调用 awaiter.GetResult 重置 token （当 awaiter 已完成时也重置）

            STask<T>.Awaiter awaiter = task.GetAwaiter();
            if (awaiter.IsCompleted)
            {
                try
                {
                    awaiter.GetResult();
                }
                catch (Exception ex)
                {
                    STaskScheduler.PublishUnobservedTaskException(ex);
                }
            }
            else
            {
                awaiter.SourceOnCompleted(state =>
                {
                    using (StateTuple<STask<T>.Awaiter> t = (StateTuple<STask<T>.Awaiter>)state)//用完后自动调用 StateTuple.Dispose()
                    {
                        try
                        {
                            t.Item1.GetResult();
                        }
                        catch (Exception ex)
                        {
                            STaskScheduler.PublishUnobservedTaskException(ex);
                        }
                    }
                }, StateTuple.Create(awaiter));
            }
        }
    }
}