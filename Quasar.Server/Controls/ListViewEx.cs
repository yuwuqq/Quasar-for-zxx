using Quasar.Common.Helpers;
using Quasar.Server.Helper;
using Quasar.Server.Utilities;
using System;
using System.Windows.Forms;

namespace Quasar.Server.Controls
{
    internal class AeroListView : ListView
    {
        private const uint WM_CHANGEUISTATE = 0x127;

        private const short UIS_SET = 1;
        private const short UISF_HIDEFOCUS = 0x1;
        private readonly IntPtr _removeDots = new IntPtr(NativeMethodsHelper.MakeWin32Long(UIS_SET, UISF_HIDEFOCUS));

        private ListViewColumnSorter LvwColumnSorter { get; set; }

        /// <summary>
        /// 初始化一个<see cref="AeroListView"/>类的新实例。
        /// </summary>
        public AeroListView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.LvwColumnSorter = new ListViewColumnSorter();
            this.ListViewItemSorter = LvwColumnSorter;
            this.View = View.Details;
            this.FullRowSelect = true;
        }

        /// <summary>
        /// 引起<see cref="E:HandleCreated" />事件。
        /// </summary>
        /// <param name="e">包含事件数据的<see cref="EventArgs"/>实例。</param>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (PlatformHelper.RunningOnMono) return;

            if (PlatformHelper.VistaOrHigher)
            {
                // set window theme to explorer
                NativeMethods.SetWindowTheme(this.Handle, "explorer", null);
            }

            if (PlatformHelper.XpOrHigher)
            {
                // 移除重点项目周围难看的虚线
                NativeMethods.SendMessage(this.Handle, WM_CHANGEUISTATE, _removeDots, IntPtr.Zero);
            }
        }

        /// <summary>
        /// 引发<see cref="E:ColumnClick" />事件。
        /// </summary>
        /// <param name="e">包含事件数据的<see cref="ColumnClickEventArgs"/>实例。</param>
        protected override void OnColumnClick(ColumnClickEventArgs e)
        {
            base.OnColumnClick(e);

            // 判断被点击的列是否已经是被排序的列。
            if (e.Column == this.LvwColumnSorter.SortColumn)
            {
                // 反转该列的当前排序方向。
                this.LvwColumnSorter.Order = (this.LvwColumnSorter.Order == SortOrder.Ascending)
                    ? SortOrder.Descending
                    : SortOrder.Ascending;
            }
            else
            {
                // 设置要排序的列号；默认为升序。
                this.LvwColumnSorter.SortColumn = e.Column;
                this.LvwColumnSorter.Order = SortOrder.Ascending;
            }

            // 用这些新的排序选项进行排序。
            if (!this.VirtualMode)
                this.Sort();
        }
    }
}