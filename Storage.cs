using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace ScreenSnap
{
    public sealed class Preferences
    {
        public string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ScreenSnap");
        public string Template = @"{yyyy}\{mm}-{dd}-{nnn}";
        public int Hotkey = (int)Keys.D1;
        public uint Modifiers = 1;
        public bool StartWithWindows = true;
        public int Tolerance = 5;
        public long NextNumber = 1;

        public Preferences Copy()
        {
            return (Preferences)MemberwiseClone();
        }
    }

    internal sealed class Storage
    {
        private static readonly Regex Tokens = new Regex(@"\{([^{}]+)\}");
        private static readonly Regex Reserved = new Regex(
            @"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\.|$)", RegexOptions.IgnoreCase);
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(Preferences));
        private readonly string settingsPath;

        public Storage(string profile)
        {
            settingsPath = Path.Combine(profile, "settings.xml");
        }

        public Preferences Load()
        {
            if (!File.Exists(settingsPath))
                return new Preferences();
            using (var stream = File.OpenRead(settingsPath))
                return (Preferences)Serializer.Deserialize(stream);
        }

        public void Save(Preferences preferences)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, preferences);
                Replace(settingsPath, stream.ToArray());
            }
        }

        public string SaveNew(Preferences preferences, DateTime now, byte[] png)
        {
            bool numbered = preferences.Template.IndexOf("{nnn}", StringComparison.OrdinalIgnoreCase) >= 0;
            long number = Math.Max(1, preferences.NextNumber);
            for (int duplicate = 1; ; duplicate++)
            {
                string relative = Expand(preferences.Template, now, number);
                string path = Path.Combine(preferences.Folder, relative);
                if (!numbered && duplicate > 1)
                    path = Path.Combine(Path.GetDirectoryName(path),
                        Path.GetFileNameWithoutExtension(path) + "-" + duplicate.ToString("D3", CultureInfo.InvariantCulture) + ".png");
                string temporary = WriteTemporary(path, png);
                try
                {
                    File.Move(temporary, path);
                    if (numbered)
                        preferences.NextNumber = number + 1;
                    return path;
                }
                catch (IOException)
                {
                    if (!File.Exists(path))
                        throw;
                    if (numbered)
                        number++;
                }
                finally
                {
                    File.Delete(temporary);
                }
            }
        }

        public static string Expand(string template, DateTime time, long number)
        {
            if (string.IsNullOrWhiteSpace(template))
                throw new FormatException("Enter a filename pattern.");
            bool expectingMinute = false;
            int previousEnd = 0;
            string value = Tokens.Replace(template, delegate(Match match)
            {
                string separator = template.Substring(previousEnd, match.Index - previousEnd);
                bool inTime = expectingMinute && separator.IndexOfAny(new[] { '\\', '/' }) < 0;
                string token = match.Groups[1].Value;
                previousEnd = match.Index + match.Length;
                expectingMinute = token.Equals("hh", StringComparison.OrdinalIgnoreCase);
                switch (token.ToLowerInvariant())
                {
                    case "yyyy": return time.ToString("yyyy", CultureInfo.InvariantCulture);
                    case "mm": return time.ToString(token == "MM" || !inTime ? "MM" : "mm", CultureInfo.InvariantCulture);
                    case "dd": return time.ToString("dd", CultureInfo.InvariantCulture);
                    case "hh": return time.ToString("HH", CultureInfo.InvariantCulture);
                    case "min": return time.ToString("%m", CultureInfo.InvariantCulture).PadLeft(2, '0');
                    case "ss": return time.ToString("ss", CultureInfo.InvariantCulture);
                    case "nnn": return number.ToString("D3", CultureInfo.InvariantCulture);
                    default: throw new FormatException("Unknown filename token: " + match.Value);
                }
            });
            if (!value.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                value += ".png";
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (string part in value.Split('\\', '/'))
            {
                if (part.Length == 0 || part.EndsWith(".") || part.EndsWith(" ") ||
                    part.IndexOfAny(invalid) >= 0 || part.IndexOfAny(new[] { '{', '}' }) >= 0 ||
                    Reserved.IsMatch(part))
                    throw new FormatException("Use a relative Windows filename pattern with valid folder and file names.");
            }
            return value.Replace('/', '\\');
        }

        public static byte[] Encode(Bitmap bitmap)
        {
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        public static void Replace(string path, byte[] bytes)
        {
            string temporary = WriteTemporary(path, bytes);
            try
            {
                if (!MoveFileEx(temporary, path, 1 | 8))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        private static string WriteTemporary(string path, byte[] contents)
        {
            string folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            string temporary = Path.Combine(folder, ".screensnap-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    stream.Write(contents, 0, contents.Length);
                return temporary;
            }
            catch
            {
                File.Delete(temporary);
                throw;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveFileEx(string existing, string replacement, int flags);
    }
}
