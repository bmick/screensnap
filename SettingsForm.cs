using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ScreenSnap
{
    internal sealed class SettingsForm : Form
    {
        private readonly Preferences original;
        private readonly TextBox folder;
        private readonly TextBox pattern;
        private readonly Label preview;
        private readonly Label error;
        private readonly CheckBox startup;
        private readonly CheckBox alt;
        private readonly CheckBox ctrl;
        private readonly CheckBox shift;
        private readonly CheckBox win;
        private readonly ComboBox key;
        private readonly TextBox tolerance;
        private readonly Action<Preferences> apply;

        public SettingsForm(Preferences original, Action<Preferences> apply)
        {
            SuspendLayout();
            this.original = original;
            this.apply = apply;
            Text = "ScreenSnap settings";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(590, 376);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(layout);

            var hotkey = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            ctrl = Modifier(hotkey, "Ctrl", 2);
            alt = Modifier(hotkey, "Alt", 1);
            shift = Modifier(hotkey, "Shift", 4);
            win = Modifier(hotkey, "Win", 8);
            key = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, AccessibleName = "Hotkey key" };
            for (int i = 0; i < 10; i++)
                key.Items.Add(new KeyChoice(i.ToString(), Keys.D0 + i));
            for (int i = 0; i < 26; i++)
                key.Items.Add(new KeyChoice(((char)('A' + i)).ToString(), Keys.A + i));
            for (int i = 0; i < 24; i++)
                key.Items.Add(new KeyChoice("F" + (i + 1), Keys.F1 + i));
            foreach (Keys extra in new[] { Keys.Insert, Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown, Keys.Space, Keys.Snapshot })
                key.Items.Add(new KeyChoice(extra == Keys.Snapshot ? "Print Screen" : extra.ToString(), extra));
            key.SelectedIndex = 1;
            for (int i = 0; i < key.Items.Count; i++)
                if ((int)((KeyChoice)key.Items[i]).Value == original.Hotkey)
                    key.SelectedIndex = i;
            hotkey.Controls.Add(key);
            AddRow(layout, "Capture hotkey", hotkey, 34);

            startup = new CheckBox { Text = "Run ScreenSnap when I sign in", Checked = original.StartWithWindows, AutoSize = true };
            AddRow(layout, "Startup", startup, 33);

            var folderRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            folder = new TextBox { Text = original.Folder, Dock = DockStyle.Fill, AccessibleName = "Autosave folder" };
            var browse = new Button { Text = "Browse…", AutoSize = true };
            browse.Click += delegate
            {
                using (var dialog = new FolderBrowserDialog { SelectedPath = folder.Text, Description = "Choose the screenshot folder" })
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        folder.Text = dialog.SelectedPath;
            };
            folderRow.Controls.Add(folder);
            folderRow.Controls.Add(browse);
            AddRow(layout, "Autosave folder", folderRow, 38);

            pattern = new TextBox { Text = original.Template, Dock = DockStyle.Top, AccessibleName = "Filename pattern" };
            AddRow(layout, "Filename pattern", pattern, 31);
            var hint = new Label { Text = @"{yyyy} year, {mm} month, {dd} day, {hh} hour, {min} minute, {ss} second, {nnn} sequence. Use \ for subfolders.",
                ForeColor = SystemColors.GrayText, Dock = DockStyle.Fill };
            AddRow(layout, "", hint, 43);
            preview = new Label { AutoEllipsis = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            AddRow(layout, "Preview", preview, 31);
            pattern.TextChanged += delegate { UpdatePreview(); };
            UpdatePreview();

            tolerance = new TextBox { Text = Math.Max(0, Math.Min(100, original.Tolerance)).ToString(),
                Width = 65, MaxLength = 3, AccessibleName = "Transparency tolerance percentage" };
            AddRow(layout, "Color tolerance (%)", tolerance, 31);

            error = new Label { ForeColor = Color.Firebrick, Dock = DockStyle.Fill };
            int errorRow = AddRow(layout, null, error, 33);
            layout.SetColumnSpan(error, 2);
            layout.SetCellPosition(error, new TableLayoutPanelCellPosition(0, errorRow));

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 86 };
            var save = new Button { Text = "Apply", Width = 86 };
            save.Click += delegate { ApplyChanges(); };
            actions.Controls.Add(cancel);
            actions.Controls.Add(save);
            AddRow(layout, "", actions, 33);
            AcceptButton = save;
            CancelButton = cancel;
            ResumeLayout(true);
        }

        private CheckBox Modifier(Control parent, string label, uint bit)
        {
            var button = new CheckBox { Text = label, AutoSize = true, Checked = (original.Modifiers & bit) != 0,
                Margin = new Padding(3, 6, 5, 2) };
            parent.Controls.Add(button);
            return button;
        }

        private static int AddRow(TableLayoutPanel layout, string name, Control control, int height)
        {
            int row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, height));
            if (name != null)
                layout.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft,
                    Padding = new Padding(0, 5, 0, 0) }, 0, row);
            layout.Controls.Add(control, name == null ? 0 : 1, row);
            return row;
        }

        private void UpdatePreview()
        {
            try
            {
                preview.Text = Storage.Expand(pattern.Text.Trim(), DateTime.Now, Math.Max(1, original.NextNumber));
                preview.ForeColor = SystemColors.ControlText;
            }
            catch (FormatException ex)
            {
                preview.Text = ex.Message;
                preview.ForeColor = Color.Firebrick;
            }
        }

        private void ApplyChanges()
        {
            try
            {
                var changes = original.Copy();
                changes.Template = pattern.Text.Trim();
                Storage.Expand(changes.Template, DateTime.Now, 1);
                string entered = folder.Text.Trim();
                if (entered.Length < 3 || !Path.IsPathRooted(entered) || Path.GetPathRoot(entered).Length <= 1 ||
                    (entered[1] == ':' && entered[2] != '\\' && entered[2] != '/'))
                    throw new FormatException("Enter a full path for the autosave folder.");
                changes.Folder = Path.GetFullPath(entered);
                changes.StartWithWindows = startup.Checked;
                changes.Hotkey = (int)((KeyChoice)key.SelectedItem).Value;
                changes.Modifiers = (alt.Checked ? 1u : 0) | (ctrl.Checked ? 2u : 0) | (shift.Checked ? 4u : 0) | (win.Checked ? 8u : 0);
                if (changes.Modifiers == 0 && changes.Hotkey != (int)Keys.Snapshot &&
                    (changes.Hotkey < (int)Keys.F1 || changes.Hotkey > (int)Keys.F24))
                    throw new FormatException("Choose a modifier for this key.");
                int percent;
                if (!int.TryParse(tolerance.Text, out percent) || percent < 0 || percent > 100)
                    throw new FormatException("Enter a color tolerance from 0 to 100.");
                changes.Tolerance = percent;
                apply(changes);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                error.Text = ex.Message;
            }
        }

        private sealed class KeyChoice
        {
            private readonly string label;
            public Keys Value { get; private set; }

            public KeyChoice(string label, Keys value)
            {
                this.label = label;
                Value = value;
            }

            public override string ToString()
            {
                return label;
            }
        }
    }
}
