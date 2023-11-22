using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace SFramework.Threading.Tasks.Internal
{
    // public for add user custom.
    
    public static class TaskTracker
    {
#if UNITY_EDITOR

        private static int trackingId = 0;
        private static bool dirty;
        
        public const string EnableAutoReloadKey = "STaskTrackerWindow_EnableAutoReloadKey";
        public const string EnableTrackingKey = "STaskTrackerWindow_EnableTrackingKey";
        public const string EnableStackTraceKey = "STaskTrackerWindow_EnableStackTraceKey";

        public static class EditorEnableState
        {
            private static bool enableAutoReload;

            public static bool EnableAutoReload
            {
                get { return enableAutoReload; }
                set
                {
                    enableAutoReload = value;
                    UnityEditor.EditorPrefs.SetBool(EnableAutoReloadKey, value);
                }
            }

            private static bool enableTracking;
            public static bool EnableTracking
            {
                get { return enableTracking; }
                set
                {
                    enableTracking = value;
                    UnityEditor.EditorPrefs.SetBool(EnableTrackingKey, value);
                }
            }

            private static bool enableStackTrace;
            public static bool EnableStackTrace
            {
                get { return enableStackTrace; }
                set
                {
                    enableStackTrace = value;
                    UnityEditor.EditorPrefs.SetBool(EnableStackTraceKey, value);
                }
            }
        }
        
#endif

        private static List<KeyValuePair<ISTaskSource, (string formattedType, int trackingId, DateTime addTime, string stackTrace)>> listPool
            = new List<KeyValuePair<ISTaskSource, (string formattedType, int trackingId, DateTime addTime, string stackTrace)>>();
        private static readonly WeakDictionary<ISTaskSource, (string formattedType, int trackingId, DateTime addTime, string stackTrace)> tracking
            = new WeakDictionary<ISTaskSource, (string formattedType, int trackingId, DateTime addTime, string stackTrace)>();

        [Conditional("UNITY_EDITOR")]
        public static void TrackActiveTask(ISTaskSource task, int skipFrame)
        {
#if UNITY_EDITOR
            dirty = true;
            if (!EditorEnableState.EnableTracking) return;
            string stackTrace = EditorEnableState.EnableStackTrace ? new StackTrace(skipFrame, true).CleanupAsyncStackTrace() : "";

            string typeName;
            if (EditorEnableState.EnableStackTrace)
            {
                var sb = new StringBuilder();
                TypeBeautify(task.GetType(), sb);
                typeName = sb.ToString();
            }
            else
            {
                typeName = task.GetType().Name;
            }
            tracking.TryAdd(task, (typeName, Interlocked.Increment(ref trackingId), DateTime.UtcNow, stackTrace));
#endif
        }

        [Conditional("UNITY_EDITOR")]
        public static void RemoveTracking(ISTaskSource task)
        {
#if UNITY_EDITOR
            dirty = true;
            if (!EditorEnableState.EnableTracking) return;
            bool success = tracking.TryRemove(task);
#endif
        }

        public static bool CheckAndResetDirty()
        {
            bool current = dirty;
            dirty = false;
            return current;
        }
        
        /// <summary>(trackingId, awaiterType, awaiterStatus, createdTime, stackTrace)</summary>
        public static void ForEachActiveTask(Action<int, string, STaskStatus, DateTime, string> action)
        {
            lock (listPool)
            {
                int count = tracking.ToList(ref listPool, clear: false);
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        action(listPool[i].Value.trackingId, listPool[i].Value.formattedType, listPool[i].Key.UnsafeGetStatus(), listPool[i].Value.addTime, listPool[i].Value.stackTrace);
                        listPool[i] = default;
                    }
                }
                catch
                {
                    listPool.Clear();
                    throw;
                }
            }
        }
        
        private static void TypeBeautify(Type type, StringBuilder sb)
        {
            if (type.IsNested)
            {
                sb.Append(type.DeclaringType.Name.ToString());
                sb.Append(".");
            }

            if (type.IsGenericType)
            {
                int genericsStart = type.Name.IndexOf("`");
                if (genericsStart != -1)
                {
                    sb.Append(type.Name.Substring(0, genericsStart));
                }
                else
                {
                    sb.Append(type.Name);
                }
                sb.Append("<");
                bool first = true;
                foreach (Type item in type.GetGenericArguments())
                {
                    if (!first)
                    {
                        sb.Append(", ");
                    }
                    first = false;
                    TypeBeautify(item, sb);
                }
                sb.Append(">");
            }
            else
            {
                sb.Append(type.Name);
            }
        }
    }
}