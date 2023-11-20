using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace SFramework.Threading.Tasks.Editor
{
    public class STaskTrackerViewItem : TreeViewItem
    {
        private static Regex removeHref = new Regex("<a href.+>(.+)</a>", RegexOptions.Compiled);
        
        public string TaskType { get; set; }
        public string Elapsed { get; set; }
        public string Status { get; set; }

        private string position;
        public string Position
        {
            get { return this.position; }
            set
            {
                this.position = value;
                this.PositionFirstLine = GetFirstLine(this.position);
            }
        }
        
        public string PositionFirstLine { get; private set; }

        private static string GetFirstLine(string str)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == '\r' || str[i] == '\n')
                {
                    break;
                }
                sb.Append(str[i]);
            }

            return removeHref.Replace(sb.ToString(), "$1");
        }

        public STaskTrackerViewItem(int id) : base(id)
        {
            
        }
    }
    
    public class STaskTrackerTreeView : TreeView
    {
        private const string sortedColumnIndexStateKey = "STaskTrackerTreeView_sortedColumnIndex";

        public IReadOnlyList<TreeViewItem> CurrentBindingItems;

        public STaskTrackerTreeView() : this(new TreeViewState(), new MultiColumnHeader(new MultiColumnHeaderState(new[]
        {
            new MultiColumnHeaderState.Column() { headerContent = new GUIContent("TaskType"), width = 20 },
            new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Elapsed"), width = 10 },
            new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Status"), width = 20 },
            new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Position") },
        }))) { }

        private STaskTrackerTreeView(TreeViewState state, MultiColumnHeader header)
            : base(state, header)
        {
            this.rowHeight = 20;
            this.showAlternatingRowBackgrounds = true;
            this.showBorder = true;
            header.sortingChanged += this.HeaderSortingChanged;
            
            header.ResizeToFit();
            this.Reload();

            header.sortedColumnIndex = SessionState.GetInt(sortedColumnIndexStateKey, 1);
        }

        public void ReloadAndSort()
        {
            List<int> currentSelected = this.state.selectedIDs;
            this.Reload();
            this.HeaderSortingChanged(this.multiColumnHeader);
            this.state.selectedIDs = currentSelected;
        }
        
        private void HeaderSortingChanged(MultiColumnHeader multicolumnHeader)
        {
            SessionState.SetInt(sortedColumnIndexStateKey, multicolumnHeader.sortedColumnIndex);
            int index = multicolumnHeader.sortedColumnIndex;
            bool ascending = multicolumnHeader.IsSortedAscending(multicolumnHeader.sortedColumnIndex);

            var items = this.rootItem.children.Cast<STaskTrackerViewItem>();

            IOrderedEnumerable<STaskTrackerViewItem> orderedEnumerable;
            switch (index)
            {
                case 0:
                    orderedEnumerable = ascending ? items.OrderBy(item => item.TaskType) : items.OrderByDescending(item => item.TaskType);
                    break;
                case 1:
                    orderedEnumerable = ascending ? items.OrderBy(item => double.Parse(item.Elapsed)) : items.OrderByDescending(item => double.Parse(item.Elapsed));
                    break;
                case 2:
                    orderedEnumerable = ascending ? items.OrderBy(item => item.Status) : items.OrderByDescending(item => item.Status);
                    break;
                case 3:
                    orderedEnumerable = ascending ? items.OrderBy(item => item.Position) : items.OrderByDescending(item => item.Position);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index), index, null);
            }

            this.CurrentBindingItems = this.rootItem.children = orderedEnumerable.Cast<TreeViewItem>().ToList();
            this.BuildRows(this.rootItem);
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem() { depth = -1 };

            var children = new List<TreeViewItem>();
            
            //todo TaskTracker foreachActiveTask

            this.CurrentBindingItems = children;
            root.children = this.CurrentBindingItems as List<TreeViewItem>;
            return root;
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item as STaskTrackerViewItem;

            for (var visibleColumnIndex = 0; visibleColumnIndex < args.GetNumVisibleColumns(); visibleColumnIndex++)
            {
                var rect = args.GetCellRect(visibleColumnIndex);
                var columnIndex = args.GetColumn(visibleColumnIndex);

                var labelStyle = args.selected ? EditorStyles.whiteLabel : EditorStyles.label;
                labelStyle.alignment = TextAnchor.MiddleLeft;
                switch (columnIndex)
                {
                    case 0:
                        EditorGUI.LabelField(rect, item.TaskType, labelStyle);
                        break;
                    case 1:
                        EditorGUI.LabelField(rect, item.Elapsed, labelStyle);
                        break;
                    case 2:
                        EditorGUI.LabelField(rect, item.Status, labelStyle);
                        break;
                    case 3:
                        EditorGUI.LabelField(rect, item.PositionFirstLine, labelStyle);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, null);
                }
            }
        }
    }
}