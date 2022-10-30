using System;
using System.Threading;

namespace SFramework.Threading.Tasks.Internal
{
    /// <summary>
    /// PlaeryLoopSystem的迭代功能的实现，与PlayerLoopRunner不同，这里的迭代对象只执行一次。
    /// 多线程安全
    /// </summary>
    /// 
    /// <remarks>
    /// 迭代对象为委托，由于只执行一次，迭代数组中的元素一般来说比较少，这里用自旋线程锁（SpinLock），且多用在Yield、线程切换上
    /// </remarks>
    internal sealed class ContinuationQueue
    {
        /// <summary>
        /// 数组可容纳的最多元素数，这里即Int32.MaxValue，大约二十多亿
        /// <see href="https://learn.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/runtime/gcallowverylargeobjects-element?redirectedfrom=MSDN"/>参考
        /// 在 .NET6 之后，新增了属性Array.MaxLength
        /// </summary>
        private const int MaxArrayLength = 0X7EFFFFF;

        /// <summary> 数组初始长度 </summary>
        private const int InitialSize = 16;

        /// <summary> 当前Queue在哪个timing迭代 </summary>
        private readonly PlayerLoopTiming timing;

        private SpinLock gate = new SpinLock(false);
        private bool dequing = false;

        /// <summary> 当前该队列存放的对象数量，小于等于Length属性 </summary>
        private int actionListCount = 0;
        private Action[] actionList = new Action[InitialSize];

        /// <summary> 当前该队列存放的对象数量，小于等于Length属性 </summary>
        private int waitingListCount = 0;
        private Action[] waitingList = new Action[InitialSize];//当队列正在迭代元素时，进队的元素暂存在这里

        public ContinuationQueue(PlayerLoopTiming timing)
        {
            this.timing = timing;
        }

        /// <summary>
        /// 入队
        /// </summary>
        /// <param name="continuation"></param>
        public void Enqueue(Action continuation)
        {
            bool lockTaken = false;
            try
            {
                this.gate.Enter(ref lockTaken);

                if (this.dequing)
                {
                    //正在迭代
                    if (this.waitingList.Length == this.waitingListCount)
                    {
                        //扩容
                        int newLength = this.waitingListCount * 2;
                        if ((uint)newLength > MaxArrayLength) newLength = MaxArrayLength;//确保不溢出，且无异常触发

                        Action[] newArray = new Action[newLength];
                        Array.Copy(this.waitingList, newArray, this.waitingListCount);
                    }
                    this.waitingList[this.waitingListCount] = continuation;//入队
                    this.waitingListCount++;//注意与Array.Length区分
                }
                else
                {
                    if (this.actionList.Length == this.actionListCount)
                    {
                        //扩容
                        int newLength = this.actionListCount * 2;
                        if((uint)newLength>MaxArrayLength) newLength = MaxArrayLength;

                        Action[] newArray = new Action[newLength];
                        Array.Copy(this.actionList, newArray, actionListCount);
                    }
                    this.actionList[this.actionListCount] = continuation;
                    this.actionListCount++;
                }
            }
            finally
            {
                if(lockTaken) this.gate.Exit(false);
            }
        }

        /// <summary>
        /// 清理队列，返回清理前队列中的元素个数
        /// </summary>
        /// <returns></returns>
        public int Clear()
        {
            int rest = this.actionListCount + this.waitingListCount;

            this.actionListCount = 0;
            this.actionList = new Action[InitialSize];

            this.waitingListCount = 0;
            this.waitingList = new Action[InitialSize];

            return rest;
        }

        // 委托订阅方法
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
            RunCore();
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
            {
                //关上actionList的大门
                bool lockTaken = false;
                try
                {
                    this.gate.Enter(ref lockTaken);
                    if (this.actionListCount == 0) return;
                    this.dequing = true;
                }
                finally
                {
                    if (lockTaken) this.gate.Exit(false);
                }
            }

            //迭代
            for (int i = 0; i < this.actionListCount; i++)
            {
                Action action = this.actionList[i];
                this.actionList[i] = null;
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }

            {
                //actionList再次营业
                bool lockTaken = false;
                try
                {
                    this.gate.Enter(ref lockTaken);
                    this.dequing = false;

                    //把等待数组里的元素倒腾到工作数组中
                    Action[] swapTempActionList = this.actionList;

                    this.actionListCount = this.waitingListCount;
                    this.actionList = this.waitingList;

                    this.waitingListCount = 0;
                    this.waitingList = swapTempActionList;
                }
                finally
                {
                    if(lockTaken) this.gate.Exit(false);
                }
            }
        }
    }
}