using System;
using System.Runtime.CompilerServices;

namespace SFramework.Threading.Tasks.Internal
{
    internal static class Error
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowArgumentNullException<T>(T value, string paramName) where T : class
        {
            if (value == null)
                ThrowArgumentNullExceptionCore(paramName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowArgumentNullExceptionCore(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Exception ArgumentOutOfRange(string paramName)
        {
            return new ArgumentOutOfRangeException(paramName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Exception NoElements()
        {
            return new InvalidOperationException("Source sequence doesn't contain any elements.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Exception MoreThanOneElement()
        {
            return new InvalidOperationException("Source sequence contains more than one element.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowArgumentException<T>(string message)
        {
            throw new ArgumentException(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowNotYetCompleted()
        {
            throw new InvalidOperationException("Not yet completed.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ThrowNotYetCompleted<T>()
        {
            throw new InvalidOperationException("Not yet Completed.");
        }

        /// <summary>
        /// 若continuation已被注册，抛出异常
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="continuationField"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowWhenContinuationIsAlreadyRegistered<T>(T continuationField) where T : class
        {
            if (continuationField != null)
                ThrowInvalidOperationExceptionCore("continuation is already registered.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowInvalidOperationExceptionCore(string message)
        {
            throw new InvalidOperationException(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowOperationCanceledException()
        {
            throw new OperationCanceledException();
        }
    }
}