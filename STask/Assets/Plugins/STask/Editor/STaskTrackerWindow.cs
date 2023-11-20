using System;
using UnityEditor;

namespace SFramework.Threading.Tasks.Editor
{
    public class STaskTrackerWindow : EditorWindow
    {
        private static STaskTrackerWindow window;

        [MenuItem("Window/STask Tracker")]
        public static void OpenWindow()
        {
            if (window != null)
            {
                window.Close();
            }
            
            // will called OnEnable(singleton instance will be set).
            GetWindow<STaskTrackerWindow>("STask Tracker").Show();
        }

        private object splitterState;

        private void OnEnable()
        {
            window = this;// set singleton.
            this.splitterState = SplitterGUILayout.CreateSplitterState(new float[] { 75f, 25f }, new int[] { 32, 32 }, null);
        }
    }
}