using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Runtime.InteropServices;

namespace RFBotApp
{
    public partial class MainForm : Form
    {
        private bool botActive = false;
        private bool running = true;
        private Mat prevGray = new Mat();
        private Rect scanRegion = new Rect(0, 0, 0, 0);
        private readonly int minMobSize = 500;
        private readonly int scanRadius = 300;
        private readonly int motionThreshold = 25;
        private readonly Rect minimapRect = new Rect(860, 10, 110, 110); // Примерные координаты миникарты
        private Point lastMinimapDirection = new Point(-1, -1);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public MainForm()
        {
            InitializeComponent();
            Task.Run(() => BotLoop());
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // START Button
            var startButton = new Button { Text = "START", Location = new Point(10, 10), Width = 100, Height = 40 };
            startButton.Click += (s, e) =>
            {
                botActive = !botActive;
                UpdateStatus();
            };

            // EXIT Button
            var exitButton = new Button { Text = "EXIT", Location = new Point(120, 10), Width = 100, Height = 40 };
            exitButton.Click += (s, e) => Application.Exit();

            // PictureBox for display
            var pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            Controls.Add(pictureBox);

            // Status Label
            var statusLabel = new Label
            {
                Name = "statusLabel",
                Text = "Статус: Не активен",
                Location = new Point(10, 60),
                AutoSize = true
            };
            Controls.Add(statusLabel);

            Controls.Add(startButton);
            Controls.Add(exitButton);
            Controls.Add(statusLabel);

            this.Text = "RF Bot — Mob Hunter";
            this.Width = 800;
            this.Height = 600;

            this.FormClosed += (s, e) => running = false;

            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F12)
                {
                    botActive = !botActive;
                    UpdateStatus();
                }
            };

            this.ResumeLayout(false);
        }

        private void UpdateStatus()
        {
            foreach (Control c in Controls)
            {
                if (c is Label && c.Name == "statusLabel")
                {
                    c.Text = botActive ? "Статус: Активен" : "Статус: Не активен";
                    c.ForeColor = botActive ? System.Drawing.Color.Green : System.Drawing.Color.Red;
                    break;
                }
            }
        }

        private void BotLoop()
        {
            var hwnd = FindWindow(null, "[PREMIUM] RF Online");
            if (hwnd == IntPtr.Zero)
            {
                MessageBox.Show("Не найдено окно игры!");
                return;
            }

            RECT gameRect;
            GetClientRect(hwnd, out gameRect);
            scanRegion = new Rect(gameRect.Left, gameRect.Top, gameRect.Right - gameRect.Left, gameRect.Bottom - gameRect.Top);

            Mat lastFrame = new Mat();
            Mat currentFrame = new Mat();

            while (running)
            {
                try
                {
                    using (var src = CaptureGameWindow(hwnd))
                    {
                        if (src.Empty())
                            continue;

                        src.CopyTo(currentFrame);

                        var gray = new Mat();
                        Cv2.CvtColor(currentFrame, gray, ColorConversionCodes.BGR2GRAY);
                        Cv2.GaussianBlur(gray, gray, new Size(11, 11), 0);

                        if (prevGray.Empty())
                        {
                            gray.CopyTo(prevGray);
                            src.Dispose();
                            gray.Dispose();
                            continue;
                        }

                        var frameDelta = new Mat();
                        Cv2.AbsDiff(prevGray, gray, frameDelta);

                        var thresh = new Mat();
                        Cv2.Threshold(frameDelta, thresh, motionThreshold, 255, ThresholdTypes.Binary);
                        Cv2.Dilate(thresh, thresh, null);

                        var contours = Cv2.FindContoursAsArray(thresh, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                        var center = new Point(currentFrame.Width / 2, currentFrame.Height / 2);
                        var mobs = new List<Rect>();

                        foreach (var cnt in contours)
                        {
                            double area = Cv2.ContourArea(cnt);
                            if (area < minMobSize) continue;

                            Rect r = Cv2.BoundingRect(cnt);
                            Point mobCenter = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
                            double dist = Math.Sqrt(Math.Pow(mobCenter.X - center.X, 2) + Math.Pow(mobCenter.Y - center.Y, 2));
                            if (dist <= scanRadius)
                                mobs.Add(r);
                        }

                        gray.CopyTo(prevGray);
                        thresh.Dispose();
                        frameDelta.Dispose();

                        if (botActive && mobs.Count > 0)
                        {
                            AttackMob(mobs[0], hwnd);
                        }

                        var output = currentFrame.Clone();
                        foreach (var rect in mobs)
                        {
                            Cv2.Rectangle(output, rect, Scalar.Red, 2);
                        }

                        pictureBox.Invoke((MethodInvoker)delegate
                        {
                            using (var bitmap = BitmapConverter.ToBitmap(output))
                            {
                                pictureBox.Image?.Dispose();
                                pictureBox.Image = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), bitmap.PixelFormat);
                            }
                        });

                        output.Dispose();
                        src.Dispose();
                        gray.Dispose();

                        System.Threading.Thread.Sleep(700);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка: {ex.Message}");
                }
            }
        }

        private Mat CaptureGameWindow(IntPtr hwnd)
        {
            RECT gameRect;
            GetClientRect(hwnd, out gameRect);
            int width = gameRect.Right - gameRect.Left;
            int height = gameRect.Bottom - gameRect.Top;

            using (var src = new Bitmap(width, height))
            using (Graphics g = Graphics.FromImage(src))
            {
                g.CopyFromScreen(gameRect.Left, gameRect.Top, 0, 0, new Size(width, height));
                return BitmapConverter.ToMat(src);
            }
        }

        private void AttackMob(Rect mob, IntPtr hwnd)
        {
            RECT gameRect;
            GetClientRect(hwnd, out gameRect);

            int targetX = mob.X + mob.Width / 2 + gameRect.Left;
            int targetY = mob.Y + mob.Height / 2 + gameRect.Top;

            Cursor.Position = new Point(targetX, targetY);
            MouseSimulator.MouseEvent(MouseSimulator.MouseEventFlags.LEFTDOWN);
            MouseSimulator.MouseEvent(MouseSimulator.MouseEventFlags.LEFTUP);
            SendKeys.SendWait(" ");
            Console.WriteLine($"Атака на: {targetX}, {targetY}");
        }
    }
}
