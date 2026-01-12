using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using MathQuizLocker.Services;
using Velopack;
using Velopack.Sources;

namespace MathQuizLocker
{
    // Double-buffered PictureBox to reduce flicker when moving/scaling PNG sprites
    internal sealed class DoubleBufferedPictureBox : PictureBox
    {
        public DoubleBufferedPictureBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            UpdateStyles();
        }
    }

    public class QuizForm : Form
    {
        private readonly AppSettings _settings;
        private readonly QuizEngine _quizEngine;
        private readonly GameSessionManager _session;

        // Combat State
        private int _a, _b, _monsterHealth, _maxMonsterHealth;
        private int _playerHealth, _maxPlayerHealth = 100;
        private bool _isInternalClose = false;
        private string _currentMonsterName = "slime";

        // Animation Logic
        private bool _isAnimating = false;
        private Point _knightOriginalPos;
        private System.Windows.Forms.Timer _animationTimer = new();

        // UI Controls
        private PictureBox _picKnight = null!;
        private PictureBox _picMonster = null!;
        private PictureBox _die1 = null!, _die2 = null!;
        private PictureBox _picMultiply = null!;
        private FantasyXpBar _monsterHealthBar = null!;
        private FantasyXpBar _playerHealthBar = null!;
        private FantasyXpBar _playerXpBar = null!;
        private Label _lblFeedback = null!, _lblLevel = null!, _lblXpStatus = null!;
        private TextBox _txtAnswer = null!;
        private Button _btnSubmit = null!, _btnReset = null!, _btnExit = null!, _btnContinue = null!;
        private Label _lblVersion = null!;

        public QuizForm(AppSettings settings)
        {
            _settings = settings ?? new AppSettings();
            _quizEngine = new QuizEngine(_settings);
            _session = new GameSessionManager(_settings, _quizEngine);
            _playerHealth = _maxPlayerHealth;

            InitializeCombatUi();
        }

        // Fix 1: enable composited painting to reduce child-control flicker (especially with Transparent picture boxes)
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style &= ~0x00080000;    // WS_SYSMENU
                cp.ExStyle |= 0x02000000;   // WS_EX_COMPOSITED
                return cp;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isInternalClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                _lblFeedback.Text = "FINISH THE FIGHT TO UNLOCK!";
                _lblFeedback.ForeColor = Color.OrangeRed;
            }
            base.OnFormClosing(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _ = UpdateMyApp();
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
            this.ShowInTaskbar = false;

            // Keep these styles as well
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                          ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint, true);
            this.UpdateStyles();

            this.Deactivate += (s, e) =>
            {
                if (!this.IsDisposed && !_isInternalClose) { this.Activate(); this.Focus(); }
            };

            this.KeyDown += (s, e) =>
            {
                if (e.Control && e.Shift && e.Alt && e.KeyCode == Keys.End)
                {
                    _isInternalClose = true;
                    this.Close();
                }

                if (e.KeyCode == Keys.Enter && _btnSubmit.Visible)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    BtnSubmit_Click(null!, null!);
                }
            };

            // Fix 2: use double-buffered PictureBoxes
            _picKnight = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _picMonster = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _die1 = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _die2 = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _picMultiply = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };

            _picKnight.Parent = this;
            _picMonster.Parent = this;

            _monsterHealthBar = new FantasyXpBar();
            _playerHealthBar = new FantasyXpBar();
            _playerXpBar = new FantasyXpBar();

            _lblLevel = new Label { Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, BackColor = Color.Black };
            _lblXpStatus = new Label { Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.Aqua, AutoSize = true, BackColor = Color.Transparent };
            _lblFeedback = new Label { Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = Color.Yellow, AutoSize = true, BackColor = Color.Transparent };

            _txtAnswer = new TextBox { Font = new Font("Segoe UI", 36), TextAlign = HorizontalAlignment.Center, BackColor = Color.FromArgb(245, 240, 220), BorderStyle = BorderStyle.None, Multiline = false };

            _btnSubmit = new Button { Text = "ATTACK", FlatStyle = FlatStyle.Flat, BackColor = Color.DarkRed, ForeColor = Color.White, Font = new Font("Segoe UI", 16, FontStyle.Bold) };
            _btnSubmit.Click += (s, e) => BtnSubmit_Click(s, e);

            _btnReset = new Button { Text = "RESET", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, Font = new Font("Segoe UI", 8) };
            _btnReset.Click += (s, e) =>
            {
                if (MessageBox.Show("Reset to Level 1?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes) { _settings.ResetProgress(); ResetBattleState(); }
            };

            _btnContinue = new Button { Text = "CONTINUE FIGHTING", FlatStyle = FlatStyle.Flat, BackColor = Color.ForestGreen, ForeColor = Color.White, Font = new Font("Segoe UI", 18, FontStyle.Bold), Visible = false };
            _btnContinue.Click += (s, e) => ResetForNextFight();

            _btnExit = new Button { Text = "EXIT TO DESKTOP", FlatStyle = FlatStyle.Flat, BackColor = Color.Goldenrod, ForeColor = Color.Black, Font = new Font("Segoe UI", 18, FontStyle.Bold), Visible = false };
            _btnExit.Click += (s, e) => { _isInternalClose = true; this.Close(); };

            _lblVersion = new Label
            {
                Text = $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Gray,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            // Fix 3: load images without locking files (avoids stutter and weird repaints)
            try
            {
                string bgPath = AssetPaths.Background("Background.png");
                var bg = LoadImageNoLock(bgPath);
                if (bg != null)
                {
                    this.BackgroundImage?.Dispose();
                    this.BackgroundImage = bg;
                    this.BackgroundImageLayout = ImageLayout.Stretch;
                }

                string xPath = AssetPaths.Dice("multiply.png");
                var mul = LoadImageNoLock(xPath);
                if (mul != null)
                {
                    _picMultiply.Image?.Dispose();
                    _picMultiply.Image = mul;
                }
            }
            catch { }

            this.Controls.AddRange(new Control[]
            {
                _picKnight, _picMonster, _die1, _picMultiply, _die2,
                _monsterHealthBar, _playerHealthBar, _playerXpBar,
                _lblLevel, _lblXpStatus, _txtAnswer, _btnSubmit, _lblFeedback,
                _btnReset, _btnExit, _btnContinue, _lblVersion
            });

            this.Resize += (s, e) => LayoutCombat();
        }

        private void LayoutCombat()
        {
            if (this.ClientSize.Width == 0 || this.ClientSize.Height == 0 || _isAnimating) return;

            int w = this.ClientSize.Width, h = this.ClientSize.Height;
            float scale = h / 1080f;

            int dSize = (int)(120 * scale);
            int xSize = (int)(80 * scale);
            _die1.Size = _die2.Size = new Size(dSize, dSize);
            _picMultiply.Size = new Size(xSize, xSize);

            int spacing = (int)(30 * scale);
            int totalDiceWidth = dSize + xSize + dSize + (spacing * 2);
            int diceStartX = (w / 2) - (totalDiceWidth / 2);

            _die1.Location = new Point(diceStartX, (int)(60 * scale));
            _picMultiply.Location = new Point(_die1.Right + spacing, _die1.Top + (dSize / 2) - (xSize / 2));
            _die2.Location = new Point(_picMultiply.Right + spacing, (int)(60 * scale));

            _lblFeedback.Location = new Point(w / 2 - _lblFeedback.Width / 2, _die1.Bottom + (int)(20 * scale));

            int kW = (int)(350 * scale); int kH = (int)(450 * scale);
            _picKnight.Size = new Size(kW, kH);
            _picKnight.Location = new Point((int)(w * 0.20), (int)(h * 0.87 - kH));

            int mW = (int)(450 * scale); int mH = (int)(550 * scale);
            _picMonster.Size = new Size(mW, mH);
            _picMonster.Location = new Point((int)(w * 0.60), (int)(h * 0.95 - mH));

            _playerHealthBar.Size = _monsterHealthBar.Size = new Size((int)(300 * scale), (int)(25 * scale));
            _playerHealthBar.Location = new Point(_picKnight.Left + (kW / 2 - _playerHealthBar.Width / 2), _picKnight.Top - (int)(40 * scale));
            _monsterHealthBar.Location = new Point(_picMonster.Left + (mW / 2 - _monsterHealthBar.Width / 2), _picMonster.Top - (int)(40 * scale));

            _playerXpBar.Size = new Size((int)(400 * scale), (int)(15 * scale));
            _playerXpBar.Location = new Point(w / 2 - _playerXpBar.Width / 2, h - (int)(60 * scale));
            _lblXpStatus.Location = new Point(_playerXpBar.Left, _playerXpBar.Top - (int)(20 * scale));
            _lblLevel.Location = new Point(20, h - (int)(40 * scale));

            _txtAnswer.Width = (int)(220 * scale);
            _txtAnswer.Location = new Point(w / 2 - _txtAnswer.Width / 2, _playerXpBar.Top - (int)(140 * scale));
            _btnSubmit.Size = new Size((int)(180 * scale), (int)(60 * scale));
            _btnSubmit.Location = new Point(w / 2 - _btnSubmit.Width / 2, _txtAnswer.Bottom + 10);

            _btnContinue.Size = _btnExit.Size = new Size((int)(350 * scale), (int)(80 * scale));
            _btnContinue.Location = new Point(w / 2 - _btnContinue.Width / 2, h / 2 - (int)(50 * scale));
            _btnExit.Location = new Point(w / 2 - _btnExit.Width / 2, _btnContinue.Bottom + 20);

            _btnReset.Size = new Size((int)(100 * scale), (int)(30 * scale));
            _btnReset.Location = new Point(w - _btnReset.Width - 20, h - _btnReset.Height - 20);
        }

        private void BtnSubmit_Click(object? sender, EventArgs e)
        {
            if (_isAnimating || !int.TryParse(_txtAnswer.Text, out int ans)) return;

            var result = _session.ProcessAnswer(ans, _a, _b);

            if (result.IsCorrect)
            {
                AnimateMeleeStrike(ans);
                _monsterHealth -= ans;
                _monsterHealthBar.Value = Math.Max(0, _monsterHealth);
                _lblFeedback.Text = $"HIT! -{ans} DMG";
                _lblFeedback.ForeColor = Color.Lime;
                if (_monsterHealth <= 0)
                {
                    XpSystem.AddXp(_settings.PlayerProgress, 25 + (_settings.MaxFactorUnlocked * 10));
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
                int monsterDmg = _a * _b;
                AnimateMonsterAttack(monsterDmg);
                _playerHealth -= monsterDmg;
                _playerHealthBar.Value = Math.Max(0, _playerHealth);
                _lblFeedback.Text = $"Ouch! {monsterDmg} DMG!";
                _lblFeedback.ForeColor = Color.Red;
                if (_playerHealth <= 0) HandleDeath(); else GenerateQuestion();
            }

            if (!_isAnimating)
            {
                UpdatePlayerStats();
                LayoutCombat();
            }
        }

        private void AnimateMeleeStrike(int damage)
        {
            _isAnimating = true;
            _animationTimer.Stop();
            _knightOriginalPos = _picKnight.Location;

            int stage = KnightProgression.GetKnightStageIndex(_settings.PlayerProgress.Level);
            string attackPath = AssetPaths.KnightAttack(stage);
            var atk = LoadImageNoLock(attackPath);
            if (atk != null)
            {
                _picKnight.Image?.Dispose();
                _picKnight.Image = atk;
            }

            int distance = _picMonster.Left - _picKnight.Right + 50;
            int step = 0;

            _animationTimer = new System.Windows.Forms.Timer { Interval = 15 };

            _animationTimer.Tick += (s, e) =>
            {
                Rectangle oldKnight = _picKnight.Bounds;
                Rectangle oldMonster = _picMonster.Bounds;

                step++;
                if (step <= 4) _picKnight.Left += (distance / 4);
                else if (step == 5)
                {
                    UpdateMonsterSprite("hit");
                    ShowDamagePopup(_picMonster, damage, Color.OrangeRed);
                    _picMonster.Left += 20;
                }
                else if (step <= 12) _picKnight.Left -= (distance / 8);
                else
                {
                    _animationTimer.Stop();
                    _isAnimating = false;
                    _picKnight.Location = _knightOriginalPos;
                    UpdateMonsterSprite("idle");
                    UpdatePlayerStats();
                    LayoutCombat();
                }

                // Fix 4: invalidate only the changed region (not the entire form)
                InvalidateMovingRegion(oldKnight, _picKnight.Bounds, oldMonster, _picMonster.Bounds);
            };

            _animationTimer.Start();
        }

        private void AnimateMonsterAttack(int damage)
        {
            _isAnimating = true;
            Point monsterStartPos = _picMonster.Location;
            UpdateMonsterSprite("attack");

            int stage = KnightProgression.GetKnightStageIndex(_settings.PlayerProgress.Level);
            string hitPath = AssetPaths.KnightHit(stage);
            var hit = LoadImageNoLock(hitPath);
            if (hit != null)
            {
                _picKnight.Image?.Dispose();
                _picKnight.Image = hit;
            }

            int lungeDistance = 120;
            int step = 0;

            var monsterTimer = new System.Windows.Forms.Timer { Interval = 20 };
            monsterTimer.Tick += (s, e) =>
            {
                Rectangle oldKnight = _picKnight.Bounds;
                Rectangle oldMonster = _picMonster.Bounds;

                step++;
                if (step <= 5) _picMonster.Left -= (lungeDistance / 5);
                else if (step == 6)
                {
                    ShowDamagePopup(_picKnight, damage, Color.Red);
                    _picKnight.Top += 10;
                }
                else if (step <= 15) _picMonster.Left += (lungeDistance / 10);
                else
                {
                    monsterTimer.Stop();
                    _isAnimating = false;
                    _picMonster.Location = monsterStartPos;
                    _picKnight.Top = (int)(this.ClientSize.Height * 0.87 - _picKnight.Height);
                    UpdateMonsterSprite("idle");
                    UpdatePlayerStats();
                    LayoutCombat();
                }

                InvalidateMovingRegion(oldKnight, _picKnight.Bounds, oldMonster, _picMonster.Bounds);
            };
            monsterTimer.Start();
        }

        private void ShowDamagePopup(Control target, int damage, Color color)
        {
            Label lbl = new Label
            {
                Text = $"-{damage}",
                Font = new Font("Segoe UI", 26, FontStyle.Bold),
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(target.Left + (target.Width / 2), target.Top)
            };
            this.Controls.Add(lbl);
            lbl.BringToFront();

            int ticks = 0;
            var t = new System.Windows.Forms.Timer { Interval = 20 };
            t.Tick += (s, e) =>
            {
                ticks++;

                Rectangle old = lbl.Bounds;
                lbl.Top -= 3;

                // Invalidate only where label moved
                InvalidateMovingRegion(old, lbl.Bounds);

                if (ticks > 25)
                {
                    t.Stop();
                    this.Controls.Remove(lbl);
                    lbl.Dispose();
                }
            };
            t.Start();
        }

        private void SpawnMonster()
        {
            int tier = Math.Max(1, _settings.MaxFactorUnlocked);
            _maxMonsterHealth = 40 + (tier * 35);
            _monsterHealth = _maxMonsterHealth;
            _monsterHealthBar.Maximum = _maxMonsterHealth;
            _monsterHealthBar.Value = _monsterHealth;
            _currentMonsterName = tier < 4 ? "slime" : tier < 7 ? "orc" : "dragon";
            UpdateMonsterSprite("idle");
        }

        private void UpdateMonsterSprite(string state)
        {
            try
            {
                string suffix = state == "idle" ? "" : $"_{state}";
                string path = Path.Combine(AssetPaths.AssetsRoot, "Monsters", $"{_currentMonsterName}{suffix}.png");

                var img = LoadImageNoLock(path);
                if (img != null)
                {
                    _picMonster.Image?.Dispose();
                    _picMonster.Image = img;
                }
            }
            catch { }
        }

        private void HandleDeath()
        {
            _lblFeedback.Text = "YOU HAVE FALLEN...";
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer { Interval = 2000 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                _playerHealth = _maxPlayerHealth;
                SpawnMonster();
                GenerateQuestion();
            };
            t.Start();
        }

        private void GenerateQuestion()
        {
            try
            {
                var q = _quizEngine.GetNextQuestion(); _a = q.a; _b = q.b;
                UpdateDiceVisuals(); _txtAnswer.Clear(); _txtAnswer.Focus();
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
                _die1.Image?.Dispose();
                _die2.Image?.Dispose();

                string d1 = AssetPaths.Dice($"die_{_a}.png");
                string d2 = AssetPaths.Dice($"die_{_b}.png");

                _die1.Image = LoadImageNoLock(d1);
                _die2.Image = LoadImageNoLock(d2);
            }
            catch { }
        }

        private void UpdatePlayerStats()
        {
            if (_isAnimating) return;

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
                string path = AssetPaths.KnightSprite(KnightProgression.GetKnightStageIndex(p.Level));
                var img = LoadImageNoLock(path);
                if (img != null)
                {
                    _picKnight.Image?.Dispose();
                    _picKnight.Image = img;
                }
            }
            catch { }
        }

        private void ShowVictoryScreen()
        {
            _monsterHealthBar.Value = 0;
            _lblFeedback.Text = "MONSTER DEFEATED!";
            _lblFeedback.ForeColor = Color.Gold;
            _txtAnswer.Visible = _btnSubmit.Visible = _die1.Visible = _die2.Visible = _picMultiply.Visible = false;
            _btnContinue.Visible = _btnExit.Visible = true;
            _btnContinue.Focus();
        }

        private void ResetForNextFight()
        {
            _btnContinue.Visible = _btnExit.Visible = false;
            _txtAnswer.Visible = _btnSubmit.Visible = _die1.Visible = _die2.Visible = _picMultiply.Visible = true;
            SpawnMonster();
            GenerateQuestion();
            _lblFeedback.Text = "A NEW FOE APPEARS!";
            _lblFeedback.ForeColor = Color.Yellow;
            _txtAnswer.Focus();
        }

        private void ResetBattleState()
        {
            _playerHealth = _maxPlayerHealth;
            _quizEngine.InitializeForCurrentLevel();
            _btnContinue.Visible = _btnExit.Visible = false;
            _txtAnswer.Visible = _btnSubmit.Visible = _die1.Visible = _die2.Visible = _picMultiply.Visible = true;
            UpdatePlayerStats();
            SpawnMonster();
            GenerateQuestion();
        }

        private async Task UpdateMyApp()
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource("https://github.com/DoneFeedingTheFox/MathQuizLocker", null, false));
                var newVersion = await mgr.CheckForUpdatesAsync();
                if (newVersion != null) { await mgr.DownloadUpdatesAsync(newVersion); mgr.ApplyUpdatesAndRestart(newVersion); }
            }
            catch (Exception ex) { Console.WriteLine("Update check failed: " + ex.Message); }
        }

        // ---------- Helper fixes ----------

        // Load image without locking file (prevents stutter and avoids GDI+ file locking)
        private static Image? LoadImageNoLock(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            // Read into memory and clone
            byte[] bytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(bytes);
            using var tmp = Image.FromStream(ms);
            return new Bitmap(tmp);
        }

        // Invalidate only what moved (significantly reduces redraw pressure and flicker)
        private void InvalidateMovingRegion(Rectangle oldA, Rectangle newA)
        {
            var r = Rectangle.Union(oldA, newA);
            r.Inflate(30, 30);
            this.Invalidate(r);
        }

        private void InvalidateMovingRegion(Rectangle oldA, Rectangle newA, Rectangle oldB, Rectangle newB)
        {
            var r = Rectangle.Union(Rectangle.Union(oldA, newA), Rectangle.Union(oldB, newB));
            r.Inflate(40, 40);
            this.Invalidate(r);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Clean up images to avoid GDI handle leaks
                try
                {
                    this.BackgroundImage?.Dispose();
                    _picKnight?.Image?.Dispose();
                    _picMonster?.Image?.Dispose();
                    _die1?.Image?.Dispose();
                    _die2?.Image?.Dispose();
                    _picMultiply?.Image?.Dispose();
                }
                catch { }
            }

            base.Dispose(disposing);
        }
    }
}
