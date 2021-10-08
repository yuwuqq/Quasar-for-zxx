using System.Windows.Forms;

namespace Quasar.Server.Controls
{
    public class RegistryTreeView : TreeView
    {
        public RegistryTreeView()
        {
            //启用双缓冲并忽略WM_ERASEBKGND以减少闪烁
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }
}
