using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
    public class QuizForm : Form
    {
        private readonly AppSettings _settings;
        private readonly QuizEngine _quizEngine;
        private readonly GameSessionManager _session;

        // Combat State
        private int _a, _b, _monsterHealth, _maxMonsterHealth;
        private int _playerHealth, _maxPlayerHealth = 100;

        // UI Controls
        private PictureBox _picKnight = null!;
        private PictureBox _picMonster = null!;
        private PictureBox _die1 = null!, _die2 = null!;
        private FantasyXpBar _monsterHealthBar = null!;
        private FantasyXpBar _playerHealthBar = null!;
        private FantasyXpBar _playerXpBar = null!;
        private Label _lblFeedback = null!, _lblLevel = null!, _lblXpStatus = null!;
        private TextBox _txtAnswer = null!;
        private Button _btnSubmit = null!, _btnReset = null!, _btnExit = null!;

        public QuizForm(AppSettings settings)
        {
            _settings = settings ?? new AppSettings();
            _quizEngine = new QuizEngine(_settings);
            _session = new GameSessionManager(_settings, _quizEngine);
            _playerHealth = _maxPlayerHealth;

            InitializeCombatUi();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            SpawnMonster();
            GenerateQuestion();
            UpdatePlayerStats();
            LayoutCombat();
        }

        private void InitializeCombatUi()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.BackColor = Color.FromArgb(30, 30, 30);

            this.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter && _btnSubmit.Visible)
                {
                    e.Handled = true;
                    BtnSubmit_Click(null!, null!);
                }
            };

            // 1. Sprites
            _picKnight = new PictureBox { Size = new Size(350, 450), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _picMonster = new PictureBox { Size = new Size(350, 450), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };

            // 2. Dice
            _die1 = new PictureBox { Size = new Size(120, 120), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _die2 = new PictureBox { Size = new Size(120, 120), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };

            // 3. Status Bars
            _monsterHealthBar = new FantasyXpBar { Width = 300, Height = 25 };
            _playerHealthBar = new FantasyXpBar { Width = 300, Height = 25 };
            _playerXpBar = new FantasyXpBar { Width = 400, Height = 15 };

            // 4. Labels
            _lblLevel = new Label { Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, BackColor = Color.Black };
            _lblXpStatus = new Label { Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.Aqua, AutoSize = true, BackColor = Color.Transparent };
            _lblFeedback = new Label { Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = Color.Yellow, AutoSize = true, BackColor = Color.Transparent };

            // 5. Inputs
            _txtAnswer = new TextBox { Font = new Font("Segoe UI", 36), Width = 220, TextAlign = HorizontalAlignment.Center, BackColor = Color.FromArgb(245, 240, 220), BorderStyle = BorderStyle.None, Multiline = false };

            _btnSubmit = new Button { Text = "ATTACK", Size = new Size(180, 60), FlatStyle = FlatStyle.Flat, BackColor = Color.DarkRed, ForeColor = Color.White, Font = new Font("Segoe UI", 16, FontStyle.Bold) };
            _btnSubmit.Click += BtnSubmit_Click;

            _btnReset = new Button { Text = "RESET", Size = new Size(100, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, Font = new Font("Segoe UI", 8) };
            _btnReset.Click += (s, e) => {
                if (MessageBox.Show("Reset to Level 1?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _settings.ResetProgress();
                    ResetBattleState();
                }
            };

            // 6. Victory Button
            _btnExit = new Button { Text = "VICTORY! COLLECT LOOT", Size = new Size(350, 80), FlatStyle = FlatStyle.Flat, BackColor = Color.Goldenrod, ForeColor = Color.Black, Font = new Font("Segoe UI", 18, FontStyle.Bold), Visible = false };
            _btnExit.Click += (s, e) => this.Close();

            try
            {
                string bgPath = AssetPaths.Background("Background.png");
                if (File.Exists(bgPath))
                {
                    this.BackgroundImage = Image.FromFile(bgPath);
                    this.BackgroundImageLayout = ImageLayout.Stretch;
                }
            }
            catch { }

            this.Controls.AddRange(new Control[] { _picKnight, _picMonster, _die1, _die2, _monsterHealthBar, _playerHealthBar, _playerXpBar, _lblLevel, _lblXpStatus, _txtAnswer, _btnSubmit, _lblFeedback, _btnReset, _btnExit });
            this.Resize += (s, e) => LayoutCombat();
        }

        private void LayoutCombat()
        {
            if (this.ClientSize.Width == 0) return;
            int w = this.ClientSize.Width, h = this.ClientSize.Height;

            _die1.Location = new Point(w / 2 - 140, 60);
            _die2.Location = new Point(w / 2 + 20, 60);
            _lblFeedback.Location = new Point(w / 2 - _lblFeedback.Width / 2, _die1.Bottom + 40);

            _picKnight.Location = new Point((int)(w * 0.10), (int)(h * 0.40));
            _picMonster.Location = new Point((int)(w * 0.60), (int)(h * 0.40));

            _playerHealthBar.Location = new Point(_picKnight.Left + 25, _picKnight.Top - 40);
            _monsterHealthBar.Location = new Point(_picMonster.Left + 25, _picMonster.Top - 40);

            _playerXpBar.Location = new Point(w / 2 - 200, h - 50);
            _lblXpStatus.Location = new Point(_playerXpBar.Left, _playerXpBar.Top - 20);
            _lblLevel.Location = new Point(20, h - 40);

            _txtAnswer.Location = new Point(w / 2 - _txtAnswer.Width / 2, h - 220);
            _btnSubmit.Location = new Point(w / 2 - _btnSubmit.Width / 2, _txtAnswer.Bottom + 10);

            _btnExit.Location = new Point(w / 2 - _btnExit.Width / 2, h / 2 - 40);
            _btnReset.Location = new Point(w - 120, h - 40);
        }

        private void BtnSubmit_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(_txtAnswer.Text, out int ans)) return;
            var result = _session.ProcessAnswer(ans, _a, _b);

            if (result.IsCorrect)
            {
                _monsterHealth -= ans;
                _monsterHealthBar.Value = Math.Max(0, _monsterHealth);
                _lblFeedback.Text = $"HIT! -{ans} DMG";
                _lblFeedback.ForeColor = Color.Lime;

                if (_monsterHealth <= 0)
                {
                    int victoryXp = 25 + (_settings.MaxFactorUnlocked * 10);
                    XpSystem.AddXp(_settings.PlayerProgress, victoryXp);
                    ShowVictoryScreen();
                }
                else
                {
                    XpSystem.AddXp(_settings.PlayerProgress, XpSystem.XpPerCorrectAnswer);
                    GenerateQuestion();
                }
            }
            else
            {
                int monsterDmg = Math.Max(5, _settings.MaxFactorUnlocked * 3);
                _playerHealth -= monsterDmg;
                _playerHealthBar.Value = Math.Max(0, _playerHealth);
                _lblFeedback.Text = $"MISS! Taken {monsterDmg} DMG";
                _lblFeedback.ForeColor = Color.Red;

                if (_playerHealth <= 0) HandleDeath();
                else GenerateQuestion();
            }
            UpdatePlayerStats();
            LayoutCombat();
        }

        private void ShowVictoryScreen()
        {
            _monsterHealthBar.Value = 0;
            _lblFeedback.Text = "MONSTER DEFEATED!";
            _lblFeedback.ForeColor = Color.Gold;

            _txtAnswer.Visible = false;
            _btnSubmit.Visible = false;
            _die1.Visible = false;
            _die2.Visible = false;
            _btnExit.Visible = true;
            _btnExit.Focus();
        }

        private void ResetBattleState()
        {
            _playerHealth = _maxPlayerHealth;
            _playerHealthBar.Value = _playerHealth;
            _quizEngine.InitializeForCurrentLevel();

            _txtAnswer.Visible = true;
            _btnSubmit.Visible = true;
            _die1.Visible = true;
            _die2.Visible = true;
            _btnExit.Visible = false;

            UpdatePlayerStats();
            SpawnMonster();
            GenerateQuestion();
        }

        private void SpawnMonster()
        {
            int tier = Math.Max(1, _settings.MaxFactorUnlocked);
            _maxMonsterHealth = 40 + (tier * 35);
            _monsterHealth = _maxMonsterHealth;
            _monsterHealthBar.Maximum = _maxMonsterHealth;
            _monsterHealthBar.Value = _monsterHealth;

            string mFile = tier < 4 ? "slime.png" : tier < 7 ? "orc.png" : "dragon.png";
            try
            {
                string path = Path.Combine(AssetPaths.AssetsRoot, "Monsters", mFile);
                if (File.Exists(path))
                {
                    _picMonster.Image?.Dispose();
                    _picMonster.Image = Image.FromFile(path);
                }
                else
                {
                    Bitmap bmp = new Bitmap(350, 450);
                    using (Graphics g = Graphics.FromImage(bmp)) g.Clear(tier < 7 ? Color.Purple : Color.DarkRed);
                    _picMonster.Image = bmp;
                }
            }
            catch { }
        }

        private void HandleDeath()
        {
            _lblFeedback.Text = "YOU HAVE FALLEN...";
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer { Interval = 2000 };
            t.Tick += (s, e) => {
                t.Stop();
                _playerHealth = _maxPlayerHealth;
                _playerHealthBar.Value = _playerHealth;
                SpawnMonster();
                GenerateQuestion();
            };
            t.Start();
        }

        private void GenerateQuestion()
        {
            try
            {
                var q = _quizEngine.GetNextQuestion();
                _a = q.a; _b = q.b;
                UpdateDiceVisuals();
                _txtAnswer.Clear();
                _txtAnswer.Focus();
            }
            catch
            {
                _quizEngine.InitializeForCurrentLevel();
                GenerateQuestion();
            }
        }

        private void UpdateDiceVisuals()
        {
            try
            {
                _die1.Image?.Dispose(); _die2.Image?.Dispose();
                string d1 = AssetPaths.Dice($"die_{_a}.png"), d2 = AssetPaths.Dice($"die_{_b}.png");
                if (File.Exists(d1)) _die1.Image = Image.FromFile(d1);
                if (File.Exists(d2)) _die2.Image = Image.FromFile(d2);
            }
            catch { }
        }

        private void UpdatePlayerStats()
        {
            var p = _settings.PlayerProgress;
            int nextLevelXp = XpSystem.GetXpRequiredForNextLevel(p.Level);

            _lblLevel.Text = $"KNIGHT LEVEL: {p.Level}";
            _lblXpStatus.Text = $"XP: {p.CurrentXp} / {nextLevelXp}";

            _playerHealthBar.Maximum = _maxPlayerHealth;
            _playerHealthBar.Value = _playerHealth;

            _playerXpBar.Maximum = nextLevelXp;
            _playerXpBar.Value = Math.Min(p.CurrentXp, nextLevelXp);

            try
            {
                // This now gets the correct index based on your new level-1 logic
                int stageIdx = KnightProgression.GetKnightStageIndex(p.Level);
                string path = AssetPaths.KnightSprite(stageIdx);

                if (File.Exists(path))
                {
                    _picKnight.Image?.Dispose();
                    _picKnight.Image = Image.FromFile(path);
                }
            }
            catch { }
        }
    }
}