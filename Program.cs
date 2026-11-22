using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenSnap
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool first;
            using (var instance = new Mutex(true, @"Local\ScreenSnap.7B8A2862", out first))
            {
                if (!first)
                    return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenSnap");
                using (var app = new TrayApp(new Storage(profile), true))
                    Application.Run(app);
            }
        }
    }

    internal sealed class TrayApp : ApplicationContext
    {
        private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupName = "ScreenSnap";
        private readonly Storage storage;
        private readonly bool manageStartup;
        private readonly HotkeyWindow hotkeys = new HotkeyWindow();
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip menu;
        private readonly Icon icon;
        private readonly Icon trayIcon;
        private Preferences preferences;
        private Editor editor;
        private int hotkeyId;
        private bool busy;
        private bool exiting;

        public TrayApp(Storage storage, bool manageStartup)
        {
            this.storage = storage;
            this.manageStartup = manageStartup;
            string error = null;
            try
            {
                preferences = storage.Load();
            }
            catch (Exception ex)
            {
                preferences = new Preferences();
                error = "Could not read settings. " + ex.Message;
            }

            menu = new TrayMenu();
            menu.Items.Add("Capture region", null, delegate { CaptureRegion(); });
            menu.Items.Add("Settings…", null, delegate { ShowSettings(); });
            menu.Items.Add("Open screenshots folder", null, delegate { OpenFolder(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit ScreenSnap", null, delegate { ExitThread(); });
            float scale = DesktopAppearance.ScaleAt(Screen.PrimaryScreen.Bounds.Location);
            int taskbarPixels = (int)Math.Round(32 * scale);
            int trayPixels = (int)Math.Round(16 * scale);
            icon = DesktopAppearance.LoadAppIcon(new Size(taskbarPixels, taskbarPixels));
            trayIcon = DesktopAppearance.LoadAppIcon(new Size(trayPixels, trayPixels));
            tray = new NotifyIcon { Icon = trayIcon, Text = "ScreenSnap", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { CaptureRegion(); };
            hotkeys.Pressed += delegate(int id)
            {
                if (id == hotkeyId)
                    CaptureRegion();
            };
            try
            {
                hotkeyId = hotkeys.Register(1, preferences);
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
            }
            try
            {
                SetStartup(preferences.StartWithWindows);
            }
            catch (Exception ex)
            {
                error = "Could not update Windows startup. " + ex.Message;
            }
            if (error != null)
                Report(error);
        }

        internal void CaptureRegion()
        {
            if (busy || exiting || (editor != null && !editor.Enabled))
                return;
            busy = true;
            Bitmap image = null;
            try
            {
                Rectangle desktop = SystemInformation.VirtualScreen;
                DateTime capturedAt = DateTime.Now;
                using (Bitmap frozen = Capture.Freeze(desktop))
                using (var overlay = new Capture(frozen, desktop))
                {
                    if (overlay.ShowDialog() != DialogResult.OK)
                        return;
                    Rectangle region = overlay.Selection;
                    image = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
                    using (var graphics = Graphics.FromImage(image))
                        graphics.DrawImage(frozen, new Rectangle(Point.Empty, image.Size), region, GraphicsUnit.Pixel);
                }

                if (editor != null)
                    editor.Close();
                var next = new Editor(image, png => SaveCapture(capturedAt, png), () => preferences.Tolerance, Report);
                image = null;
                editor = next;
                editor.Icon = icon;
                editor.FormClosed += delegate
                {
                    if (editor == next)
                        editor = null;
                };
                editor.PublishInitial();
                editor.Show();
                editor.Activate();
            }
            catch (Exception ex)
            {
                Report("Could not capture the screen. " + ex.Message);
            }
            finally
            {
                if (image != null)
                    image.Dispose();
                busy = false;
            }
        }

        protected override void ExitThreadCore()
        {
            exiting = true;
            if (editor != null)
                editor.Close();
            tray.Visible = false;
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tray.Dispose();
                menu.Dispose();
                icon.Dispose();
                trayIcon.Dispose();
                hotkeys.Dispose();
            }
            base.Dispose(disposing);
        }

        private string SaveCapture(DateTime capturedAt, byte[] png)
        {
            long before = preferences.NextNumber;
            string path = storage.SaveNew(preferences, capturedAt, png);
            if (before != preferences.NextNumber)
            {
                try
                {
                    storage.Save(preferences);
                }
                catch (Exception ex)
                {
                    Report("The PNG was saved. Could not store the sequence counter. " + ex.Message);
                }
            }
            return path;
        }

        private void ShowSettings()
        {
            if (busy)
                return;
            busy = true;
            try
            {
                using (var settings = new SettingsForm(preferences, ApplySettings))
                {
                    settings.Icon = icon;
                    settings.ShowDialog();
                }
            }
            finally
            {
                busy = false;
            }
        }

        private void ApplySettings(Preferences changes)
        {
            bool changeHotkey = hotkeyId == 0 || preferences.Hotkey != changes.Hotkey || preferences.Modifiers != changes.Modifiers;
            int replacement = changeHotkey ? hotkeys.Register(hotkeyId == 1 ? 2 : 1, changes) : hotkeyId;
            bool changedStartup = false;
            try
            {
                if (preferences.StartWithWindows != changes.StartWithWindows)
                {
                    SetStartup(changes.StartWithWindows);
                    changedStartup = true;
                }
                changes.NextNumber = preferences.NextNumber;
                storage.Save(changes);
            }
            catch
            {
                if (changeHotkey)
                    hotkeys.Unregister(replacement);
                if (changedStartup)
                    SetStartup(preferences.StartWithWindows);
                throw;
            }
            if (changeHotkey && hotkeyId != 0)
                hotkeys.Unregister(hotkeyId);
            hotkeyId = replacement;
            preferences = changes;
        }

        private void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(preferences.Folder);
                Process.Start(new ProcessStartInfo(preferences.Folder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Report("Could not open the screenshot folder. " + ex.Message);
            }
        }

        private void SetStartup(bool enabled)
        {
            if (!manageStartup)
                return;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupKey, true) ??
                Registry.CurrentUser.CreateSubKey(StartupKey))
            {
                if (!enabled)
                {
                    key.DeleteValue(StartupName, false);
                    return;
                }
                string command = "\"" + Application.ExecutablePath + "\"";
                if (!string.Equals(key.GetValue(StartupName) as string, command, StringComparison.Ordinal))
                    key.SetValue(StartupName, command, RegistryValueKind.String);
            }
        }

        private void Report(string message)
        {
            tray.ShowBalloonTip(5000, "ScreenSnap", message, ToolTipIcon.Warning);
        }
    }

    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        public event Action<int> Pressed;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams { Parent = new IntPtr(-3), Caption = "ScreenSnap hotkey" });
        }

        public int Register(int id, Preferences settings)
        {
            if (!RegisterHotKey(Handle, id, settings.Modifiers | 0x4000, (uint)settings.Hotkey))
                throw new InvalidOperationException("Could not register the capture hotkey. Choose a different shortcut in Settings.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            return id;
        }

        public void Unregister(int id)
        {
            UnregisterHotKey(Handle, id);
        }

        protected override void WndProc(ref Message message)
        {
            const int WM_HOTKEY = 0x0312;
            if (message.Msg == WM_HOTKEY && Pressed != null)
                Pressed(message.WParam.ToInt32());
            else
                base.WndProc(ref message);
        }

        public void Dispose()
        {
            DestroyHandle();
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterHotKey(IntPtr handle, int id);
    }
}
