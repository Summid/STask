using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace SFramework.Threading.Tasks.Internal
{
    /// <summary>
    /// Same interface as System.Buffers.ArrayPool but only provides Shared.
    /// <see href="https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/Buffers/ArrayPool.cs"/>
    /// </summary>
    internal sealed class ArrayPool<T>
    {
        // Same size as System.Buffers.DefaultArrayPool<T>
        private const int DefaultMaxNumberOfArrayPerBucket = 50;

        private static readonly T[] EmptyArray = new T[0];

        public static readonly ArrayPool<T> Shared = new ArrayPool<T>();

        private readonly MinimumQueue<T[]>[] buckets;
        private readonly SpinLock[] locks;

        private ArrayPool()
        {
            //see: GetQueueIndex
            this.buckets = new MinimumQueue<T[]>[18];
            this.locks = new SpinLock[18];
            for (int i = 0; i < this.buckets.Length; ++i)
            {
                this.buckets[i] = new MinimumQueue<T[]>(4); //提前准备四个数组
                this.locks[i] = new SpinLock(false);
            }
        }

        public T[] Rent(int minimumLength)
        {
            if (minimumLength < 0)
            {
                throw new ArgumentOutOfRangeException("minimumLength");
            }
            else if (minimumLength == 0)
            {
                return EmptyArray;
            }

            int size = CalculateSize(minimumLength);
            int index = GetQueueIndex(size);
            if (index != -1)
            {
                var q = this.buckets[index];
                bool lockTaken = false;
                try
                {
                    this.locks[index].Enter(ref lockTaken);

                    if (q.Count != 0)
                    {
                        return q.Dequeue();
                    }
                }
                finally
                {
                    if (lockTaken)
                        this.locks[index].Exit(false);
                }
            }

            return new T[size];
        }

        public void Return(T[] array, bool clearArray = false)
        {
            if (array == null || array.Length == 0)
                return;

            int index = GetQueueIndex(array.Length);
            if (index != -1)
            {
                if (clearArray)
                {
                    Array.Clear(array, 0, array.Length);
                }

                var q = this.buckets[index];
                bool lockTaken = false;

                try
                {
                    this.locks[index].Enter(ref lockTaken);

                    if (q.Count > DefaultMaxNumberOfArrayPerBucket)
                        return;

                    q.Enqueue(array);
                }
                finally
                {
                    if (lockTaken)
                        this.locks[index].Exit(false);
                }
            }
        }

        /// <summary>
        /// get the 2^n ceil of the size
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        private static int CalculateSize(int size)
        {
            size--;             //避免 size 本身就是2的幂，计算后的结果却是 size*2 的情况
            size |= size >> 1;  //使得与最高位（含）紧邻的 2 位低位为 1
            size |= size >> 2;  //使得与最高位（含）紧邻的 4 位低位为 1
            size |= size >> 4;  //使得与最高位（含）紧邻的 8 位低位为 1
            size |= size >> 8;
            size |= size >> 16;
            size += 1;          //到此，最高位即其所有低位都变成 1 了（雾，我们再加 1 得到不小于 size 的最小 2次幂

            if (size < 8)//最小也补足到8，方便管理
            {
                size = 8;
            }

            return size;
        }
        
        /// <summary>
        /// get the index the array in the queue
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        private static int GetQueueIndex(int size)
        {
            switch (size)
            {
                case 8: return 0;
                case 16: return 1;
                case 32: return 2;
                case 64: return 3;
                case 128: return 4;
                case 256: return 5;
                case 512: return 6;
                case 1024: return 7;
                case 2048: return 8;
                case 4096: return 9;
                case 8192: return 10;
                case 16384: return 11;
                case 32768: return 12;
                case 65536: return 13;
                case 131072: return 14;
                case 262144: return 15;
                case 524288: return 16;
                case 1048576: return 17; // max array length
                default:
                    return -1;
            }
        }
    }
}