using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ScreenSnap
{
    internal sealed class Editor : Form
    {
        private readonly Canvas canvas;
        private readonly ToolStrip tools;
        private readonly StatusStrip status;
        private readonly ToolStripStatusLabel message;
        private readonly ToolStripButton undo;
        private readonly List<Bitmap> toolbarImages = new List<Bitmap>();
        private readonly LinkedList<Snapshot> history = new LinkedList<Snapshot>();
        private readonly Func<byte[], string> saveNew;
        private readonly Func<int> getTolerance;
        private readonly Action<string> reportError;
        private readonly Timer publishTimer = new Timer();
        private bool savePending = true;
        private bool copyPending = true;
        private byte[] png;
        private string path;

        public Editor(Bitmap bitmap, Func<byte[], string> saveNew, Func<int> getTolerance, Action<string> reportError)
        {
            SuspendLayout();
            this.saveNew = saveNew;
            this.getTolerance = getTolerance;
            this.reportError = reportError;
            Text = "ScreenSnap";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(690, 320);
            StartPosition = FormStartPosition.CenterScreen;
            Rectangle working = Screen.FromPoint(Cursor.Position).WorkingArea;
            SizeF scale = CurrentAutoScaleDimensions;
            float horizontal = 96 / scale.Width;
            float vertical = 96 / scale.Height;
            ClientSize = new Size(Math.Min((int)((working.Width - 32) * horizontal), Math.Max(780, (int)(bitmap.Width * horizontal) + 48)),
                Math.Min((int)((working.Height - 64) * vertical), Math.Max(450, (int)(bitmap.Height * vertical) + 124)));

            canvas = new Canvas { Dock = DockStyle.Fill, Image = bitmap };
            canvas.Apply = Apply;
            tools = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding((int)Math.Round(12 / horizontal), (int)Math.Round(8 / vertical),
                    (int)Math.Round(12 / horizontal), (int)Math.Round(8 / vertical)),
                BackColor = SystemColors.ControlLightLight };
            int glyphPixels = (int)Math.Round(20 / horizontal);
            foreach (Tool tool in Enum.GetValues(typeof(Tool)))
            {
                string label = tool == Tool.Transparency ? "Make transparent" : tool.ToString();
                var button = ImageButton(label, ToolbarArtwork.Annotation(tool, glyphPixels));
                button.Tag = tool;
                button.Checked = tool == Tool.Scribble;
                button.ToolTipText = tool == Tool.Transparency ? "Click a color to remove matching pixels connected to it" : label;
                if (tool == Tool.Rectangle || tool == Tool.Ellipse)
                    button.ToolTipText = label + " (hold Shift to constrain)";
                if (tool == Tool.Text)
                    button.ToolTipText = "Text (click to type, Enter to place, Shift+Enter for a new line)";
                button.Click += SelectTool;
                tools.Items.Add(button);
            }
            tools.Items.Add(new ToolStripSeparator());
            var crayons = new[]
            {
                new { Name = "Red", Rgb = 0xEE204D },
                new { Name = "Orange", Rgb = 0xFF7538 },
                new { Name = "Yellow", Rgb = 0xFCE883 },
                new { Name = "Green", Rgb = 0x1CAC78 },
                new { Name = "Blue", Rgb = 0x1F75FE },
                new { Name = "Violet", Rgb = 0x926EAE },
                new { Name = "Brown", Rgb = 0xB4674D },
                new { Name = "White", Rgb = 0xFFFFFF },
                new { Name = "Gray", Rgb = 0x95918C },
                new { Name = "Black", Rgb = 0x000000 }
            };
            for (int index = 0; index < crayons.Length; index++)
            {
                var crayon = crayons[index];
                Color color = Color.FromArgb(unchecked((int)0xFF000000) | crayon.Rgb);
                var swatch = ImageButton(crayon.Name, ToolbarArtwork.Swatch(color, glyphPixels));
                swatch.Tag = color;
                swatch.ToolTipText = crayon.Name + " ink";
                swatch.Click += SelectInk;
                tools.Items.Add(swatch);
                if (index == 0)
                {
                    swatch.Checked = true;
                    canvas.Ink = color;
                }
            }
            tools.Items.Add(new ToolStripSeparator());
            var stroke = new TrackBar { Minimum = 1, Maximum = 16, Value = canvas.Stroke, TickStyle = TickStyle.None,
                SmallChange = 1, LargeChange = 3, AutoSize = false, BackColor = tools.BackColor,
                AccessibleName = "Weight, thin to thick",
                Size = new Size((int)Math.Round(118 / horizontal), (int)Math.Round(32 / vertical)) };
            stroke.ValueChanged += delegate
            {
                canvas.FinishText(true);
                canvas.Stroke = stroke.Value;
            };
            tools.Items.Add(new ToolStripLabel("Thin") { ForeColor = SystemColors.GrayText });
            tools.Items.Add(new ToolStripControlHost(stroke) { AutoSize = false, Size = stroke.Size });
            tools.Items.Add(new ToolStripLabel("Thick") { ForeColor = SystemColors.GrayText });
            tools.Items.Add(new ToolStripSeparator());
            undo = ImageButton("Undo", ToolbarArtwork.Undo(glyphPixels));
            undo.Enabled = false;
            undo.ToolTipText = "Undo (Ctrl+Z)";
            undo.Click += delegate { Undo(); };
            tools.Items.Add(undo);

            status = new StatusStrip { SizingGrip = false, ShowItemToolTips = true, BackColor = Color.FromArgb(38, 47, 61),
                ForeColor = Color.FromArgb(201, 211, 225), Renderer = new FooterRenderer(),
                Padding = new Padding(12, 0, 12, 0) };
            message = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft, AutoToolTip = false,
                Margin = new Padding(0, 5, 8, 5) };
            status.Items.Add(message);
            status.Items.Add(new ToolStripStatusLabel(bitmap.Width + " × " + bitmap.Height + " px")
            {
                ForeColor = Color.FromArgb(177, 192, 210), Margin = new Padding(0, 5, 0, 5)
            });
            Controls.Add(canvas);
            Controls.Add(tools);
            Controls.Add(status);
            publishTimer.Tick += delegate
            {
                publishTimer.Stop();
                if (canvas.Drawing)
                    Schedule(500);
                else
                    Publish(false);
            };
            ResumeLayout(true);
        }

        public void PublishInitial()
        {
            Publish(false);
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (!canvas.EditingText && keyData == (Keys.Control | Keys.Z))
            {
                Undo();
                return true;
            }
            return base.ProcessCmdKey(ref message, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            canvas.FinishText(true);
            publishTimer.Stop();
            Publish(true);
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                publishTimer.Dispose();
                foreach (Bitmap bitmap in history.Select(state => state.Image).Distinct())
                {
                    if (bitmap == canvas.Image)
                        continue;
                    bitmap.Dispose();
                }
                history.Clear();
                if (canvas != null && canvas.Image != null)
                {
                    canvas.Image.Dispose();
                    canvas.Image = null;
                }
            }
            base.Dispose(disposing);
            if (disposing)
            {
                foreach (Bitmap image in toolbarImages)
                    image.Dispose();
                toolbarImages.Clear();
            }
        }

        private ToolStripButton ImageButton(string label, Bitmap image)
        {
            toolbarImages.Add(image);
            return new ToolStripButton(label, image)
            {
                AccessibleName = label,
                DisplayStyle = ToolStripItemDisplayStyle.Image,
                ImageScaling = ToolStripItemImageScaling.None,
                Padding = new Padding(Math.Max(2, image.Width / 5))
            };
        }

        private void SelectTool(object sender, EventArgs e)
        {
            canvas.FinishText(true);
            var button = (ToolStripButton)sender;
            canvas.Tool = (Tool)button.Tag;
            foreach (ToolStripButton item in tools.Items.OfType<ToolStripButton>())
                if (item.Tag is Tool)
                    item.Checked = item == button;
        }

        private void SelectInk(object sender, EventArgs e)
        {
            canvas.FinishText(true);
            var swatch = (ToolStripButton)sender;
            canvas.Ink = (Color)swatch.Tag;
            foreach (ToolStripButton item in tools.Items.OfType<ToolStripButton>())
                if (item.Tag is Color)
                    item.Checked = item == swatch;
        }

        private void Apply(Tool tool, List<Point> points, string text, bool constrain)
        {
            Bitmap image = canvas.Image;
            Bitmap modified = null;
            try
            {
                var previous = new Snapshot(image, canvas.Marks);
                if (tool == Tool.Transparency)
                {
                    modified = canvas.RenderImage();
                    if (!Annotations.ClearConnected(modified, points[0], getTolerance()))
                        return;
                    canvas.Image = modified;
                    canvas.Marks = null;
                    modified = null;
                }
                else
                    canvas.Marks = new AnnotationMark(canvas.Marks, tool, points, canvas.Ink, canvas.Stroke, text, constrain);
                history.AddLast(previous);
                long imageBytes = 4L * image.Width * image.Height;
                while (history.Count > 1 && (history.Count > 25 ||
                    history.Select(state => state.Image).Distinct().Count() * imageBytes > 128L * 1024 * 1024))
                {
                    Bitmap oldest = history.First.Value.Image;
                    history.RemoveFirst();
                    ReleaseImage(oldest);
                }
                Changed();
            }
            catch (Exception ex)
            {
                message.Text = "Could not apply annotation. " + ex.Message;
            }
            finally
            {
                if (modified != null)
                    modified.Dispose();
            }
        }

        private void Undo()
        {
            canvas.FinishText(true);
            if (history.Count == 0 || canvas.Drawing)
                return;
            Bitmap discarded = canvas.Image;
            canvas.Image = history.Last.Value.Image;
            canvas.Marks = history.Last.Value.Marks;
            history.RemoveLast();
            ReleaseImage(discarded);
            Changed();
        }

        private void ReleaseImage(Bitmap bitmap)
        {
            if (bitmap != canvas.Image && !history.Any(state => state.Image == bitmap))
                bitmap.Dispose();
        }

        private sealed class Snapshot
        {
            public readonly Bitmap Image;
            public readonly AnnotationMark Marks;

            public Snapshot(Bitmap image, AnnotationMark marks)
            {
                Image = image;
                Marks = marks;
            }
        }

        private void Changed()
        {
            canvas.Invalidate();
            undo.Enabled = history.Count > 0;
            savePending = copyPending = true;
            png = null;
            message.Text = "Saving…";
            message.ToolTipText = path;
            Schedule(500);
        }

        private void Schedule(int milliseconds)
        {
            publishTimer.Stop();
            publishTimer.Interval = milliseconds;
            publishTimer.Start();
        }

        private void Publish(bool closing)
        {
            if (!savePending && !copyPending)
                return;
            string saveError = null;
            string copyError = null;
            Bitmap rendered = null;
            try
            {
                if (png == null || copyPending)
                    rendered = canvas.RenderImage();
                if (png == null)
                    png = Storage.Encode(rendered);
            }
            catch (Exception ex)
            {
                saveError = "Could not encode PNG. " + ex.Message;
            }
            using (rendered)
            {
                // Copy first so a slow disk or network directory cannot delay the clipboard.
                if (png != null && copyPending && rendered != null)
                {
                    try
                    {
                        CopyImage(rendered, png);
                        copyPending = false;
                    }
                    catch (Exception ex)
                    {
                        copyError = "Could not copy to clipboard. " + ex.Message;
                    }
                }
                if (png != null && savePending)
                {
                    try
                    {
                        if (path == null)
                        {
                            path = saveNew(png);
                            Text = Path.GetFileName(path) + " — ScreenSnap";
                        }
                        else
                            Storage.Replace(path, png);
                        savePending = false;
                    }
                    catch (Exception ex)
                    {
                        saveError = "Could not autosave PNG. " + ex.Message;
                    }
                }
            }
            message.Text = saveError != null ? "Autosave failed" : copyError != null ? "Clipboard copy failed" : "Saved and copied";
            message.ToolTipText = saveError ?? copyError ?? path;
            if (closing)
            {
                if (saveError != null || copyError != null)
                    reportError(saveError ?? copyError);
                return;
            }
            if (copyPending || savePending)
                Schedule(savePending ? 2000 : 350);
            else
                png = null;
        }

        private sealed class FooterRenderer : ToolStripRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                e.Graphics.Clear(e.ToolStrip.BackColor);
            }
        }

        internal static void CopyImage(Bitmap bitmap, byte[] png)
        {
            using (var pngStream = new MemoryStream(png, false))
            {
                var data = new DataObject();
                data.SetData("PNG", false, pngStream);
                data.SetData(DataFormats.Bitmap, true, bitmap);
                Clipboard.SetDataObject(data, true, 3, 35);
            }
        }
    }
}
