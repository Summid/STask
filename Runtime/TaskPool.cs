using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace SFramework.Threading.Tasks
{
    /// <summary>
    /// TaskPool工具类，提供获取对象池大小接口，不实现对象池功能
    /// </summary>
    public static class TaskPool
    {
        internal static int MaxPoolSize;

        private static Dictionary<Type, Func<int>> sizes = new Dictionary<Type, Func<int>>();

        static TaskPool()
        {
            //先从环境变量中寻找预定义的大小，若没找到则默认最大值
            try
            {
                string value = Environment.GetEnvironmentVariable("STASK_MAX_POOLSIZE");
                if (value != null)
                {
                    if (int.TryParse(value, out int size))
                    {
                        MaxPoolSize = size;
                        return;
                    }
                }
            }
            catch { }

            MaxPoolSize = int.MaxValue;
        }

        public static void SetMaxPoolSize(int maxPoolSize)
        {
            MaxPoolSize = maxPoolSize;
        }

        public static IEnumerable<(Type, int)> GetCacheSizeInfo()
        {
            lock (sizes)
            {
                foreach(var item in sizes)
                {
                    yield return (item.Key, item.Value());
                }
            }
        }

        public static void RegisterSizeGetter(Type type,Func<int> getSize)
        {
            lock (sizes)
            {
                sizes[type] = getSize;
            }
        }
    }

    public interface ITaskPoolNode<T>
    {
        ref T NextNode { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct TaskPool<T> where T : class, ITaskPoolNode<T>
    {
        private int gate;
        private int size;
        private T root;//root为链表头，出队与入队都在root位置操作

        public int Size => this.size;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPop(out T result)
        {
            //if gate equals comparand, it will be replaced by value, return original value in location1
            if (Interlocked.CompareExchange(ref this.gate, 1, 0) == 0)
            {
                T v = this.root;
                if (!(v is null))
                {
                    ref var nextNode = ref v.NextNode;
                    this.root = nextNode;
                    nextNode = null;
                    this.size--;
                    result = v;
                    //The `Volatile.Write` method forces the value in location to be written to at the point of the call.
                    //In addition, any earlier program-order loads and stores must occur before the call to Volatile.Write.
                    Volatile.Write(ref this.gate, 0);
                    return true;
                }

                Volatile.Write(ref this.gate, 0);
            }
            result = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPush(T item)
        {
            if (Interlocked.CompareExchange(ref this.gate, 1, 0) == 0)
            {
                if (this.size < TaskPool.MaxPoolSize)
                {
                    item.NextNode = this.root;
                    this.root = item;
                    this.size++;
                    Volatile.Write(ref this.gate, 0);
                    return true;
                }
                else
                {
                    Volatile.Write(ref this.gate, 0);
                }
            }
            return false;
        }
    }
}