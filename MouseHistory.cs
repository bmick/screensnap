using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ScreenSnap
{
    internal sealed class MouseHistory
    {
        private static readonly uint SampleSize = (uint)Marshal.SizeOf(typeof(Sample));
        private readonly Sample[] samples = new Sample[64];
        private Point previous;
        private int previousTime;

        public void Begin(Point screen)
        {
            previous = screen;
            previousTime = GetMessageTime();
        }

        public void Read(Point screen, Action<Point> add)
        {
            int time = GetMessageTime();
            var anchor = new Sample { X = screen.X & 0xffff, Y = screen.Y & 0xffff, Time = time };
            int count = GetMouseMovePointsEx(SampleSize, ref anchor, samples, samples.Length, 1);
            // The first sample is the current message. Walk backward to find the previous boundary,
            // then replay the omitted positions in chronological order.
            int older = 1;
            while (older < count && unchecked(samples[older].Time - previousTime) >= 0 &&
                (samples[older].Time != previousTime || samples[older].Position != previous))
                older++;
            for (int i = older - 1; i > 0; i--)
                add(samples[i].Position);
            add(screen);
            previous = screen;
            previousTime = time;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Sample
        {
            public int X, Y, Time;
            public UIntPtr ExtraInfo;
            public Point Position { get { return new Point(unchecked((short)X), unchecked((short)Y)); } }
        }

        [DllImport("user32.dll")]
        private static extern int GetMessageTime();

        [DllImport("user32.dll")]
        private static extern int GetMouseMovePointsEx(uint size, ref Sample anchor, [Out] Sample[] buffer,
            int count, uint resolution);
    }
}
