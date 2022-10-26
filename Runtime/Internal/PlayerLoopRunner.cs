using System;
using UnityEngine;

namespace SFramework.Threading.Tasks.Internal
{
    /// <summary>
    /// PlayerLoopSystem的迭代功能的实现，字如其人。
    /// 多线程安全
    /// </summary>
    /// 
    /// <remarks>
    /// 被迭代对象可能存在于多个（Unity）生命周期中，迭代是否完成由IPlayerLoopItem实现。
    /// 由于不确定IPlayerLoopItem.MoveNext()的执行结果，可能会有比较多Item存在于数组中，迭代一次需要花费较多时间，因此这里用混合多线程锁（Monitor）
    /// </remarks>
    internal sealed class PlayerLoopRunner
    {
        const int InitialSize = 16;

        /// <summary> 当前Runner在哪个timing迭代 </summary>
        private readonly PlayerLoopTiming timing;

        /// <summary> <see cref="running"/> 与 <see cref="waitQueue"/>线程锁 </summary>
        private readonly object runningAndQueueLock = new object();

        /// <summary> <see cref="loopItems"/>线程锁 </summary>
        private readonly object arrayLock = new object();

        /// <summary> IPlayerLoopItem.MoveNext()中出现异常时的兜底处理 </summary>
        private readonly Action<Exception> unhandleExceptionCallback;

        /// <summary> <see cref="loopItems"/>尾指针，指向最后一个元素的后一个位置（无元素时为0） </summary>
        private int tail = 0;
        private bool running = false;
        private IPlayerLoopItem[] loopItems = new IPlayerLoopItem[InitialSize];

        /// <summary> 当Runner在Run时，有Item入队，将暂时保存在该队列里，Run完再整理到loopItems里 </summary>
        private MinimumQueue<IPlayerLoopItem> waitQueue = new MinimumQueue<IPlayerLoopItem>(InitialSize);

        public PlayerLoopRunner(PlayerLoopTiming timing)
        {
            this.unhandleExceptionCallback = ex => Debug.LogException(ex);
            this.timing = timing;
        }

        /// <summary>
        /// 添加 IPlayerLoopItem 对象，在对应 <see cref="timing"/> 调用其 MoveNext() 来迭代
        /// </summary>
        /// <param name="item"></param>
        public void AddAction(IPlayerLoopItem item)
        {
            lock (this.runningAndQueueLock)
            {
                //当Runner在Run，将入队的Item暂存在队列里
                if (this.running)
                {
                    this.waitQueue.Enqueue(item);
                    return;
                }
            }

            lock (this.arrayLock)
            {
                if (this.loopItems.Length == this.tail)
                {
                    //当数组里没空位了，扩容；扩容大小为目前的两倍，对 tail*2 进行溢出检查，当数值溢出时将抛出异常
                    Array.Resize(ref this.loopItems, checked(this.tail * 2));
                }
                this.loopItems[this.tail++] = item;
            }
        }

        /// <summary>
        /// 清除所有 IPlayerLoopItem 对象，并返回清除对象的数量
        /// </summary>
        /// <returns></returns>
        public int Clear()
        {
            lock (this.arrayLock)
            {
                int rest = 0;

                for (int index = 0; index < this.loopItems.Length; index++)
                {
                    if (this.loopItems[index] != null)
                    {
                        rest++;
                    }

                    this.loopItems[index] = null;
                }

                this.tail = 0;
                return rest;
            }
        }

