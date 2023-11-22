using SFramework.Threading.Tasks.Internal;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SFramework.Threading.Tasks.Editor
{
    public class STaskTrackerWindow : EditorWindow
    {
        private static int interval;
        
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

        private static readonly GUILayoutOption[] EmptyLayoutOption = new GUILayoutOption[0];
        
        private STaskTrackerTreeView treeView;
        private object splitterState;

        private void OnEnable()
        {
            window = this;// set singleton.
            this.splitterState = SplitterGUILayout.CreateSplitterState(new float[] { 75f, 25f }, new int[] { 32, 32 }, null);
            this.treeView = new STaskTrackerTreeView();
            TaskTracker.EditorEnableState.EnableAutoReload = EditorPrefs.GetBool(TaskTracker.EnableAutoReloadKey, false);
            TaskTracker.EditorEnableState.EnableTracking = EditorPrefs.GetBool(TaskTracker.EnableTrackingKey, false);
            TaskTracker.EditorEnableState.EnableStackTrace = EditorPrefs.GetBool(TaskTracker.EnableStackTraceKey, false);
        }

        private void OnGUI()
        {
            // Head
            this.RenderHeadPanel();

            // Splittable
            SplitterGUILayout.BeginVerticalSplit(this.splitterState, EmptyLayoutOption);
            {
                // Column Table
                this.RenderTable();
                
                // StackTrace details
                this.RenderDetailsPanel();
            }
            SplitterGUILayout.EndVerticalSplit();
        }

        #region HeadPanel

        public static bool EnableAutoReload => TaskTracker.EditorEnableState.EnableAutoReload;
        public static bool EnableTracking => TaskTracker.EditorEnableState.EnableTracking;
        public static bool EnableStackTrace => TaskTracker.EditorEnableState.EnableStackTrace;
        private static readonly GUIContent EnableAutoReloadHeadContent = EditorGUIUtility.TrTextContent("Enable AutoReload", "Reload automatically.");
        private static readonly GUIContent ReloadHeadContent = EditorGUIUtility.TrTextContent("Reload", "Reload View");
        private static readonly GUIContent GCHeadContent = EditorGUIUtility.TrTextContent("GC.Collect", "Invoke GC.Collect");
        private static readonly GUIContent EnableTrackingHeadContent = EditorGUIUtility.TrTextContent("Enable Tracking", "Start to track async/await STask. Performance impact:low");
        private static readonly GUIContent EnableStackTraceHeadContent = EditorGUIUtility.TrTextContent("Enable StackTrace", "Capture StackTrace when task is started. Performance:high");

        // [Enable Tracking] | [Enable StackTrace]
        private void RenderHeadPanel()
        {
            EditorGUILayout.BeginVertical(EmptyLayoutOption);
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, EmptyLayoutOption);
                {
                    if (GUILayout.Toggle(EnableAutoReload, EnableAutoReloadHeadContent, EditorStyles.toolbarButton, EmptyLayoutOption) != EnableAutoReload)
                    {
                        TaskTracker.EditorEnableState.EnableAutoReload = !EnableAutoReload;
                    }

                    if (GUILayout.Toggle(EnableTracking, EnableTrackingHeadContent, EditorStyles.toolbarButton, EmptyLayoutOption) != EnableTracking)
                    {
                        TaskTracker.EditorEnableState.EnableTracking = !EnableTracking;
                    }
                    
                    if (GUILayout.Toggle(EnableStackTrace, EnableStackTraceHeadContent, EditorStyles.toolbarButton, EmptyLayoutOption) != EnableStackTrace)
                    {
                        TaskTracker.EditorEnableState.EnableStackTrace = !EnableStackTrace;
                    }
                    
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(ReloadHeadContent, EditorStyles.toolbarButton, EmptyLayoutOption))
                    {
                        TaskTracker.CheckAndResetDirty();
                        this.treeView.ReloadAndSort();
                        this.Repaint();
                    }

                    if (GUILayout.Button(GCHeadContent, EditorStyles.toolbarButton, EmptyLayoutOption))
                    {
                        GC.Collect();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }
        #endregion

        #region TableColumn

        private Vector2 tableScroll;
        private GUIStyle tableListStyle;

        private void RenderTable()
        {
            if (this.tableListStyle == null)
            {
                this.tableListStyle = new GUIStyle("CN Box");
                this.tableListStyle.margin.top = 0;
                this.tableListStyle.padding.left = 3;
            }

            EditorGUILayout.BeginVertical(this.tableListStyle, EmptyLayoutOption);
            {
                this.tableScroll = EditorGUILayout.BeginScrollView(this.tableScroll, new GUILayoutOption[]
                {
                    GUILayout.ExpandWidth(true),
                    GUILayout.MaxWidth(2000f)
                });
                var controlRect = EditorGUILayout.GetControlRect(new GUILayoutOption[]
                {
                    GUILayout.ExpandHeight(true),
                    GUILayout.ExpandWidth(true)
                });

                this.treeView?.OnGUI(controlRect);
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private void Update()
        {
            if (EnableAutoReload)
            {
                if (interval++ % 120 == 0)
                {
                    if (TaskTracker.CheckAndResetDirty())
                    {
                        this.treeView.ReloadAndSort();
                        this.Repaint();
                    }
                }
            }
        }

        #endregion

        #region Details

        private static GUIStyle detailsStyle;
        private Vector2 detailsScroll;

        private void RenderDetailsPanel()
        {
            if (detailsStyle == null)
            {
                detailsStyle = new GUIStyle("CN Message");
                detailsStyle.wordWrap = false;
                detailsStyle.stretchHeight = true;
                detailsStyle.margin.right = 15;
            }

            string message = "";
            var selected = this.treeView.state.selectedIDs;
            if (selected.Count > 0)
            {
                var first = selected[0];
                var item = this.treeView.CurrentBindingItems.FirstOrDefault(x => x.id == first) as STaskTrackerViewItem;
                if (item != null)
                {
                    message = item.Position;
                }
            }

            this.detailsScroll = EditorGUILayout.BeginScrollView(this.detailsScroll, EmptyLayoutOption);
            {
                var vector = detailsStyle.CalcSize(new GUIContent(message));
                EditorGUILayout.SelectableLabel(message, detailsStyle, new GUILayoutOption[]
                {
                    GUILayout.ExpandHeight(true),
                    GUILayout.ExpandWidth(true),
                    GUILayout.MinWidth(vector.x),
                    GUILayout.MinHeight(vector.y)
                });
            }
            EditorGUILayout.EndScrollView();
        }

        #endregion
    }
}