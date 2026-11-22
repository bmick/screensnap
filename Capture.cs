using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenSnap
{
    internal sealed class Capture : Form
    {
        private readonly Bitmap desktop;
        private Point pointer;
        private Point? anchor;
        public Rectangle Selection { get; private set; }

        public Capture(Bitmap desktop, Rectangle bounds)
        {
            this.desktop = desktop;
            Text = "Capture region — ScreenSnap";
            AccessibleName = "Drag to capture a region. Press Escape to cancel.";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Bounds = bounds;
            TopMost = true;
            DoubleBuffered = true;
            Cursor = Cursors.Cross;
            pointer = Clip(Point.Subtract(Cursor.Position, new Size(bounds.Location)));
        }

        public static Bitmap Freeze(Rectangle bounds)
        {
            var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppRgb);
            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Black);
                    foreach (Screen screen in Screen.AllScreens)
                    {
                        Rectangle area = screen.Bounds;
                        graphics.CopyFromScreen(area.X, area.Y, area.X - bounds.X, area.Y - bounds.Y, area.Size);
                    }
                }
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData != Keys.Escape)
                return base.ProcessCmdKey(ref message, keyData);
            DialogResult = DialogResult.Cancel;
            return true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                DialogResult = DialogResult.Cancel;
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                pointer = Clip(e.Location);
                anchor = pointer;
                base.Capture = true;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            pointer = Clip(e.Location);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !anchor.HasValue)
                return;
            pointer = Clip(e.Location);
            Rectangle result = DragRectangle();
            bool moved = pointer != anchor.Value;
            anchor = null;
            base.Capture = false;
            if (moved)
            {
                Selection = result;
                DialogResult = DialogResult.OK;
            }
            else
                Invalidate();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!base.Capture && anchor.HasValue)
            {
                anchor = null;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Rectangle dirty = Rectangle.Intersect(e.ClipRectangle, new Rectangle(Point.Empty, desktop.Size));
            if (dirty.IsEmpty)
                return;
            graphics.DrawImage(desktop, dirty, dirty, GraphicsUnit.Pixel);
            Rectangle selection = DragRectangle();
            using (var outside = new Region(ClientRectangle))
            using (var shade = new SolidBrush(Color.FromArgb(105, 8, 14, 24)))
            {
                if (!selection.IsEmpty)
                    outside.Exclude(selection);
                graphics.FillRegion(shade, outside);
            }

            using (var shadow = new Pen(Color.FromArgb(180, 7, 15, 24), 3))
            using (var guide = new Pen(Color.FromArgb(225, 204, 246, 255)))
            using (var border = new Pen(Color.FromArgb(95, 214, 247)))
            {
                foreach (Pen pen in new[] { shadow, guide })
                {
                    graphics.DrawLine(pen, 0, pointer.Y, ClientSize.Width, pointer.Y);
                    graphics.DrawLine(pen, pointer.X, 0, pointer.X, ClientSize.Height);
                }
                if (!selection.IsEmpty)
                    graphics.DrawRectangle(border, selection.X, selection.Y, selection.Width - 1, selection.Height - 1);
            }
            PaintMagnifier(graphics, selection);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        private void PaintMagnifier(Graphics graphics, Rectangle selection)
        {
            const int radius = 8;
            Point screenPoint = PointToScreen(pointer);
            Rectangle monitor = Screen.FromPoint(screenPoint).Bounds;
            uint dpi, verticalDpi;
            IntPtr monitorHandle = MonitorFromPoint(screenPoint, 2);
            if (GetDpiForMonitor(monitorHandle, 0, out dpi, out verticalDpi) != 0)
                dpi = 96;
            int zoom = (int)Math.Round(8 * dpi / 96.0);
            int size = (radius * 2 + 1) * zoom;
            int labelHeight = (int)Math.Round(23 * dpi / 96.0);
            monitor.Offset(-Left, -Top);
            int x = pointer.X + 24;
            int y = pointer.Y + 24;
            if (x + size + 6 > monitor.Right)
                x = pointer.X - size - 24;
            if (y + size + labelHeight + 6 > monitor.Bottom)
                y = pointer.Y - size - labelHeight - 24;
            x = Math.Max(monitor.Left + 4, x);
            y = Math.Max(monitor.Top + 4, y);

            var sample = new Rectangle(pointer.X - radius, pointer.Y - radius, radius * 2 + 1, radius * 2 + 1);
            Rectangle pixels = Rectangle.Intersect(sample, new Rectangle(Point.Empty, desktop.Size));
            var visible = new Rectangle(x + (pixels.X - sample.X) * zoom, y + (pixels.Y - sample.Y) * zoom,
                pixels.Width * zoom, pixels.Height * zoom);
            using (var background = new SolidBrush(Color.FromArgb(19, 29, 42)))
            using (var edge = new Pen(Color.FromArgb(103, 178, 199)))
            {
                graphics.FillRectangle(background, x - 2, y - 2, size + 4, size + labelHeight + 4);
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(desktop, visible, pixels, GraphicsUnit.Pixel);
                graphics.PixelOffsetMode = PixelOffsetMode.Default;
                graphics.DrawRectangle(edge, x - 2, y - 2, size + 3, size + labelHeight + 3);
            }
            int centerX = x + radius * zoom;
            int centerY = y + radius * zoom;
            graphics.DrawRectangle(Pens.Black, centerX - 1, centerY - 1, zoom + 1, zoom + 1);
            graphics.DrawRectangle(Pens.White, centerX, centerY, zoom - 1, zoom - 1);
            string text = selection.IsEmpty ? "Drag to capture" : selection.Width + " × " + selection.Height + " px";
            TextRenderer.DrawText(graphics, text, SystemFonts.MessageBoxFont,
                new Rectangle(x, y + size, size, labelHeight), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private Rectangle DragRectangle()
        {
            if (!anchor.HasValue)
                return Rectangle.Empty;
            Point start = anchor.Value;
            return Rectangle.FromLTRB(Math.Min(start.X, pointer.X), Math.Min(start.Y, pointer.Y),
                Math.Max(start.X, pointer.X) + 1, Math.Max(start.Y, pointer.Y) + 1);
        }

        private Point Clip(Point point)
        {
            return new Point(Math.Max(0, Math.Min(desktop.Width - 1, point.X)),
                Math.Max(0, Math.Min(desktop.Height - 1, point.Y)));
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(Point point, uint flags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    }
}