        /// <summary>
        /// 委托订阅方法
        /// </summary>
        public void Run()
        {
            //为了调试方便，创建堆栈名
#if DEBUG
            switch (this.timing)
            {
                case PlayerLoopTiming.Initialization:
                    this.Initialization();
                    break;
                case PlayerLoopTiming.LastInitialization:
                    this.LastInitialization();
                    break;
                case PlayerLoopTiming.EarlyUpdate:
                    this.EarlyUpdate();
                    break;
                case PlayerLoopTiming.LastEarlyUpdate:
                    this.LastEarlyUpdate();
                    break;
                case PlayerLoopTiming.FixedUpdate:
                    this.FixedUpdate();
                    break;
                case PlayerLoopTiming.LastFixedUpdate:
                    this.LastFixedUpdate();
                    break;
                case PlayerLoopTiming.PreUpdate:
                    this.PreUpdate();
                    break;
                case PlayerLoopTiming.LastPreUpdate:
                    this.LastPreUpdate();
                    break;
                case PlayerLoopTiming.Update:
                    this.Update();
                    break;
                case PlayerLoopTiming.LastUpdate:
                    this.LastUpdate();
                    break;
                case PlayerLoopTiming.PreLateUpdate:
                    this.PreLateUpdate();
                    break;
                case PlayerLoopTiming.LastPreLateUpdate:
                    this.LastPreLateUpdate();
                    break;
                case PlayerLoopTiming.PostLateUpdate:
                    this.PostLateUpdate();
                    break;
                case PlayerLoopTiming.LastPostLateUpdate:
                    this.LastPostLateUpdate();
                    break;

                //需2020.2更新版本支持
                case PlayerLoopTiming.TimeUpdate:
                    this.TimeUpdate();
                    break;

                //需2020.2更新版本支持
                case PlayerLoopTiming.LastTimeUpdate:
                    this.LastTimeUpdate();
                    break;
                default:
                    break;
            }
#else
            this.RunCore();
#endif
        }

#region DEBUG转发
        private void Initialization() => this.RunCore();
        private void LastInitialization() => this.RunCore();
        private void EarlyUpdate() => this.RunCore();
        private void LastEarlyUpdate() => this.RunCore();
        private void FixedUpdate() => this.RunCore();
        private void LastFixedUpdate() => this.RunCore();
        private void PreUpdate() => this.RunCore();
        private void LastPreUpdate() => this.RunCore();
        private void Update() => this.RunCore();
        private void LastUpdate() => this.RunCore();
        private void PreLateUpdate() => this.RunCore();
        private void LastPreLateUpdate() => this.RunCore();
        private void PostLateUpdate() => this.RunCore();
        private void LastPostLateUpdate() => this.RunCore();
        //需2020.2更新版本支持
        private void TimeUpdate() => this.RunCore();
        //需2020.2更新版本支持
        private void LastTimeUpdate() => this.RunCore();
#endregion

        [System.Diagnostics.DebuggerHidden]
        private void RunCore()
        {
            lock (this.runningAndQueueLock)
            {
                this.running = true;
            }

            lock (this.arrayLock)
            {
                int j = this.tail - 1;//数组中最后一个元素的索引

                for (int i = 0; i < this.loopItems.Length; i++)
                {
                    IPlayerLoopItem item = this.loopItems[i];
                    if (item != null)
                    {
                        try
                        {
                            if (!item.MoveNext())
                            {
                                //item迭代结束
                                this.loopItems[i] = null;
                            }
                            else
                            {
                                //检查下一个item
                                continue;
                            }
                        }
                        catch(Exception ex)
                        {
                            this.loopItems[i] = null;
                            try
                            {
                                this.unhandleExceptionCallback(ex);//将异常传递到UnityLog
                            }
                            catch { }
                        }
                    }

                    //当有item迭代结束，从数组尾巴往前检查，item后面的对象；此时 i 与 i 之前的 item 都已检查过
                    //这么做的目的是为了优化 移除已迭代完毕的item 这一步骤
                    while (i < j)
                    {
                        IPlayerLoopItem fromTail = this.loopItems[j];
                        if (fromTail != null)
                        {
                            try
                            {
                                if (!fromTail.MoveNext())
                                {
                                    //当该item结束迭代
                                    this.loopItems[j] = null;
                                    j--;
                                    continue;//检查下一个 j，舍弃当前 j 位置的对象
                                }
                                else
                                {
                                    //当该item还需下次迭代，将其放到 i 位置，因为 i 位置的对象已经不需再迭代，相当于删除 i 位置对象
                                    this.loopItems[i] = fromTail;
                                    this.loopItems[j] = null;
                                    j--;
                                    goto NEXT_LOOP;//检查下一个 i；
                                }
                            }
                            catch (Exception ex)
                            {
                                this.loopItems[j] = null;
                                j--;
                                try
                                {
                                    this.unhandleExceptionCallback(ex);
                                }
                                catch { }
                                continue;//检查下一个 j
                            }
                        }
                    }

                    this.tail = i;//循环结束，每个item只检查一次；此时 i 之前的 item （不包括i）都是需要下次迭代的
                    break;//循环结束

                NEXT_LOOP:
                    continue;
                }

                //将等待队列里的item安排到数组里
                lock (this.runningAndQueueLock)
                {
                    this.running = false;
                    while (this.waitQueue.Count != 0)
                    {
                        if (this.loopItems.Length == this.tail)
                        {
                            //故技重施，扩容
                            Array.Resize(ref this.loopItems, checked(this.tail * 2));
                        }
                        this.loopItems[this.tail++] = this.waitQueue.Dequeue();
                    }
                }
            }
        }
    }
}