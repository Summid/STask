#if UNITY_2023_1_OR_NEWER

namespace SFramework.Threading.Tasks
{
    public static class AwaitableExtensions
    {
        public static async STask AsSTask(this UnityEngine.Awaitable awaitable)
        {
            await awaitable;
        }

        public static async STask<T> AsSTask<T>(this UnityEngine.Awaitable<T> awaitable)
        {
            return await awaitable;
        }
    }
}
#endif