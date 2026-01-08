using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
    public class QuizForm : Form
    {
        private readonly AppSettings _settings;
        private readonly QuizEngine _quizEngine;
        private readonly GameSessionManager _session;

        // Battle State
        private int _a, _b, _monsterHealth, _maxMonsterHealth;
        private bool _solved = false;

        // Entities & UI
        private PictureBox _picKnight = null!;
        private PictureBox _picMonster = null!;
        private PictureBox _die1 = null!, _die2 = null!;
        private FantasyXpBar _monsterHealthBar = null!;
        private FantasyXpBar _playerXpBar = null!;
        private Label _lblQuestion = null!, _lblFeedback = null!, _lblLevel = null!;
        private TextBox _txtAnswer = null!;
        private Button _btnSubmit = null!;

        public QuizForm(AppSettings settings)
        {
            _settings = settings ?? new AppSettings();
            _quizEngine = new QuizEngine(_settings);
            _session = new GameSessionManager(_settings, _quizEngine);

            InitializeCombatUi();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            SpawnMonster();
            GenerateQuestion();
            UpdatePlayerStats();
        }

        private void InitializeCombatUi()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnSubmit_Click(null, null); };

            // 1. Background
            try
            {
                string bgPath = AssetPaths.Background("Background.png");
                if (File.Exists(bgPath))
                {
                    this.BackgroundImage = Image.FromFile(bgPath);
                    this.BackgroundImageLayout = ImageLayout.Stretch;
                }
            }
            catch { this.BackColor = Color.ForestGreen; }

            // 2. Knight (Left)
            _picKnight = new PictureBox
            {
                Size = new Size(350, 450),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            // 3. Monster (Right)
            _picMonster = new PictureBox
            {
                Size = new Size(350, 450),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            // 4. Dice (Center Top)
            _die1 = new PictureBox { Size = new Size(100, 100), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _die2 = new PictureBox { Size = new Size(100, 100), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };

            // 5. Monster Health Bar
            _monsterHealthBar = new FantasyXpBar { Width = 300, Height = 25 };

            // 6. Player UI
            _playerXpBar = new FantasyXpBar { Width = 300, Height = 20 };
            _lblLevel = new Label { Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.White, AutoSize = true };

            // 7. Input Area (Clean single-line style)
            _txtAnswer = new TextBox
            {
                Font = new Font("Segoe UI", 32),
                Width = 220,
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.FromArgb(245, 240, 220),
                BorderStyle = BorderStyle.None,
                Multiline = false
            };

            _btnSubmit = new Button
            {
                Text = "ATTACK",
                Size = new Size(160, 60),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.DarkRed,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };
            _btnSubmit.Click += BtnSubmit_Click;

            _lblQuestion = new Label { Font = new Font("Segoe UI", 24, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, TextAlign = ContentAlignment.MiddleCenter };
            _lblFeedback = new Label { Font = new Font("Segoe UI", 18), ForeColor = Color.Yellow, AutoSize = true, BackColor = Color.Transparent };

            this.Controls.AddRange(new Control[] { _picKnight, _picMonster, _die1, _die2, _monsterHealthBar, _playerXpBar, _lblLevel, _txtAnswer, _btnSubmit, _lblQuestion, _lblFeedback });

            this.Resize += (s, e) => LayoutCombat();
        }

        private void LayoutCombat()
        {
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            // Positioning entities
            _picKnight.Location = new Point((int)(w * 0.12), (int)(h * 0.42));
            _picMonster.Location = new Point((int)(w * 0.62), (int)(h * 0.42));

            // Dice - Higher and spread apart
            _die1.Location = new Point(w / 2 - 120, 60);
            _die2.Location = new Point(w / 2 + 20, 60);

            // Labels placed explicitly below dice
            _lblQuestion.Location = new Point(w / 2 - _lblQuestion.Width / 2, _die1.Bottom + 20);
            _lblFeedback.Location = new Point(w / 2 - _lblFeedback.Width / 2, _lblQuestion.Top - 45);

            // Health Bars anchored to monster/player
            _monsterHealthBar.Location = new Point(_picMonster.Left + (_picMonster.Width / 2) - (_monsterHealthBar.Width / 2), _picMonster.Top - 40);
            _playerXpBar.Location = new Point(30, h - 50);
            _lblLevel.Location = new Point(30, _playerXpBar.Top - 35);

            // Input and Submit
            _txtAnswer.Location = new Point(w / 2 - _txtAnswer.Width / 2, h - 170);
            _btnSubmit.Location = new Point(w / 2 - _btnSubmit.Width / 2, _txtAnswer.Bottom + 15);
        }

        private void SpawnMonster()
        {
            _maxMonsterHealth = 50 + (_settings.PlayerProgress.Level * 25);
            _monsterHealth = _maxMonsterHealth;
            _monsterHealthBar.Maximum = _maxMonsterHealth;
            _monsterHealthBar.Value = _monsterHealth;

            try
            {
                string monsterPath = Path.Combine(AssetPaths.AssetsRoot, "Monsters", "slime.png");
                if (File.Exists(monsterPath))
                {
                    _picMonster.Image = Image.FromFile(monsterPath);
                }
                else
                {
                    // Placeholder box if asset doesn't exist yet
                    Bitmap bmp = new Bitmap(_picMonster.Width, _picMonster.Height);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.FillEllipse(Brushes.Purple, 50, 50, 250, 250);
                        g.DrawString("NEW MONSTER", new Font("Arial", 12, FontStyle.Bold), Brushes.White, 85, 160);
                    }
                    _picMonster.Image = bmp;
                }
            }
            catch { _picMonster.BackColor = Color.Purple; }
        }

        private void GenerateQuestion()
        {
            var q = _quizEngine.GetNextQuestion();
            _a = q.a; _b = q.b;

            UpdateDiceVisuals();
            _lblQuestion.Text = $"{_a} × {_b}";
            _txtAnswer.Clear();
            _txtAnswer.Focus();
            LayoutCombat(); // Refresh layout to center text
        }

        private void UpdateDiceVisuals()
        {
            try
            {
                string path1 = AssetPaths.Dice($"die_{_a}.png");
                string path2 = AssetPaths.Dice($"die_{_b}.png");

                if (File.Exists(path1) && File.Exists(path2))
                {
                    _die1.Image = Image.FromFile(path1);
                    _die2.Image = Image.FromFile(path2);
                }
                else
                {
                    _die1.BackColor = Color.White;
                    _die2.BackColor = Color.White;
                }
            }
            catch { _die1.BackColor = Color.White; _die2.BackColor = Color.White; }
        }

        private void BtnSubmit_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(_txtAnswer.Text, out int ans)) return;

            var result = _session.ProcessAnswer(ans, _a, _b);

            if (result.IsCorrect)
            {
                PerformAttack(ans);
            }
            else
            {
                _lblFeedback.Text = "MISS!";
                _lblFeedback.ForeColor = Color.Red;
                GenerateQuestion();
            }

            UpdatePlayerStats();
        }

        private void PerformAttack(int damage)
        {
            _monsterHealth -= damage;
            _monsterHealthBar.Value = Math.Max(0, _monsterHealth);
            _lblFeedback.Text = $"HIT! -{damage} DMG";
            _lblFeedback.ForeColor = Color.Lime;

            if (_monsterHealth <= 0)
            {
                _lblFeedback.Text = "MONSTER SLAIN!";
                _solved = true;

                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                t.Interval = 1500;
                t.Tick += (s, e) => { t.Stop(); this.Close(); };
                t.Start();
            }
            else
            {
                GenerateQuestion();
            }
        }

        private void UpdatePlayerStats()
        {
            var p = _settings.PlayerProgress;
            _playerXpBar.Maximum = XpSystem.GetXpRequiredForNextLevel(p.Level);
            _playerXpBar.Value = p.CurrentXp;
            _lblLevel.Text = $"KNIGHT LEVEL: {p.Level}";

            int stage = KnightProgression.GetKnightStageIndex(p.Level);
            string path = AssetPaths.KnightSprite(stage);
            if (File.Exists(path))
            {
                _picKnight.Image?.Dispose();
                _picKnight.Image = Image.FromFile(path);
            }
        }

        private void QuizForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) e.Handled = true;
        }
    }
}