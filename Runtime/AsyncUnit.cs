using System;

namespace SFramework.Threading.Tasks
{
    /// <summary>
    /// <see cref="STaskCompletionSourceCore{TResult}"/> 的默认泛型类型，当无需返回值时，用来占坑，因此声明为 readonly
    /// </summary>
    public readonly struct AsyncUnit : IEquatable<AsyncUnit>
    {
        public static readonly AsyncUnit Default = new AsyncUnit();

        public override int GetHashCode()
        {
            return 0;//when Equals() returns true, the both of object must have the same GetHashCode() result; but the same hash code do NOT mean they're equal
        }

        public bool Equals(AsyncUnit other)
        {
            return true;//readonly struct, default true
        }

        public override string ToString()
        {
            return "()";
        }
    }
}