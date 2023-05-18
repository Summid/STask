using System;
using System.Threading;

namespace SFramework.Threading.Tasks
{
    /// <summary>
    /// STask 没有类似 TaskScheduler 的 scheduler，该类只处理未处理的异常
    /// </summary>
    public static class STaskScheduler
    {
        public static event Action<Exception> UnobservedTaskException;

        /// <summary>
        /// 是否调用 UnobservedTaskException 来处理 OperationCanceledException；默认为 false
        /// </summary>
        public static bool PropagateOperationCanceledException = false;

        /// <summary>
        /// 捕获到未处理异常且没绑定 UnobservedTaskException 时，写到 UnityLog 的 LogType；默认为 Exception
        /// </summary>
        public static UnityEngine.LogType UnobservedExceptionWriteLogType = UnityEngine.LogType.Exception;

        /// <summary>
        /// 是否将异常发回Unity主线程；默认为 true
        /// </summary>
        public static bool DispatchUnityMainThread = true;

        private static void InvokeUnobservedTaskException(object state)
        {
            UnobservedTaskException((Exception)state);
        }

        /// <summary>
        /// 委托缓存
        /// </summary>
        private static readonly SendOrPostCallback handleExceptionInvoke = InvokeUnobservedTaskException;

        internal static void PublishUnobservedTaskException(Exception ex)
        {
            if (ex != null)
            {
                if (!PropagateOperationCanceledException && ex is OperationCanceledException)
                {
                    return;
                }

                if (UnobservedTaskException != null)
                {
                    if (!DispatchUnityMainThread || Thread.CurrentThread.ManagedThreadId == PlayerLoopHelper.MainThreadId)
                    {
                        UnobservedTaskException.Invoke(ex);
                    }
                    else
                    {
                        PlayerLoopHelper.UnitySynchronizationContext.Post(handleExceptionInvoke, ex);
                    }
                }
                else
                {
                    string msg = null;
                    if (UnobservedExceptionWriteLogType != UnityEngine.LogType.Exception)
                    {
                        msg = "UnobservedTaskException: " + ex.ToString();
                    }
                    switch (UnobservedExceptionWriteLogType)
                    {
                        case UnityEngine.LogType.Error:
                            UnityEngine.Debug.LogError(msg);
                            break;
                        case UnityEngine.LogType.Assert:
                            UnityEngine.Debug.LogAssertion(msg);
                            break;
                        case UnityEngine.LogType.Warning:
                            UnityEngine.Debug.LogWarning(msg);
                            break;
                        case UnityEngine.LogType.Log:
                            UnityEngine.Debug.Log(msg);
                            break;
                        case UnityEngine.LogType.Exception:
                            UnityEngine.Debug.LogException(ex);
                            break;
                        default:
                            break;
                    }
                }
            }
        }
    }
}