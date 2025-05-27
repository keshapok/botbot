using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RFBot
{
    public partial class MainForm : Form
    {
        private BotLogic _bot;

        public MainForm()
        {
            InitializeComponent();
            _bot = new BotLogic(this);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            var startButton = new Button { Text = "START", Location = new Point(10, 10), Size = new Size(100, 40) };
            startButton.Click += (s, e) => _bot.ToggleBot();

            var exitButton = new Button { Text = "EXIT", Location = new Point(120, 10), Size = new Size(100, 40) };
            exitButton.Click += (s, e) => Application.Exit();

            var statusLabel = new Label
            {
                Name = "statusLabel",
                Text = "Статус: Не активен",
                Location = new Point(10, 60),
                AutoSize = true
            };

            Controls.Add(startButton);
            Controls.Add(exitButton);
            Controls.Add(statusLabel);

            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F12)
                {
                    _bot.ToggleBot();
                }
            };

            this.Text = "RF Bot — Mob Hunter";
            this.Width = 800;
            this.Height = 600;
            this.ResumeLayout(false);
        }

        public void UpdateStatus(string text, bool active)
        {
            foreach (Control control in Controls)
            {
                if (control is Label label && label.Name == "statusLabel")
                {
                    label.Text = text;
                    label.ForeColor = active ? System.Drawing.Color.Green : System.Drawing.Color.Red;
                    break;
                }
            }
        }
    }
}
