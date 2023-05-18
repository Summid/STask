using System;
using System.Runtime.CompilerServices;

namespace SFramework.Threading.Tasks.Internal
{
    /// <summary>
    /// 池化委托，调用后自动放回对象池
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class PooledDelegate<T> : ITaskPoolNode<PooledDelegate<T>>
    {
        private static TaskPool<PooledDelegate<T>> pool;

        PooledDelegate<T> nextNode;
        public ref PooledDelegate<T> NextNode => ref this.nextNode;

        static PooledDelegate()
        {
            TaskPool.RegisterSizeGetter(typeof(PooledDelegate<T>), () => pool.Size);
        }

        private readonly Action<T> runDelegate;//用泛型Action包装下给用户
        private Action continuation;//真正的continuation，不用关心泛型

        private PooledDelegate()
        {
            this.runDelegate = this.Run;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Run(T _)
        {
            Action call = this.continuation;
            this.continuation = null;
            if(call != null)
            {
                pool.TryPush(this);
                call.Invoke();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Action<T> Create(Action continuation)
        {
            if (!pool.TryPop(out PooledDelegate<T> self))
            {
                self = new PooledDelegate<T>();
            }

            self.continuation = continuation;
            return self.runDelegate;
        }
    }
}