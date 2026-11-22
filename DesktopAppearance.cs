using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenSnap
{
    internal static class DesktopAppearance
    {
        public static float ScaleAt(Point point)
        {
            uint x, y;
            IntPtr monitor = MonitorFromPoint(point, 2);
            return GetDpiForMonitor(monitor, 0, out x, out y) == 0 ? x / 96f : 1;
        }

        public static Icon LoadAppIcon(Size size)
        {
            const int applicationIcon = 32512;
            const int imageIcon = 1;
            IntPtr icon = LoadImage(GetModuleHandle(null), new IntPtr(applicationIcon), imageIcon, size.Width, size.Height, 0);
            if (icon == IntPtr.Zero)
                return (Icon)SystemIcons.Application.Clone();
            try
            {
                using (Icon borrowed = Icon.FromHandle(icon))
                    return (Icon)borrowed.Clone();
            }
            finally
            {
                DestroyIcon(icon);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(Point point, uint flags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImage(IntPtr instance, IntPtr name, int type, int width, int height, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);
    }
}
