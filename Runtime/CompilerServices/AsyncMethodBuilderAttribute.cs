namespace System.Runtime.CompilerServices
{
#if !NET_STANDARD_2_1
    internal sealed class AsyncMethodBuilderAttribute : Attribute
    {
        public Type BuilderType { get; }

        public AsyncMethodBuilderAttribute(Type type)
        {
            this.BuilderType = type;
        }
    }
#endif
}