using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RFBot
{
    public class BotLogic
    {
        private readonly MainForm _form;
        private bool _botActive = false;
        private readonly int _minMobSize = 500;
        private readonly int _scanRadius = 300;
        private readonly int _motionThreshold = 25;

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private Mat _prevGray = new Mat();
        private Rect _screenRegion = new Rect();

        public BotLogic(MainForm form)
        {
            _form = form;
            Task.Run(() => RunLoop());
        }

        private void ToggleBot() => _botActive = !_botActive;

        private void RunLoop()
        {
            var hwnd = FindWindow(null, "[PREMIUM] RF Online");
            if (hwnd == IntPtr.Zero)
            {
                MessageBox.Show("Не найдено окно игры!");
                return;
            }

            while (true)
            {
                if (_botActive)
                {
                    using (var frame = CaptureGameWindow(hwnd))
                    {
                        if (!frame.Empty())
                        {
                            ProcessFrame(frame);
                        }
                    }
                }

                System.Threading.Thread.Sleep(700);
            }
        }

        private Mat CaptureGameWindow(IntPtr hwnd)
        {
            GetClientRect(hwnd, out RECT gameRect);
            _screenRegion = new Rect(gameRect.Left, gameRect.Top,
                gameRect.Right - gameRect.Left,
                gameRect.Bottom - gameRect.Top);

            using (Bitmap bitmap = new Bitmap(_screenRegion.Width, _screenRegion.Height))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(_screenRegion.Location.X, _screenRegion.Location.Y, 0, 0, bitmap.Size);
                return BitmapConverter.ToMat(bitmap);
            }
        }

        private void ProcessFrame(Mat frame)
        {
            var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, gray, new Size(11, 11), 0);

            if (_prevGray.Empty())
            {
                gray.CopyTo(_prevGray);
                return;
            }

            var delta = new Mat();
            Cv2.AbsDiff(gray, _prevGray, delta);
            var thresh = new Mat();
            Cv2.Threshold(delta, thresh, _motionThreshold, 255, ThresholdTypes.Binary);
            Cv2.Dilate(thresh, thresh, null);
            var contours = Cv2.FindContoursAsArray(thresh, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            var center = new Point(frame.Width / 2, frame.Height / 2);
            foreach (var cnt in contours)
            {
                double area = Cv2.ContourArea(cnt);
                if (area < _minMobSize) continue;

                Rect r = Cv2.BoundingRect(cnt);
                Point mobCenter = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
                double dist = Math.Sqrt(Math.Pow(mobCenter.X - center.X, 2) + Math.Pow(mobCenter.Y - center.Y, 2));
                
                if (dist <= _scanRadius)
                {
                    AttackMob(r, hwnd);
                    break;
                }
            }
        }

        private void AttackMob(Rect mob, IntPtr hwnd)
        {
            GetClientRect(hwnd, out RECT gameRect);
            int targetX = mob.X + mob.Width / 2 + gameRect.Left;
            int targetY = mob.Y + mob.Height / 2 + gameRect.Top;

            Cursor.Position = new Point(targetX, targetY);
            MouseSimulator.MouseEvent(MouseSimulator.MouseEventFlags.LEFTDOWN);
            MouseSimulator.MouseEvent(MouseSimulator.MouseEventFlags.LEFTUP);
            SendKeys.SendWait(" ");
        }
    }
}
