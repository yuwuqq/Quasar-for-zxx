using Quasar.Common.Helpers;
using Quasar.Server.Helper;
using Quasar.Server.Utilities;
using System;
using System.Windows.Forms;

namespace Quasar.Server.Extensions
{
    public static class ListViewExtensions
    {
        private const uint SET_COLUMN_WIDTH = 4126;
        private static readonly IntPtr AUTOSIZE_USEHEADER = new IntPtr(-2);

        /// <summary>
        /// 在给定的列表视图上自动确定正确的列大小。
        /// </summary>
        /// <param name="targetListView">列表视图，其列要被自动调整。</param>
        public static void AutosizeColumns(this ListView targetListView)
        {
            if (PlatformHelper.RunningOnMono) return;
            for (int lngColumn = 0; lngColumn <= (targetListView.Columns.Count - 1); lngColumn++)
            {
                NativeMethods.SendMessage(targetListView.Handle, SET_COLUMN_WIDTH, new IntPtr(lngColumn), AUTOSIZE_USEHEADER);
            }
        }

        /// <summary>
        /// 选择给定列表视图上的所有项目。
        /// </summary>
        /// <param name="targetListView">列表视图，其项目将被选中。</param>
        public static void SelectAllItems(this ListView targetListView)
        {
            NativeMethodsHelper.SetItemState(targetListView.Handle, -1, 2, 2);
        }
    }
}