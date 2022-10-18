using System;
using System.Runtime.CompilerServices;

namespace SFramework.Threading.Tasks.Internal
{
    /// <summary>
    /// 简易版 Queue<T> 循环队列；只保留一种构造方法，去掉字段version、Contains方法、迭代器等用不到的内容
    /// 官方源码对比:<see cref="https://source.dot.net/#System.Collections.NonGeneric/System/Collections/Queue.cs"/>
    /// 为优化方法调用性能，一些方法声明为内联调用
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class MinimumQueue<T>
    {
        /// <summary> 每次扩容的最小值 </summary>
        private const int MinimumGrow = 4;

        /// <summary> 扩容系数 </summary>
        private const int GrowFactor = 200;

        private T[] array;
        /// <summary> 头指针，指向队列第一个元素 </summary>
        private int head;
        /// <summary> 尾指针，指向队列最后一个元素的后一个位置 </summary>
        private int tail;
        /// <summary> 当前队列的大小 </summary>
        private int size;

        public MinimumQueue(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException("capacity invalid");
            this.array = new T[capacity];
            this.head = this.tail = this.tail = 0;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return this.size; }
        }

        /// <summary>
        /// 取队首元素
        /// 队空时抛出异常
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 当 <see cref="size"/> 等于0时抛出
        /// </exception>
        /// <returns></returns>
        public T Peek()
        {
            if (this.size == 0)
                this.ThrowForEmptyQueue();
            return this.array[this.head];
        }

        /// <summary>
        /// 入队
        /// 会自动扩列
        /// </summary>
        /// <param name="item"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(T item)
        {
            if (this.size == this.array.Length)
            {
                this.Grow();
            }

            this.array[this.tail] = item;
            this.tail = (this.tail + 1) % this.array.Length;
            this.size++;
        }

        /// <summary>
        /// 出队
        /// 队空时抛出异常
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 当 <see cref="size"/> 等于0时抛出
        /// </exception>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Dequeue()
        {
            if (this.size == 0)
                this.ThrowForEmptyQueue();

            T removed = this.array[this.head];
            this.array[this.head] = default(T);
            this.head = (this.head + 1) % this.array.Length;
            this.size--;
            return removed;
        }

        /// <summary>
        /// 计算扩容大小，然后调用<see cref="SetCapacity(int)"/>进行扩容
        /// </summary>
        private void Grow()
        {
            int newCapacity = (int)((long)this.array.Length * (long)GrowFactor / 100);//GrowFactor默认值为200，即每次扩容大小为目前的两倍
            if (newCapacity < this.array.Length + MinimumGrow)
            {
                //最小扩容大小
                newCapacity = this.array.Length + MinimumGrow;
            }
            this.SetCapacity(newCapacity);
        }

        /// <summary>
        /// 扩容数组
        /// </summary>
        /// <param name="capacity"></param>
        private void SetCapacity(int capacity)
        {
            T[] newArray = new T[capacity];
            if (this.size > 0)
            {
                if (this.head < this.tail)
                {
                    //头指针在前，尾指针在后，即当前队列未发展成循环队列==>直接复制
                    Array.Copy(this.array, this.head, newArray, 0, this.size);
                }
                else
                {
                    //已经是循环队列的形状了
                    Array.Copy(this.array, this.head, newArray, 0, this.array.Length - this.head);//先复制头指针后面的
                    Array.Copy(this.array, 0, newArray, this.array.Length - this.head, this.tail);//再复制尾指针前面的
                }
            }

            this.array = newArray;
            this.head = 0;
            this.tail = this.size == capacity ? 0 : this.size;
            //size等于capacity时特殊处理，此时尾指针应该指向索引0位置（循环队列），从结果来看，扩容前后的数组没有区别
            //这个处理有点令人疑惑，因为要capacity与size相等几乎不可能（在MinimumGrow的限制下），但为了防止以后复制粘贴出错，还是加上
            //那么当capacity小于size时的情况呢？=> Array.Copy会抛出ArgumentException异常，因此这里就不处理了
        }


        private void ThrowForEmptyQueue()
        {
            throw new InvalidOperationException("Empty Queue");
        }
    }
}