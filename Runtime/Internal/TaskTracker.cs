using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SFramework.Threading.Tasks.Internal
{
    // public for add user custom.
    
    public static class TaskTracker
    {
#if UNITY_EDITOR
        
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
    }
}