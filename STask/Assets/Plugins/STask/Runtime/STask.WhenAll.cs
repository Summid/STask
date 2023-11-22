using SFramework.Threading.Tasks.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace SFramework.Threading.Tasks
{
    public partial struct STask
    {
#region WhenAll<T>
        public static STask<T[]> WhenAll<T>(params STask<T>[] tasks)
        {
            if (tasks.Length == 0)
            {
                return STask.FromResult(Array.Empty<T>());
            }

            return new STask<T[]>(new WhenAllPromise<T>(tasks, tasks.Length), 0);
        }

        public static STask<T[]> WhenAll<T>(IEnumerable<STask<T>> tasks)
        {
            using (var span = ArrayPoolUtil.Materialize(tasks))
            {
                var promise = new WhenAllPromise<T>(span.Array, span.Length); // consumed array in constructor.
                return new STask<T[]>(promise, 0);
            }
        }

        private sealed class WhenAllPromise<T> : ISTaskSource<T[]>
        {
            private T[] result;
            private int completeCount;
            private STaskCompletionSourceCore<T[]> core;//won't reset, if called after GetResult, invoke TrySetException
            
            /*
             由于 WhenAllPromise 依靠的是外部提供的 STask 进行工作，因此没办法复用，无法池化，也就没有重置 core 的必要了
             当多次等待 WhenAll 时，会抛出 STask[] 中 STask 的 token 不匹配异常，不用担心多次等待的问题
             每次使用 WhenAll 都会有 GC ，少用为妙嗷
             */
            
            public WhenAllPromise(STask<T>[] tasks, int tasksLength)
            {
                TaskTracker.TrackActiveTask(this, 3);
                
                this.completeCount = 0;

                if (tasksLength == 0)
                {
                    this.result = Array.Empty<T>();
                    this.core.TrySetResult(this.result);
                    return;
                }

                this.result = new T[tasksLength];

                for (int i = 0; i < tasksLength; ++i)
                {
                    STask<T>.Awaiter awaiter;
                    try
                    {
                        awaiter = tasks[i].GetAwaiter();
                    }
                    catch (Exception exception)
                    {
                        this.core.TrySetException(exception);
                        continue;
                    }

                    if (awaiter.IsCompleted)//if await twice, throw exception in awaiter.core.ValidateToken
                    {
                        TryInvokeContinuation(this, awaiter, i);
                    }
                    else
                    {
                        //我们无法单独 await STask ，状态机就不会调用 STask.awaiter.OnCompleted ，因此我们要手动注册回调方法
                        //当 STask 完成后，调用 TryInvokeContinuation 计数
                        awaiter.SourceOnCompleted(state =>
                        {
                            using (var t = (StateTuple<WhenAllPromise<T>, STask<T>.Awaiter, int>)state)
                            {
                                TryInvokeContinuation(t.Item1, t.Item2, t.Item3);
                            }
                        }, StateTuple.Create(this, awaiter, i));
                    }
                }
            }
            
            private static void TryInvokeContinuation(WhenAllPromise<T> self, in STask<T>.Awaiter awaiter, int i)
            {
                try
                {
                    self.result[i] = awaiter.GetResult();
                }
                catch (Exception exception)
                {
                    self.core.TrySetException(exception); // 可能触发多次 await 异常
                    return;
                }

                if (Interlocked.Increment(ref self.completeCount) == self.result.Length)//所有STask都完成后，调用 TrySetResult
                {
                    self.core.TrySetResult(self.result);
                }
            }

            public STaskStatus GetStatus(short token)
            {
                return this.core.GetStatus(token);
            }
            
            public void OnCompleted(Action<object> continuation, object state, short token)
            {
                this.core.OnCompleted(continuation, state, token);
            }
            
            public T[] GetResult(short token)
            {
                TaskTracker.RemoveTracking(this);
                GC.SuppressFinalize(this);//这是在干什么，既没有手写异构函数也没有实现IDisposable接口——因此这句代码不会起作用
                return this.core.GetResult(token);
            }
            
            void ISTaskSource.GetResult(short token)
            {
                this.GetResult(token);
            }
            
            public STaskStatus UnsafeGetStatus()
            {
                return this.core.UnsafeGetStatus();
            }
        }

#endregion
        
#region WhenAll
        public static STask WhenAll(params STask[] tasks)
        {
            if (tasks.Length == 0)
            {
                return STask.CompletedTask;
            }

            return new STask(new WhenAllPromise(tasks, tasks.Length), 0);
        }

        public static STask WhenAll(IEnumerable<STask> tasks)
        {
            using (var span = ArrayPoolUtil.Materialize(tasks))
            {
                var promise = new WhenAllPromise(span.Array, span.Length);// consumed array in constructor.
                return new STask(promise, 0);
            }
        }

        private sealed class WhenAllPromise : ISTaskSource
        {
            private int completeCount;
            private int tasksLength;
            private STaskCompletionSourceCore<AsyncUnit> core;//won't reset, if called after GetResult, invoke TrySetException

            /*
             由于 WhenAllPromise 依靠的是外部提供的 STask 进行工作，因此没办法复用，无法池化，也就没有重置 core 的必要了
             当多次等待 WhenAll 时，会抛出 STask[] 中 STask 的 token 不匹配异常，不用担心多次等待的问题
             每次使用 WhenAll 都会有 GC ，少用为妙嗷
             */

            public WhenAllPromise(STask[] tasks, int tasksLength)
            {
                TaskTracker.TrackActiveTask(this, 3);
                
                this.tasksLength = tasksLength;
                this.completeCount = 0;

                if (tasksLength == 0)
                {
                    this.core.TrySetResult(AsyncUnit.Default);
                    return;
                }

                for (int i = 0; i < tasksLength; ++i)
                {
                    STask.Awaiter awaiter;
                    try
                    {
                        awaiter = tasks[i].GetAwaiter();
                    }
                    catch (Exception exception)
                    {
                        this.core.TrySetException(exception);
                        continue;
                    }

                    if (awaiter.IsCompleted)//if await twice, throw exception in awaiter.core.ValidateToken
                    {
                        TryInvokeContinuation(this, awaiter);
                    }
                    else
                    {
                        //我们无法单独 await STask ，状态机就不会调用 STask.awaiter.OnCompleted ，因此我们要手动注册回调方法
                        //当 STask 完成后，调用 TryInvokeContinuation 计数
                        awaiter.SourceOnCompleted(state =>
                        {
                            using (var t = (StateTuple<WhenAllPromise, STask.Awaiter>)state)
                            {
                                TryInvokeContinuation(t.Item1, t.Item2);
                            }
                        }, StateTuple.Create(this, awaiter));
                    }
                }
            }

            private static void TryInvokeContinuation(WhenAllPromise self, in STask.Awaiter awaiter)
            {
                try
                {
                    awaiter.GetResult();
                }
                catch (Exception exception)
                {
                    self.core.TrySetException(exception); // 可能触发多次 await 异常
                    return;
                }

                if (Interlocked.Increment(ref self.completeCount) == self.tasksLength)//所有STask都完成后，调用 TrySetResult
                {
                    self.core.TrySetResult(AsyncUnit.Default);
                }
            }

            public STaskStatus GetStatus(short token)
            {
                return this.core.GetStatus(token);
            }

            public void OnCompleted(Action<object> continuation, object state, short token)
            {
                this.core.OnCompleted(continuation, state, token);
            }

            public void GetResult(short token)
            {
                TaskTracker.RemoveTracking(this);
                GC.SuppressFinalize(this);//这是在干什么，既没有手写异构函数也没有实现IDisposable接口——因此这句代码不会起作用
                this.core.GetResult(token);
            }

            public STaskStatus UnsafeGetStatus()
            {
                return this.core.UnsafeGetStatus();
            }
        }
#endregion
    }
}