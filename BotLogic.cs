using OpenCvSharp;
using System;
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
        private readonly Rect _minimapRect = new Rect(860, 10, 110, 110); // Примерные координаты миникарты
        private OpenCvSharp.Point _lastMinimapDirection = new OpenCvSharp.Point(-1, -1);
        private IntPtr _hwnd;

        public BotLogic(MainForm form)
        {
            _form = form;
            _hwnd = FindWindow(null, "[PREMIUM] RF Online");
            Task.Run(() => RunLoop());
        }

        public void ToggleBot()
        {
            _botActive = !_botActive;
            _form.UpdateStatus(_botActive ? "Статус: Активен" : "Статус: Неактивен", _botActive);
        }

        private void RunLoop()
        {
            if (_hwnd == IntPtr.Zero)
            {
                MessageBox.Show("Не найдено окно игры!");
                return;
            }

            GetClientRect(_hwnd, out RECT gameRect);
            _screenRegion = new Rect(gameRect.Left, gameRect.Top,
                gameRect.Right - gameRect.Left, gameRect.Bottom - gameRect.Top);

            while (true)
            {
                using (Mat frame = CaptureGameWindow())
                {
                    if (frame.Empty())
                        continue;

                    ProcessFrame(frame, _hwnd);
                }

                Task.Delay(700).Wait();
            }
        }

        private Mat CaptureGameWindow()
        {
            using (var src = new Bitmap(_screenRegion.Width, _screenRegion.Height))
            using (Graphics g = Graphics.FromImage(src))
            {
                g.CopyFromScreen(_screenRegion.Location.X, _screenRegion.Location.Y, 0, 0, src.Size);
                return OpenCvSharp.Extensions.BitmapConverter.ToMat(src);
            }
        }

        private void ProcessFrame(Mat frame, IntPtr hwnd)
        {
            var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(11, 11), 0);

            if (_prevGray.Empty())
            {
                gray.CopyTo(_prevGray);
                return;
            }

            if (gray.Size() != _prevGray.Size())
            {
                gray.CopyTo(_prevGray);
                return;
            }

            var delta = new Mat();
            Cv2.Absdiff(gray, _prevGray, delta);  // Правильное имя метода

            var thresh = new Mat();
            Cv2.Threshold(delta, thresh, _motionThreshold, 255, ThresholdTypes.Binary);
            Cv2.Dilate(thresh, thresh, null);

            var contours = Cv2.FindContoursAsArray(thresh, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            var center = new OpenCvSharp.Point(frame.Width / 2, frame.Height / 2);
            foreach (var cnt in contours)
            {
                double area = Cv2.ContourArea(cnt);
                if (area < _minMobSize) continue;

                Rect r = Cv2.BoundingRect(cnt);
                OpenCvSharp.Point mobCenter = new OpenCvSharp.Point(r.X + r.Width / 2, r.Y + r.Height / 2);
                double dist = Math.Sqrt(Math.Pow(mobCenter.X - center.X, 2) + Math.Pow(mobCenter.Y - center.Y, 2));
                
                if (dist <= _scanRadius)
                {
                    AttackMob(r, hwnd);
                    break;
                }
            }

            gray.CopyTo(_prevGray);
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
            Console.WriteLine($"Атака на координаты: ({targetX}, {targetY})");
        }
    }
}
