using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenSnap
{
    internal sealed class TrayMenu : ContextMenuStrip
    {
        private Font menuFont;
        private float scale = 1;
        private Color hover;
        private Color border;

        public TrayMenu()
        {
            ShowImageMargin = false;
            ShowCheckMargin = false;
            Renderer = new MenuRenderer(this);
        }

        protected override void OnOpening(CancelEventArgs e)
        {
            scale = DesktopAppearance.ScaleAt(Cursor.Position);
            bool highContrast = SystemInformation.HighContrast;
            bool light = true;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                light = !Equals(key == null ? null : key.GetValue("SystemUsesLightTheme"), 0);
            BackColor = highContrast ? SystemColors.Menu : light ? Color.FromArgb(250, 250, 250) : Color.FromArgb(37, 37, 40);
            ForeColor = highContrast ? SystemColors.MenuText : light ? Color.FromArgb(28, 28, 30) : Color.FromArgb(245, 245, 246);
            hover = highContrast ? SystemColors.Highlight : light ? Color.FromArgb(235, 235, 239) : Color.FromArgb(58, 58, 63);
            border = highContrast ? SystemColors.WindowText : light ? Color.FromArgb(219, 219, 224) : Color.FromArgb(74, 74, 79);
            Font previous = menuFont;
            menuFont = new Font("Segoe UI", Px(14), FontStyle.Regular, GraphicsUnit.Pixel);
            Font = menuFont;
            if (previous != null)
                previous.Dispose();

            SuspendLayout();
            Padding = new Padding(Px(5));
            int width = Px(224);
            foreach (ToolStripItem item in Items)
                width = Math.Max(width, TextRenderer.MeasureText(item.Text, menuFont).Width + Px(24));
            foreach (ToolStripItem item in Items)
            {
                bool separator = item is ToolStripSeparator;
                item.AutoSize = false;
                item.Size = new Size(width, Px(separator ? 9 : 34));
                item.Margin = Padding.Empty;
                item.Padding = separator ? Padding.Empty : new Padding(Px(11), 0, Px(11), 0);
                item.TextAlign = ContentAlignment.MiddleLeft;
            }
            ResumeLayout(true);
            base.OnOpening(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateCorners();
        }

        protected override void OnOpened(EventArgs e)
        {
            UpdateCorners();
            base.OnOpened(e);
        }

        private void UpdateCorners()
        {
            if (Width < 2 || Height < 2)
                return;
            using (GraphicsPath path = Rounded(new Rectangle(0, 0, Width, Height), Px(8)))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null)
                    previous.Dispose();
            }
        }

        private int Px(int logical)
        {
            return Math.Max(1, (int)Math.Round(logical * scale));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && menuFont != null)
            {
                menuFont.Dispose();
                menuFont = null;
            }
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class MenuRenderer : ToolStripRenderer
        {
            private readonly TrayMenu menu;

            public MenuRenderer(TrayMenu menu) { this.menu = menu; }

            private Rectangle ItemBounds(ToolStripItem item)
            {
                int width = Math.Min(item.Width, menu.ClientSize.Width - item.Bounds.Left);
                return new Rectangle(0, 0, width, item.Height);
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                e.Graphics.Clear(menu.BackColor);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = Rounded(new Rectangle(0, 0, menu.Width - 1, menu.Height - 1), menu.Px(8)))
                using (var pen = new Pen(menu.border))
                    e.Graphics.DrawPath(pen, path);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!e.Item.Selected && !e.Item.Pressed)
                    return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = ItemBounds(e.Item);
                bounds.Inflate(-menu.Px(4), -menu.Px(2));
                using (var path = Rounded(bounds, menu.Px(5)))
                using (var brush = new SolidBrush(menu.hover))
                    e.Graphics.FillPath(brush, path);
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                Color color = SystemInformation.HighContrast && e.Item.Selected ? SystemColors.HighlightText : menu.ForeColor;
                Rectangle bounds = ItemBounds(e.Item);
                bounds.Inflate(-menu.Px(11), 0);
                TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, bounds, color,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                int y = e.Item.Height / 2;
                using (var pen = new Pen(menu.border))
                    e.Graphics.DrawLine(pen, menu.Px(6), y, e.Item.Width - menu.Px(6), y);
            }
        }
    }
}
