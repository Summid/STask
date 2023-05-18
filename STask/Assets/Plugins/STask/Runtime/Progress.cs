using SFramework.Threading.Tasks.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SFramework.Threading.Tasks
{
    /// <summary>
    /// 轻量级 IProgress[T] 工厂
    /// </summary>
    public static class Progress
    {
        public static IProgress<T> Create<T>(Action<T> handler)
        {
            if (handler == null)
                return NullProgress<T>.Instance;
            return new AnonymousProgress<T>(handler);
        }

        public static IProgress<T> CreateOnlyValueChanged<T>(Action<T> handler, IEqualityComparer<T> comparer = null)
        {
            if (handler == null)
                return NullProgress<T>.Instance;
            return new OnlyValueChangedProgress<T>(handler, comparer ?? UnityEqualityComparer.GetDefault<T>());
        }

        private sealed class NullProgress<T> : IProgress<T>
        {
            public static readonly IProgress<T> Instance = new NullProgress<T>();

            NullProgress() { }

            public void Report(T value) { }
        }

        private sealed class AnonymousProgress<T> : IProgress<T>
        {
            private readonly Action<T> action;

            public AnonymousProgress(Action<T> action)
            {
                this.action = action;
            }

            public void Report(T value)
            {
                this.action(value);
            }
        }

        private sealed class OnlyValueChangedProgress<T> : IProgress<T>
        {
            private readonly Action<T> action;
            private readonly IEqualityComparer<T> comparer;
            private bool isFirstCall;
            private T latesValue;

            public OnlyValueChangedProgress(Action<T> action, IEqualityComparer<T> comparer)
            {
                this.action = action;
                this.comparer = comparer;
                this.isFirstCall = true;
            }

            public void Report(T value)
            {
                if (this.isFirstCall)
                {
                    this.isFirstCall = false;
                }
                else if (this.comparer.Equals(value, this.latesValue))
                {
                    return;
                }

                this.latesValue = value;
                this.action(value);
            }
        }
    }
}