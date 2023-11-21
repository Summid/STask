using System;
using System.Collections.Generic;
using System.Threading;

namespace SFramework.Threading.Tasks.Internal
{
    /// <summary>
    /// 弱引用字典，允许 GC 回收字典中的 TKey，其与普通字典最大的区别是在 Add, Remove, Enumerate 操作中需判断引用对象是否还健在
    /// 线程安全
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    internal class WeakDictionary<TKey, TValue>
        where TKey : class
    {
        private Entry[] buckets; // the index of buckets is Entry.Hash
        private int size;
        private SpinLock gate; // mutable struct(not readonly)

        private readonly float loadFactor;
        private readonly IEqualityComparer<TKey> keyEqualityComparer;

        public WeakDictionary(int capacity = 4, float loadFactor = 0.75f, IEqualityComparer<TKey> keyComparer = null)
        {
            int tableSize = CalculateCapacity(capacity, loadFactor);
            this.buckets = new Entry[tableSize];
            this.loadFactor = loadFactor;
            this.gate = new SpinLock(false);
            this.keyEqualityComparer = keyComparer ?? EqualityComparer<TKey>.Default;
        }

        public bool TryAdd(TKey key, TValue value)
        {
            bool lockTaken = false;
            try
            {
                this.gate.Enter(ref lockTaken);
                return this.TryAddInternal(key, value);
            }
            finally
            {
                if (lockTaken)
                    this.gate.Exit(false);
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            bool lockTaken = false;
            try
            {
                this.gate.Enter(ref lockTaken);
                if (this.TryGetEntry(key, out _, out var entry))
                {
                    value = entry.Value;
                    return true;
                }

                value = default(TValue);
                return false;
            }
            finally
            {
                if (lockTaken)
                    this.gate.Exit(false);
            }
        }

        public bool TryRemove(TKey key)
        {
            bool lockTaken = false;
            try
            {
                this.gate.Enter(ref lockTaken);
                if (this.TryGetEntry(key, out var hashIndex, out var entry))
                {
                    this.Remove(hashIndex, entry);
                    return true;
                }

                return false;
            }
            finally
            {
                if (lockTaken)
                    this.gate.Exit(false);
            }
        }

        private bool TryAddInternal(TKey key, TValue value)
        {
            int nextCapacity = CalculateCapacity(this.size + 1, this.loadFactor);
            
            TRY_ADD_AGAIN:
                if (this.buckets.Length < nextCapacity)
                {
                    // copy original entries to nextBucket
                    var nextBucket = new Entry[nextCapacity];
                    for (int i = 0; i < this.buckets.Length; i++)
                    {
                        var e = this.buckets[i];
                        while (e != null)
                        {
                            this.AddToBuckets(nextBucket, key, e.Value, e.Hash);
                            e = e.Next;
                        }
                    }

                    this.buckets = nextBucket;
                    goto TRY_ADD_AGAIN;
                }
                else
                {
                    // add entry
                    bool successAdd = this.AddToBuckets(this.buckets, key, value, this.keyEqualityComparer.GetHashCode(key));
                    if (successAdd)
                        this.size++;
                    return successAdd;
                }
        }

        /// <summary>
        /// 添加新 entry 到 bucket，若 bucket[hashIndex] 处已有（未过期的）entry，则添加到链表尾部
        /// </summary>
        /// <param name="targetBuckets"></param>
        /// <param name="newKey"></param>
        /// <param name="value"></param>
        /// <param name="keyHash"></param>
        /// <returns></returns>
        private bool AddToBuckets(Entry[] targetBuckets, TKey newKey, TValue value, int keyHash)
        {
            int h = keyHash;
            int hashIndex = h & (targetBuckets.Length - 1); // 避免数组越界
            
            TRY_ADD_AGAIN:
                if (targetBuckets[hashIndex] == null) // 空链表，直接放
                {
                    targetBuckets[hashIndex] = new Entry
                    {
                        Key = new WeakReference<TKey>(newKey, false),
                        Value = value,
                        Hash = h
                    };

                    return true;
                }
                else
                {
                    // 非空链表，先判断该链表下对象是否在生命周期内，再把待添加对象放在链表尾部
                    var entry = targetBuckets[hashIndex];
                    while (entry != null)
                    {
                        if (entry.Key.TryGetTarget(out var target))
                        {
                            if (this.keyEqualityComparer.Equals(newKey, target))
                            {
                                return false; // duplicate
                            }
                        }
                        else
                        {
                            this.Remove(hashIndex, entry);
                            if (targetBuckets[hashIndex] == null) // 链表在执行 Remove 后变空，add again
                                goto TRY_ADD_AGAIN;
                        }

                        if (entry.Next != null)
                        {
                            entry = entry.Next; // foreach the linked list
                        }
                        else
                        {
                            // found last
                            entry.Next = new Entry()
                            {
                                Key = new WeakReference<TKey>(newKey, false),
                                Value = value,
                                Hash = h
                            };
                            entry.Next.Prev = entry;
                        }
                    }
                    
                    return false;
                }
        }
        
        private bool TryGetEntry(TKey key, out int hashIndex, out Entry entry)
        {
            var table = this.buckets;
            var hash = this.keyEqualityComparer.GetHashCode(key);
            hashIndex = hash & table.Length - 1; // 避免数组越界
            entry = table[hashIndex];

            while (entry != null)
            {
                if (entry.Key.TryGetTarget(out var target))
                {
                    if (this.keyEqualityComparer.Equals(key, target))
                    {
                        return true;
                    }
                }
                else
                {
                    // 已被 GC 回收，Remove
                    this.Remove(hashIndex, entry);
                }

                entry = entry.Next;
            }

            return false;
        }

        /// <summary>
        /// 移除 hashIndex 索引下的指定 entry，会自动整理 entry 链表
        /// </summary>
        /// <param name="hashIndex"><see cref="buckets"/>索引</param>
        /// <param name="entry">待删除entry</param>
        private void Remove(int hashIndex, Entry entry)
        {
            if (entry.Prev == null && entry.Next == null)
            {
                this.buckets[hashIndex] = null;
            }
            else
            {
                if (entry.Prev == null)
                {
                    this.buckets[hashIndex] = entry.Next;
                }
                if (entry.Prev != null)
                {
                    entry.Prev.Next = entry.Next;
                }
                if (entry.Next != null)
                {
                    entry.Next.Prev = entry.Prev;
                }
            }
            this.size--;
        }

        public List<KeyValuePair<TKey, TValue>> ToList()
        {
            var list = new List<KeyValuePair<TKey, TValue>>();
            this.ToList(ref list, false);
            return list;
        }

        /// <summary>
        /// 用<see cref="buckets"/>更新 list 的数据
        /// 更新后 list 中的对象可能被修改，数量可能会增加，若对象被 GC 回收则为 null
        /// </summary>
        /// <param name="list"></param>
        /// <param name="clear">若为 true，则全量更新 list</param>
        /// <returns></returns>
        public int ToList(ref List<KeyValuePair<TKey, TValue>> list, bool clear = true)
        {
            if (clear)
            {
                list.Clear();
            }

            int listIndex = 0;

            bool lockTaken = false;
            try
            {
                this.gate.Enter(ref lockTaken);
                for (int i = 0; i < this.buckets.Length; i++)
                {
                    var entry = this.buckets[i];
                    
                    // i 从 0 开始自增，因此 buckets[i] 可能为 null
                    // 遍历 hashIndex == i 下的 entry 链表
                    while (entry != null)
                    {
                        if (entry.Key.TryGetTarget(out var target))
                        {
                            var item = new KeyValuePair<TKey, TValue>(target, entry.Value);
                            if (listIndex < list.Count) // always true if clear is true
                            {
                                list[listIndex++] = item;
                            }
                            else
                            {
                                list.Add(item);
                                listIndex++;
                            }
                        }
                        else
                        {
                            // 已被 GC 回收，Remove
                            this.Remove(i, entry);
                        }
                        
                        entry = entry.Next;
                    }
                }
            }
            finally
            {
                if (lockTaken)
                    this.gate.Exit(false);
            }

            return listIndex;
        }
        
        /// <summary>
        /// 获取大于或等于一个整数的最小2的幂，具体思路可看<see cref="ArrayPool{T}.CalculateSize"/>
        /// </summary>
        /// <param name="collectionSize"></param>
        /// <param name="loadFactor"></param>
        /// <returns></returns>
        private static int CalculateCapacity(int collectionSize, float loadFactor)
        {
            var size = (int)(collectionSize / loadFactor);

            size--;
            size |= size >> 1;
            size |= size >> 2;
            size |= size >> 4;
            size |= size >> 8;
            size |= size >> 16;
            size += 1;

            if (size < 8)
            {
                size = 8;
            }
            return size;
        }

        private class Entry
        {
            public WeakReference<TKey> Key; // 仍然可被 GC 回收的对象的引用
            public TValue Value;
            public int Hash;
            public Entry Prev; // 用链表组织 Hash 相等的 Entry （Entry 在 buckets 中的索引为 Hash）
            public Entry Next;

            // debug only
            public override string ToString()
            {
                if (this.Key.TryGetTarget(out var target))
                {
                    return target + "(" + this.Count() + ")";
                }
                else
                {
                    return "(Dead)";
                }
            }

            private int Count()
            {
                int count = 1;
                Entry n = this;
                while (n.Next != null)
                {
                    count++;
                    n = n.Next;
                }
                return count;
            }
        }
    }
}