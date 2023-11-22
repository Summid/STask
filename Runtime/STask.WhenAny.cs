using SFramework.Threading.Tasks.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace SFramework.Threading.Tasks
{
    public partial struct STask
    {
#region WhenAny: hasResultLeft
        /// <summary>
        /// if left task win, 'hasResultLeft' set true, 'result' hold left task's result; if right task win, 'hasResultRight' set false, 'result' set default 
        /// </summary>
        /// <param name="leftTask"></param>
        /// <param name="rightTask"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static STask<(bool hasResultLeft, T result)> WhenAny<T>(STask<T> leftTask, STask<T> rightTask)
        {
            return new STask<(bool, T)>(new WhenAnyLRPromise<T>(leftTask, rightTask), 0);
        }

        private sealed class WhenAnyLRPromise<T> : ISTaskSource<(bool, T)>
        {
            private int completedCount;
            private STaskCompletionSourceCore<(bool, T)> core;

            public WhenAnyLRPromise(STask<T> leftTask, STask<T> rightTask)
            {
                TaskTracker.TrackActiveTask(this, 3);
                {
                    STask<T>.Awaiter awaiter;
                    try
                    {
                        awaiter = leftTask.GetAwaiter();
                    }
                    catch (Exception ex)
                    {
                        this.core.TrySetException(ex);
                        goto RIGHT;
                    }

                    if (awaiter.IsCompleted)
                    {
                        TryLeftInvokeContinuation(this, awaiter);
                    }
                    else
                    {
                        awaiter.SourceOnCompleted(state =>
                        {
                            using (var t = (StateTuple<WhenAnyLRPromise<T>, STask<T>.Awaiter>)state)
                            {
                                TryLeftInvokeContinuation(t.Item1, t.Item2);
                            }
                        }, StateTuple.Create(this, awaiter));
                    }
                }
            RIGHT:
                {
                    STask<T>.Awaiter awaiter;
                    try
                    {
                        awaiter = rightTask.GetAwaiter();
                    }
                    catch (Exception ex)
                    {
                        this.core.TrySetException(ex);
                        return;
                    }

                    if (awaiter.IsCompleted)
                    {
                        TryRightInvokeContinuation(this, awaiter);
                    }
                    else
                    {
                        awaiter.SourceOnCompleted(state =>
                        {
                            using (var t = (StateTuple<WhenAnyLRPromise<T>, STask<T>.Awaiter>)state)
                            {
                                TryRightInvokeContinuation(t.Item1, t.Item2);
                            }
                        }, StateTuple.Create(this, awaiter));
                    }
                }
            }

            private static void TryLeftInvokeContinuation(WhenAnyLRPromise<T> self, in STask<T>.Awaiter awaiter)
            {
                T result;
                try
                {
                    result = awaiter.GetResult();
                }
                catch (Exception ex)
                {
                    self.core.TrySetException(ex);
                    return;
                }

                if (Interlocked.Increment(ref self.completedCount) == 1)
                {
                    self.core.TrySetResult((true, result));
                }
            }
            
            private static void TryRightInvokeContinuation(WhenAnyLRPromise<T> self, in STask<T>.Awaiter awaiter)
            {
                try
                {
                    awaiter.GetResult();
                }
                catch (Exception ex)
                {
                    self.core.TrySetException(ex);
                    return;
                }

                if (Interlocked.Increment(ref self.completedCount) == 1)
                {
                    self.core.TrySetResult((false, default));
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
            public (bool, T) GetResult(short token)
            {
                TaskTracker.RemoveTracking(this);
                GC.SuppressFinalize(this);
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
        
#region WhenAny: Return value is winArgumentIndex and T result
        public static STask<(int winArgumentIndex, T result)> WhenAny<T>(params STask<T>[] tasks)
        {
            return new STask<(int, T)>(new WhenAnyPromise<T>(tasks, tasks.Length), 0);
        }

        public static STask<(int winArgumentIndex, T result)> WhenAny<T>(IEnumerable<STask<T>> tasks)
        {
            using (var span = ArrayPoolUtil.Materialize(tasks))
            {
                return new STask<(int, T)>(new WhenAnyPromise<T>(span.Array, span.Length), 0);
            }
        }
        
        private sealed class WhenAnyPromise<T> : ISTaskSource<(int, T)>
        {
            private int completedCount;
            private STaskCompletionSourceCore<(int, T)> core;

            public WhenAnyPromise(STask<T>[] tasks, int tasksLength)
            {
                if (tasksLength == 0)
                {
                    throw new ArgumentException("The tasks argument contains no tasks.");
                }

                TaskTracker.TrackActiveTask(this, 3);

                for (int i = 0; i < tasksLength; ++i)
                {
                    STask<T>.Awaiter awaiter;
                    try
                    {
                        awaiter = tasks[i].GetAwaiter();
                    }
                    catch (Exception ex)
                    {
                        this.core.TrySetException(ex);
                        continue; //继续检查其他 task
                    }

                    if (awaiter.IsCompleted)
                    {
                        TryInvokeContinuation(this, awaiter, i);
                    }
                    else
                    {
                        awaiter.SourceOnCompleted(state =>
                        {
                            using (var t = (StateTuple<WhenAnyPromise<T>, STask<T>.Awaiter, int>)state)
                            {
                                TryInvokeContinuation(t.Item1, t.Item2, t.Item3);
                            }
                        }, StateTuple.Create(this, awaiter, i));
                    }
                }
            }

            private static void TryInvokeContinuation(WhenAnyPromise<T> self, in STask<T>.Awaiter awaiter, int i)
            {
                T result;
                try
                {
                    result = awaiter.GetResult();
                }
                catch (Exception ex)
                {
                    self.core.TrySetException(ex);
                    return;
                }

                if (Interlocked.Increment(ref self.completedCount) == 1)
                {
                    self.core.TrySetResult((i, result));
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
            public (int, T) GetResult(short token)
            {
                TaskTracker.RemoveTracking(this);
                GC.SuppressFinalize(this);
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


#region WhenAny: Return value is winArgumentIndex
        public static STask<int> WhenAny(params STask[] tasks)
        {
            return new STask<int>(new WhenAnyPromise(tasks, tasks.Length), 0);
        }

        public static STask<int> WhenAny(IEnumerable<STask> tasks)
        {
            using (var span = ArrayPoolUtil.Materialize(tasks))
            {
                return new STask<int>(new WhenAnyPromise(span.Array, span.Length), 0);
            }
        }

        private sealed class WhenAnyPromise : ISTaskSource<int>
        {
            private int completedCount;
            private STaskCompletionSourceCore<int> core;

            public WhenAnyPromise(STask[] tasks, int tasksLength)
            {
                if (tasksLength == 0)
                {
                    throw new ArgumentException("The tasks argument contains no tasks.");
                }

                TaskTracker.TrackActiveTask(this, 3);

                for (int i = 0; i < tasksLength; ++i)
                {
                    STask.Awaiter awaiter;
                    try
                    {
                        awaiter = tasks[i].GetAwaiter();
                    }
                    catch (Exception ex)
                    {
                        this.core.TrySetException(ex);
                        continue; //继续检查其他 task
                    }

                    if (awaiter.IsCompleted)
                    {
                        TryInvokeContinuation(this, awaiter, i);
                    }
                    else
                    {
                        awaiter.SourceOnCompleted(state =>
                        {
                            using (var t = (StateTuple<WhenAnyPromise, STask.Awaiter, int>)state)
                            {
                                TryInvokeContinuation(t.Item1, t.Item2, t.Item3);
                            }
                        }, StateTuple.Create(this, awaiter, i));
                    }
                }
            }

            private static void TryInvokeContinuation(WhenAnyPromise self, in STask.Awaiter awaiter, int i)
            {
                try
                {
                    awaiter.GetResult();
                }
                catch (Exception ex)
                {
                    self.core.TrySetException(ex);
                    return;
                }

                if (Interlocked.Increment(ref self.completedCount) == 1)
                {
                    self.core.TrySetResult(i);
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
            public int GetResult(short token)
            {
                TaskTracker.RemoveTracking(this);
                GC.SuppressFinalize(this);
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
    }
}