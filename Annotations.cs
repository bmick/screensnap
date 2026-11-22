using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ScreenSnap
{
    internal enum Tool { Scribble, Line, Rectangle, Ellipse, Text, Transparency }

    internal sealed class AnnotationMark
    {
        public readonly AnnotationMark Previous;
        private readonly Tool tool;
        private readonly List<Point> points;
        private readonly Color ink;
        private readonly int stroke;
        private readonly string text;
        private readonly bool constrain;

        public AnnotationMark(AnnotationMark previous, Tool tool, List<Point> points, Color ink, int stroke,
            string text, bool constrain)
        {
            Previous = previous;
            this.tool = tool;
            this.points = points;
            this.ink = ink;
            this.stroke = stroke;
            this.text = text;
            this.constrain = constrain;
        }

        public static void DrawAll(Graphics graphics, AnnotationMark latest)
        {
            var ordered = new Stack<AnnotationMark>();
            for (AnnotationMark mark = latest; mark != null; mark = mark.Previous)
                ordered.Push(mark);
            foreach (AnnotationMark mark in ordered)
                Annotations.Draw(graphics, mark.tool, mark.points, mark.ink, mark.stroke, mark.text, mark.constrain);
        }
    }

    internal static class Annotations
    {
        public static void Draw(Graphics graphics, Tool tool, List<Point> points, Color color, int width, string text,
            bool constrain = false)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            Point start = points[0];
            Point end = points[points.Count - 1];
            using (var pen = new Pen(color, width))
            using (var brush = new SolidBrush(color))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                switch (tool)
                {
                    case Tool.Scribble:
                        if (points.Count == 2)
                            graphics.DrawLine(pen, start, end);
                        else if (points.Count > 2)
                        {
                            using (var path = new GraphicsPath())
                            {
                                for (int i = 2; i < points.Count; i++)
                                    AddScribbleCurve(path, points, i);
                                path.AddLine(ScribbleTail(points), end);
                                graphics.DrawPath(pen, path);
                            }
                        }
                        else
                            graphics.FillEllipse(brush, start.X - width / 2f, start.Y - width / 2f, width, width);
                        break;
                    case Tool.Line:
                        graphics.DrawLine(pen, start, end);
                        break;
                    case Tool.Rectangle:
                    case Tool.Ellipse:
                        if (constrain)
                        {
                            int side = Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
                            end = new Point(start.X + (end.X < start.X ? -side : side),
                                start.Y + (end.Y < start.Y ? -side : side));
                        }
                        var bounds = Rectangle.FromLTRB(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
                            Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));
                        if (bounds.Width == 0 || bounds.Height == 0)
                            graphics.DrawLine(pen, start, end);
                        else if (tool == Tool.Rectangle)
                            graphics.DrawRectangle(pen, bounds);
                        else
                            graphics.DrawEllipse(pen, bounds);
                        break;
                    case Tool.Text:
                        DrawText(graphics, text, start, width, color);
                        break;
                }
            }
        }

        public static PointF ScribbleTail(List<Point> points)
        {
            int last = points.Count - 1;
            return last == 1 ? points[0] : Midpoint(points[last - 1], points[last]);
        }

        public static void AddScribbleCurve(GraphicsPath path, List<Point> points, int index)
        {
            PointF middle = points[index - 1];
            PointF start = index == 2 ? points[0] : Midpoint(points[index - 2], middle);
            PointF end = Midpoint(middle, points[index]);
            path.AddBezier(start,
                new PointF((start.X + 2 * middle.X) / 3, (start.Y + 2 * middle.Y) / 3),
                new PointF((end.X + 2 * middle.X) / 3, (end.Y + 2 * middle.Y) / 3), end);
        }

        private static PointF Midpoint(PointF first, PointF second)
        {
            return new PointF((first.X + second.X) / 2, (first.Y + second.Y) / 2);
        }

        public static void DrawText(Graphics graphics, string text, Point anchor, int stroke, Color ink,
            int caret = -1, int selectionLength = 0)
        {
            // Increase the default text size while preserving the slider's end sizes.
            float size = stroke <= 4 ? 14 + (stroke - 1) * (34f / 3) : 48 + (stroke - 4) * 4;
            using (var family = new FontFamily("Segoe UI"))
            using (var font = new Font(family, size, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
            using (var path = new GraphicsPath())
            using (var brush = new SolidBrush(ink))
            using (var selection = new SolidBrush(Color.FromArgb(110, SystemColors.Highlight)))
            using (var caretEdge = new Pen(Color.White, 0))
            using (var caretLine = new Pen(Color.FromArgb(32, 39, 51), 0))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                float height = size * family.GetLineSpacing(FontStyle.Regular) / family.GetEmHeight(FontStyle.Regular);
                float y = anchor.Y;
                int offset = 0;
                foreach (string line in text.Split('\n'))
                {
                    string content = line.TrimEnd('\r');
                    int end = offset + content.Length;
                    if (selectionLength > 0 && caret <= end && caret + selectionLength > offset)
                    {
                        float left = TextOffset(graphics, font, format, content, Math.Max(0, caret - offset));
                        float right = TextOffset(graphics, font, format, content, Math.Min(content.Length, caret + selectionLength - offset));
                        graphics.FillRectangle(selection, anchor.X + left, y, Math.Max(1, right - left), height);
                    }
                    if (content.Length > 0)
                        path.AddString(content, family, (int)FontStyle.Regular, size, new PointF(anchor.X, y), format);
                    if (selectionLength == 0 && caret >= offset && caret <= end)
                    {
                        float x = anchor.X + TextOffset(graphics, font, format, content, caret - offset);
                        graphics.DrawLine(caretEdge, x + 1, y, x + 1, y + height);
                        graphics.DrawLine(caretLine, x, y, x, y + height);
                    }
                    offset += line.Length + 1;
                    y += height;
                }
                graphics.FillPath(brush, path);
            }
        }

        private static float TextOffset(Graphics graphics, Font font, StringFormat format, string line, int count)
        {
            return count == 0 ? 0 : graphics.MeasureString(line.Substring(0, count), font, PointF.Empty, format).Width;
        }

        public static bool ClearConnected(Bitmap bitmap, Point start, int percent)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            var bounds = new Rectangle(0, 0, width, height);
            if (!bounds.Contains(start))
                return false;
            BitmapData data = bitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int[] pixels = new int[checked(width * height)];
                Transfer(data, pixels, width, height, false);
                int origin = start.Y * width + start.X;
                int color = pixels[origin];
                if ((uint)color >> 24 == 0)
                    return false;
                int tolerance = (int)Math.Round(255 * percent / 100.0);
                int[] queue = new int[pixels.Length];
                int tail = 1;
                queue[0] = origin;
                pixels[origin] = 0;
                for (int head = 0; head < tail; head++)
                {
                    int index = queue[head];
                    int column = index % width;
                    if (column > 0)
                        Enqueue(index - 1, color, tolerance, pixels, queue, ref tail);
                    if (column < width - 1)
                        Enqueue(index + 1, color, tolerance, pixels, queue, ref tail);
                    if (index >= width)
                        Enqueue(index - width, color, tolerance, pixels, queue, ref tail);
                    if (index < pixels.Length - width)
                        Enqueue(index + width, color, tolerance, pixels, queue, ref tail);
                }
                Transfer(data, pixels, width, height, true);
                return true;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static void Transfer(BitmapData data, int[] pixels, int width, int height, bool write)
        {
            if (data.Stride == width * 4)
            {
                if (write)
                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                else
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                return;
            }
            for (int row = 0; row < height; row++)
            {
                IntPtr line = IntPtr.Add(data.Scan0, row * data.Stride);
                if (write)
                    Marshal.Copy(pixels, row * width, line, width);
                else
                    Marshal.Copy(line, pixels, row * width, width);
            }
        }

        private static void Enqueue(int index, int origin, int tolerance, int[] pixels, int[] queue, ref int tail)
        {
            int other = pixels[index];
            if ((uint)other >> 24 == 0 ||
                Math.Abs((other & 255) - (origin & 255)) > tolerance ||
                Math.Abs(((other >> 8) & 255) - ((origin >> 8) & 255)) > tolerance ||
                Math.Abs(((other >> 16) & 255) - ((origin >> 16) & 255)) > tolerance)
                return;
            pixels[index] = 0;
            queue[tail++] = index;
        }
    }

    internal sealed class Canvas : Control
    {
        private static readonly Color Backdrop = Color.FromArgb(31, 39, 51);
        private readonly List<Point> points = new List<Point>();
        private readonly MouseHistory mouseHistory = new MouseHistory();
        private Bitmap viewport;
        private Bitmap viewportSource;
        private AnnotationMark viewportMarks;
        private Bitmap scribble;
        private int paintedPoints;
        private TextBox textInput;
        private Point textAnchor;
        public Bitmap Image { get; set; }
        public AnnotationMark Marks { get; set; }
        public Tool Tool { get; set; }
        public Color Ink { get; set; }
        public int Stroke = 4;
        public bool Drawing { get { return points.Count > 0; } }
        public bool EditingText { get { return textInput != null; } }
        public Action<Tool, List<Point>, string, bool> Apply;

        public Canvas()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.Selectable, true);
            ResizeRedraw = true;
            Cursor = Cursors.Cross;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(Backdrop))
                e.Graphics.FillRectangle(brush, e.ClipRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Image == null)
                return;
            Rectangle area = ImageArea();
            Graphics graphics = e.Graphics;
            EnsureViewport(area.Size);
            Rectangle dirty = Rectangle.Intersect(area, e.ClipRectangle);
            if (dirty.Width == 0 || dirty.Height == 0)
                return;
            Blit(graphics, viewport, area, dirty);
            if (Drawing && Tool == Tool.Scribble && points.Count > 1)
            {
                UpdateScribble(area.Size);
                Blit(graphics, scribble, area, dirty);
            }
            if (!Drawing && !EditingText)
                return;
            GraphicsState state = graphics.Save();
            graphics.SetClip(area);
            graphics.TranslateTransform(area.X, area.Y);
            graphics.ScaleTransform(area.Width / (float)Image.Width, area.Height / (float)Image.Height);
            graphics.PixelOffsetMode = PixelOffsetMode.Default;
            if (Drawing && Tool == Tool.Scribble && points.Count > 1)
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = ScribblePen())
                    graphics.DrawLine(pen, Annotations.ScribbleTail(points), points[points.Count - 1]);
            }
            else if (Drawing)
                Annotations.Draw(graphics, Tool, points, Ink, Stroke, null, ShiftHeld);
            if (EditingText)
                Annotations.DrawText(graphics, textInput.Text, textAnchor, Stroke, Ink,
                    textInput.SelectionStart, textInput.SelectionLength);
            graphics.Restore(state);
        }

        private void EnsureViewport(Size size)
        {
            if (viewport != null && viewport.Size == size && viewportSource == Image && viewportMarks == Marks)
                return;
            if (viewport != null)
                viewport.Dispose();
            viewport = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            viewportSource = Image;
            viewportMarks = Marks;
            var bounds = new Rectangle(Point.Empty, size);
            using (var graphics = Graphics.FromImage(viewport))
            using (var checker = new HatchBrush(HatchStyle.LargeCheckerBoard, Color.FromArgb(210, 217, 224), Color.WhiteSmoke))
            using (var edges = new ImageAttributes())
            {
                graphics.FillRectangle(checker, bounds);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                edges.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(Image, bounds, 0, 0, Image.Width, Image.Height, GraphicsUnit.Pixel, edges);
                graphics.PixelOffsetMode = PixelOffsetMode.Default;
                graphics.ScaleTransform(size.Width / (float)Image.Width, size.Height / (float)Image.Height);
                AnnotationMark.DrawAll(graphics, Marks);
            }
        }

        private static void Blit(Graphics graphics, Bitmap bitmap, Rectangle area, Rectangle dirty)
        {
            Rectangle source = dirty;
            source.Offset(-area.X, -area.Y);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(bitmap, dirty, source, GraphicsUnit.Pixel);
        }

        private Pen ScribblePen()
        {
            return new Pen(Ink, Stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }

        private void UpdateScribble(Size size)
        {
            if (scribble == null || scribble.Size != size)
            {
                ClearScribble();
                scribble = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                paintedPoints = 2;
            }
            if (paintedPoints >= points.Count)
                return;
            using (var graphics = Graphics.FromImage(scribble))
            using (var path = new GraphicsPath())
            using (var pen = ScribblePen())
            {
                graphics.ScaleTransform(size.Width / (float)Image.Width, size.Height / (float)Image.Height);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = paintedPoints; i < points.Count; i++)
                    Annotations.AddScribbleCurve(path, points, i);
                graphics.DrawPath(pen, path);
            }
            paintedPoints = points.Count;
        }

        private void ClearScribble()
        {
            if (scribble != null)
            {
                scribble.Dispose();
                scribble = null;
            }
        }

        public Bitmap RenderImage()
        {
            var bitmap = Image.Clone(new Rectangle(0, 0, Image.Width, Image.Height), PixelFormat.Format32bppArgb);
            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                    AnnotationMark.DrawAll(graphics, Marks);
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || Image == null)
                return;
            FinishText(true);
            if (!ImageArea().Contains(e.Location))
                return;
            Focus();
            Point point = ToPixel(e.Location);
            if (Tool == Tool.Text)
            {
                BeginText(point);
                return;
            }
            if (Tool == Tool.Transparency)
            {
                Apply(Tool, new List<Point> { point }, null, false);
                return;
            }
            points.Add(point);
            if (Tool == Tool.Scribble)
                mouseHistory.Begin(PointToScreen(e.Location));
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!Drawing)
                return;
            if (Tool == Tool.Scribble)
            {
                Rectangle area = ImageArea();
                Point origin = PointToScreen(Point.Empty);
                int before = points.Count;
                mouseHistory.Read(new Point(origin.X + e.X, origin.Y + e.Y),
                    screen => Track(new Point(screen.X - origin.X, screen.Y - origin.Y), area));
                if (points.Count == before)
                    return;
                // Include the previous tip, which is replaced by a curved segment on the next paint.
                var dirty = new Rectangle(points[Math.Max(0, before - 2)], new Size(1, 1));
                for (int i = before - 1; i < points.Count; i++)
                    dirty = Rectangle.Union(dirty, new Rectangle(points[i], new Size(1, 1)));
                dirty.Inflate(Stroke + 2, Stroke + 2);
                float sx = area.Width / (float)Image.Width;
                float sy = area.Height / (float)Image.Height;
                Invalidate(Rectangle.Ceiling(new RectangleF(area.X + dirty.X * sx, area.Y + dirty.Y * sy,
                    dirty.Width * sx + 2, dirty.Height * sy + 2)));
            }
            else
            {
                Track(e.Location, ImageArea());
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !Drawing)
                return;
            Track(e.Location, ImageArea());
            var stroke = new List<Point>(points);
            points.Clear();
            ClearScribble();
            Capture = false;
            if (Tool == Tool.Scribble || stroke.Count > 1)
                Apply(Tool, stroke, null, ShiftHeld);
            Invalidate();
        }

        private static bool ShiftHeld { get { return (ModifierKeys & Keys.Shift) != 0; } }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (Drawing && e.KeyCode == Keys.ShiftKey)
                Invalidate();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (Drawing && e.KeyCode == Keys.ShiftKey)
                Invalidate();
        }

        private void BeginText(Point anchor)
        {
            textAnchor = anchor;
            var input = new TextBox
            {
                AccessibleName = "Annotation text",
                Multiline = true,
                AcceptsReturn = true,
                WordWrap = false,
                Bounds = new Rectangle(-8, -8, 1, 1),
                TabStop = false
            };
            textInput = input;
            input.KeyDown += TextKeyDown;
            input.TextChanged += delegate { Invalidate(); };
            input.KeyUp += delegate { Invalidate(); };
            input.LostFocus += delegate
            {
                if (textInput == input)
                    FinishText(true);
            };
            Controls.Add(input);
            input.Focus();
            Invalidate();
        }

        private void TextKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Escape)
                return;
            e.SuppressKeyPress = true;
            if (e.KeyCode == Keys.Enter && e.Shift)
                textInput.SelectedText = Environment.NewLine;
            else
                FinishText(e.KeyCode == Keys.Enter);
        }

        public void FinishText(bool commit)
        {
            if (!EditingText)
                return;
            TextBox input = textInput;
            string value = input.Text;
            textInput = null;
            input.Dispose();
            Invalidate();
            if (commit && !string.IsNullOrWhiteSpace(value))
                Apply(Tool.Text, new List<Point> { textAnchor }, value, false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                FinishText(false);
                ClearScribble();
                if (viewport != null)
                    viewport.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && Drawing)
            {
                points.Clear();
                ClearScribble();
                Invalidate();
            }
        }

        private void Track(Point location, Rectangle area)
        {
            Point point = ToPixel(location, area);
            if (points[points.Count - 1] == point)
                return;
            if (Tool != Tool.Scribble && points.Count > 1)
                points[1] = point;
            else
                points.Add(point);
        }

        private Rectangle ImageArea()
        {
            int width = Math.Max(1, ClientSize.Width - 32);
            int height = Math.Max(1, ClientSize.Height - 32);
            double scale = Math.Min(3, Math.Min(width / (double)Image.Width, height / (double)Image.Height));
            int shownWidth = Math.Max(1, (int)Math.Round(Image.Width * scale));
            int shownHeight = Math.Max(1, (int)Math.Round(Image.Height * scale));
            return new Rectangle((ClientSize.Width - shownWidth) / 2, (ClientSize.Height - shownHeight) / 2,
                shownWidth, shownHeight);
        }

        private Point ToPixel(Point point)
        {
            return ToPixel(point, ImageArea());
        }

        private Point ToPixel(Point point, Rectangle area)
        {
            int x = (int)((point.X - area.X + 0.5) * Image.Width / area.Width);
            int y = (int)((point.Y - area.Y + 0.5) * Image.Height / area.Height);
            return new Point(Math.Max(0, Math.Min(Image.Width - 1, x)), Math.Max(0, Math.Min(Image.Height - 1, y)));
        }
    }
}
