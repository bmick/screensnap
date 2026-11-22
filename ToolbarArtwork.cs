using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ScreenSnap
{
    internal static class ToolbarArtwork
    {
        public static Bitmap Annotation(Tool tool, int pixels)
        {
            return Render(pixels, (graphics, pen) =>
            {
                switch (tool)
                {
                    case Tool.Scribble:
                        using (var stroke = new GraphicsPath())
                        {
                            stroke.AddBezier(2.5f, 15, 6, 2, 8.5f, 3, 9, 11);
                            stroke.AddBezier(9, 11, 9.5f, 19, 13, 17, 17.5f, 6);
                            graphics.DrawPath(pen, stroke);
                        }
                        break;
                    case Tool.Line:
                        graphics.DrawLine(pen, 3.5f, 16.5f, 16.5f, 3.5f);
                        break;
                    case Tool.Rectangle:
                        graphics.DrawRectangle(pen, 2.5f, 5, 15, 10);
                        break;
                    case Tool.Ellipse:
                        graphics.DrawEllipse(pen, 2.5f, 5, 15, 10);
                        break;
                    case Tool.Text:
                        graphics.DrawLine(pen, 4, 4, 16, 4);
                        graphics.DrawLine(pen, 10, 4, 10, 16);
                        graphics.DrawLine(pen, 7, 16, 13, 16);
                        break;
                    case Tool.Transparency:
                        using (var check = new SolidBrush(SystemColors.ControlDark))
                        {
                            graphics.FillRectangle(check, 4, 4, 6, 6);
                            graphics.FillRectangle(check, 10, 10, 6, 6);
                        }
                        graphics.DrawRectangle(pen, 3.5f, 3.5f, 13, 13);
                        break;
                }
            });
        }

        public static Bitmap Swatch(Color color, int pixels)
        {
            return Render(pixels, (graphics, pen) =>
            {
                using (var fill = new SolidBrush(color))
                    graphics.FillEllipse(fill, 2, 2, 16, 16);
                pen.Width = 1;
                pen.Color = SystemColors.ControlDarkDark;
                graphics.DrawEllipse(pen, 2, 2, 16, 16);
            });
        }

        public static Bitmap Undo(int pixels)
        {
            return Render(pixels, (graphics, pen) =>
            {
                using (var arrow = new GraphicsPath())
                {
                    arrow.AddLines(new[] { new PointF(7, 3), new PointF(3, 7), new PointF(12, 7) });
                    arrow.AddBezier(12, 7, 18.5f, 7, 18.5f, 16, 12, 16);
                    arrow.AddLine(12, 16, 8, 16);
                    graphics.DrawPath(pen, arrow);
                }
                graphics.DrawLine(pen, 3, 7, 7, 11);
            });
        }

        private static Bitmap Render(int pixels, Action<Graphics, Pen> draw)
        {
            var image = new Bitmap(pixels, pixels);
            using (var graphics = Graphics.FromImage(image))
            using (var pen = new Pen(SystemColors.ControlText, 1.7f))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.ScaleTransform(pixels / 20f, pixels / 20f);
                pen.StartCap = pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                draw(graphics, pen);
            }
            return image;
        }
    }
}
