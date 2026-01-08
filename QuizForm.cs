using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
    #region UI Components

    public class RoundedPanel : Panel
    {
        private int _cornerRadius = 20;

        public RoundedPanel()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(32, 32, 32);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

            using var path = UiPathHelpers.CreateRoundedRectPath(rect, _cornerRadius);
            using (var shadowPath = UiPathHelpers.CreateRoundedRectPath(new Rectangle(rect.X + 4, rect.Y + 4, rect.Width, rect.Height), _cornerRadius))
            using (var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            {
                g.FillPath(shadowBrush, shadowPath);
            }

            using (var fillBrush = new SolidBrush(this.BackColor)) g.FillPath(fillBrush, path);
            using (var borderPen = new Pen(Color.FromArgb(70, 70, 70), 1.5f)) g.DrawPath(borderPen, path);
        }
    }

    internal static class UiPathHelpers
    {
        public static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0) { path.AddRectangle(rect); path.CloseFigure(); return path; }

            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            var arc = new Rectangle(rect.Location, new Size(d, d));
            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - d; path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - d; path.AddArc(arc, 0, 90);
            arc.X = rect.Left; path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    #endregion

    public class QuizForm : Form
    {
        private readonly AppSettings _settings;
        private readonly QuizEngine _quizEngine;

        // State
        private int _a, _b, _correctCount = 0, _correctStreak = 0;
        private (int a, int b) _currentQuestion;
        private bool _solved = false, _secondChanceUsedThisQuestion = false;
        private bool _powerStrikeQueued = false, _xpBoostActive = false;
        private bool _hintUsedThisSession = false, _mathSightUsedThisSession = false, _skipUsedThisSession = false;
        private bool _powerStrikeUsedThisSession = false, _xpBoostUsedThisSession = false, _valorUnlockedThisSession = false, _valorUsedThisSession = false;

        // Controls
        private Panel _card = null!, _txtWrapper = null!;
        private Label _lblQuestion = null!, _lblHint = null!, _lblLevel = null!;
        private TextBox _txtAnswer = null!;
        private Button _btnSubmit = null!, _btnHint = null!, _btnUseToken = null!, _btnMathSight = null!, _btnSkip = null!, _btnPowerStrike = null!, _btnXpBoost = null!, _btnValor = null!;
        private FantasyXpBar _xpBar = null!;
        private PictureBox _picKnight = null!;

        public QuizForm(AppSettings settings)
        {
            _settings = settings ?? new AppSettings();
            _quizEngine = new QuizEngine(_settings);

            InitializeUi();
            
            UpdateXpBar();
            UpdateKnightImage();
            UpdateAbilityButtons();
            GenerateQuestion();
        }

        #region Initialization

        private void InitializeUi()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.KeyDown += QuizForm_KeyDown;

            LoadBackgroundImage();

            _card = new Panel { BackColor = Color.Transparent };
            LoadCardLayout();

            _lblHint = CreateLabel("Solve the equation to continue.", 20, Color.FromArgb(200, 200, 200), true);
            _lblLevel = CreateLabel($"Level: {_settings.PlayerProgress.Level}", 14, Color.FromArgb(180, 180, 180), true);
            _lblQuestion = CreateLabel("Question", 28, Color.White, false, ContentAlignment.MiddleCenter);
            _picKnight = new PictureBox { Size = new Size(360, 360), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _xpBar = new FantasyXpBar { Width = 260, Height = 22 };

            _txtWrapper = new Panel { BackColor = Color.FromArgb(220, 200, 170), Width = 360, Height = 70, Padding = new Padding(4) };
            _txtWrapper.Paint += (s, e) => {
                using var path = UiPathHelpers.CreateRoundedRectPath(new Rectangle(0, 0, _txtWrapper.Width - 1, _txtWrapper.Height - 1), 20);
                using var pen = new Pen(Color.FromArgb(100, 80, 50), 2);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            };

            _txtAnswer = new TextBox { Font = new Font("Segoe UI", 28), Width = 350, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(245, 240, 220), TextAlign = HorizontalAlignment.Center };
            _txtWrapper.Controls.Add(_txtAnswer);
            CenterControlInParent(_txtAnswer, _txtWrapper);

            _btnSubmit = CreateButton("OK", Color.FromArgb(210, 170, 70), BtnSubmit_Click, 220, 70);
            _btnSubmit.Paint += BtnSubmit_Paint;

            _btnHint = CreateButton("Hint", Color.FromArgb(60, 60, 60), BtnHint_Click, 90, 32, 11);
            _btnUseToken = CreateButton("Token", Color.FromArgb(120, 60, 60), BtnUseToken_Click, 110, 32, 11);
            _btnMathSight = CreateButton("MathSight", Color.FromArgb(50, 80, 120), BtnMathSight_Click, 110, 32, 11);
            _btnSkip = CreateButton("Skip", Color.FromArgb(80, 80, 80), BtnSkip_Click, 90, 32, 11);
            _btnPowerStrike = CreateButton("PowerStrike", Color.FromArgb(100, 80, 40), BtnPowerStrike_Click, 110, 32, 11);
            _btnXpBoost = CreateButton("XP Boost", Color.FromArgb(40, 100, 40), BtnXpBoost_Click, 110, 32, 11);
            _btnValor = CreateButton("Valor", Color.FromArgb(120, 100, 0), BtnValor_Click, 110, 32, 11);

            _card.Controls.AddRange(new Control[] { _lblHint, _lblLevel, _xpBar, _lblQuestion, _btnSubmit, _picKnight, _txtWrapper, _btnHint, _btnUseToken, _btnMathSight, _btnSkip, _btnPowerStrike, _btnXpBoost, _btnValor });
            this.Controls.Add(_card);
            this.Resize += (s, e) => LayoutCard();
        }

        private Label CreateLabel(string text, float size, Color color, bool autoSize, ContentAlignment align = ContentAlignment.TopLeft)
        {
            return new Label { Text = text, Font = new Font("Segoe UI", size, (size > 20 ? FontStyle.Bold : FontStyle.Regular)), ForeColor = color, AutoSize = autoSize, TextAlign = align, BackColor = Color.Transparent };
        }

        private Button CreateButton(string text, Color backColor, EventHandler onClick, int w, int h, float fontSize = 22)
        {
            var btn = new Button { Text = text, Font = new Font("Segoe UI", fontSize, FontStyle.Bold), Width = w, Height = h, FlatStyle = FlatStyle.Flat, BackColor = backColor, ForeColor = Color.White, Cursor = Cursors.Hand, TabStop = false };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            if (h > 40) MakeButtonRounded(btn, 20);
            return btn;
        }

        #endregion

        #region Helpers & Assets

        private static string GetAssetPath(params string[] relativeParts)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string fullPath = Path.Combine(baseDir, "Assets");
            foreach (var part in relativeParts) fullPath = Path.Combine(fullPath, part);
            return fullPath;
        }

        private void LoadBackgroundImage()
        {
            try {
                string bgPath = GetAssetPath("Backgrounds", "Background.png");
                if (File.Exists(bgPath)) { this.BackgroundImage = Image.FromFile(bgPath); this.BackgroundImageLayout = ImageLayout.Stretch; }
            } catch { }
        }

        private void LoadCardLayout()
        {
            try {
                string panelPath = GetAssetPath("UI", "panel.png");
                if (File.Exists(panelPath)) {
                    var img = Image.FromFile(panelPath);
                    _card.BackgroundImage = img;
                    _card.BackgroundImageLayout = ImageLayout.Stretch;
                    int targetWidth = (int)(Screen.PrimaryScreen.Bounds.Width * 0.75);
                    _card.Size = new Size(targetWidth, (int)(img.Height * (targetWidth / (float)img.Width)));
                } else _card.Size = new Size(1300, 480);
            } catch { _card.Size = new Size(1300, 480); }
        }

        private void UpdateKnightImage()
        {
            try {
                int stageIndex = KnightProgression.GetKnightStageIndex(_settings.PlayerProgress.Level);
                string fullPath = GetAssetPath("KnightSprites", $"knight_stage_{stageIndex}.png");
                if (File.Exists(fullPath)) {
                    var old = _picKnight.Image;
                    _picKnight.Image = Image.FromFile(fullPath);
                    old?.Dispose();
                }
            } catch { _picKnight.Image = null; }
        }

        #endregion

        #region Logic & Events

        private void GenerateQuestion()
        {
            _secondChanceUsedThisQuestion = false;
            _currentQuestion = _quizEngine.GetNextQuestion();
            _a = _currentQuestion.a; _b = _currentQuestion.b;

            int required = _settings.RequiredCorrectAnswers > 0 ? _settings.RequiredCorrectAnswers : 10;
            _lblQuestion.Text = $"({_correctCount}/{required})  What is {_a} × {_b}?";
            _txtAnswer.Text = "";
            _txtAnswer.Focus();
            LayoutCard();
        }

        private void BtnSubmit_Click(object? sender, EventArgs e)
        {
            if (!int.TryParse(_txtAnswer.Text, out int answer)) {
                _lblHint.Text = "Please enter a number."; _lblHint.ForeColor = Color.Goldenrod;
                return;
            }

            if (_quizEngine.SubmitAnswer(answer)) {
                _correctCount++;
                bool leveledUp = AwardXp();
                _lblHint.Text = leveledUp ? $"LEVEL UP! Level {_settings.PlayerProgress.Level}! 🎉" : $"Correct! {_a} × {_b} = {_a * _b}";
                _lblHint.ForeColor = leveledUp ? Color.Gold : Color.LimeGreen;
                RegisterCorrectForStreak();

                if (_correctCount >= (_settings.RequiredCorrectAnswers > 0 ? _settings.RequiredCorrectAnswers : 10)) {
                    _solved = true; this.Close(); return;
                }
                GenerateQuestion();
            } else {
                HandleWrongAnswer(answer);
            }
            LayoutCard();
        }

        private void HandleWrongAnswer(int answer)
        {
            if (RewardSystem.HasAbility(_settings.PlayerProgress, AbilityType.SecondChance) && !_secondChanceUsedThisQuestion) {
                _secondChanceUsedThisQuestion = true;
                _lblHint.Text = $"Wrong: {answer}. Second Chance active!";
                _lblHint.ForeColor = Color.Goldenrod;
            } else {
                _lblHint.Text = $"Wrong: {answer}. {_a} × {_b} = {_a * _b}";
                _lblHint.ForeColor = Color.FromArgb(220, 50, 50);
                _correctStreak = 0;
                UpdateAbilityButtons();
                GenerateQuestion();
            }
        }

        private bool AwardXp()
        {
            var p = _settings.PlayerProgress;
            int gain = XpSystem.XpPerCorrectAnswer;
            if (_xpBoostActive) gain *= 2;
            if (_powerStrikeQueued) { gain *= 2; _powerStrikeQueued = false; }
            
            bool leveledUp = XpSystem.AddXp(p, gain);
            AppSettings.Save(_settings);
            _lblLevel.Text = $"Level: {p.Level}";
            UpdateXpBar(); UpdateKnightImage(); UpdateAbilityButtons();
            return leveledUp;
        }

        private void RegisterCorrectForStreak()
        {
            _correctStreak++;
            if (_correctStreak >= 5) { _settings.PlayerProgress.CheatTokens++; _correctStreak -= 5; }
            if (!_valorUnlockedThisSession && _correctStreak >= 8) _valorUnlockedThisSession = true;
            AppSettings.Save(_settings);
            UpdateAbilityButtons();
        }

        private void UpdateXpBar()
        {
            var p = _settings.PlayerProgress;
            int needed = XpSystem.GetXpRequiredForNextLevel(p.Level);
            _xpBar.Maximum = needed;
            _xpBar.Value = Math.Min(p.CurrentXp, needed);
        }

        private void UpdateAbilityButtons()
        {
            var p = _settings.PlayerProgress;
            _btnHint.Visible = RewardSystem.HasAbility(p, AbilityType.HintPower);
            _btnHint.Enabled = _btnHint.Visible && !_hintUsedThisSession;
            _btnUseToken.Text = $"Token ({p.CheatTokens})";
            _btnUseToken.Enabled = p.CheatTokens > 0;
            _btnMathSight.Visible = RewardSystem.HasAbility(p, AbilityType.MathSight);
            _btnMathSight.Enabled = _btnMathSight.Visible && !_mathSightUsedThisSession;
            _btnSkip.Visible = RewardSystem.HasAbility(p, AbilityType.SkipStone);
            _btnSkip.Enabled = _btnSkip.Visible && !_skipUsedThisSession;
            _btnPowerStrike.Visible = RewardSystem.HasAbility(p, AbilityType.PowerStrike);
            _btnPowerStrike.Enabled = _btnPowerStrike.Visible && !_powerStrikeUsedThisSession && !_powerStrikeQueued;
            _btnXpBoost.Visible = RewardSystem.HasAbility(p, AbilityType.XpBoost);
            _btnXpBoost.Text = _xpBoostActive ? "XP Boost (ON)" : "XP Boost";
            _btnXpBoost.Enabled = _btnXpBoost.Visible && !_xpBoostUsedThisSession && !_xpBoostActive;
            _btnValor.Visible = _valorUnlockedThisSession;
            _btnValor.Enabled = _valorUnlockedThisSession && !_valorUsedThisSession;
        }

        // Shared Hint Logic
        private void ShowHint()
        {
            int bigger = Math.Max(_a, _b), smaller = Math.Min(_a, _b);
            string msg = smaller == 1 ? "MathSight: Any number × 1 is itself." : $"MathSight: Think {string.Join(" + ", Enumerable.Repeat(bigger.ToString(), smaller))}";
            _lblHint.Text = msg; _lblHint.ForeColor = Color.FromArgb(150, 200, 255);
            LayoutCard();
        }

        private void BtnHint_Click(object? s, EventArgs e) { if (!_hintUsedThisSession) { _hintUsedThisSession = true; ShowHint(); UpdateAbilityButtons(); } }
        private void BtnMathSight_Click(object? s, EventArgs e) { if (!_mathSightUsedThisSession) { _mathSightUsedThisSession = true; ShowHint(); UpdateAbilityButtons(); } }
        private void BtnUseToken_Click(object? s, EventArgs e) { if (_settings.PlayerProgress.CheatTokens > 0) { _settings.PlayerProgress.CheatTokens--; BtnSubmit_Click(s, e); } }
        private void BtnSkip_Click(object? s, EventArgs e) { _skipUsedThisSession = true; _correctStreak = 0; GenerateQuestion(); }
        private void BtnPowerStrike_Click(object? s, EventArgs e) { _powerStrikeQueued = _powerStrikeUsedThisSession = true; UpdateAbilityButtons(); }
        private void BtnXpBoost_Click(object? s, EventArgs e) { _xpBoostActive = _xpBoostUsedThisSession = true; UpdateAbilityButtons(); }
        private void BtnValor_Click(object? s, EventArgs e) { _valorUsedThisSession = _solved = true; this.Close(); }

        #endregion

        #region Layout & Paint

        private void LayoutCard()
        {
            if (_card == null) return;
            int pad = 60, rColW = 360, rX = _card.Width - pad - rColW / 2;
            int lX = pad + (_card.Width - rColW - pad) / 2;

            _lblHint.Location = new Point(lX - _lblHint.Width / 2, pad + 50);
            _lblQuestion.Bounds = new Rectangle(pad, _lblHint.Bottom + 30, _card.Width - rColW - pad * 2, 60);
            _txtWrapper.Location = new Point(lX - _txtWrapper.Width / 2, _lblQuestion.Bottom + 30);
            _btnSubmit.Location = new Point(lX - _btnSubmit.Width / 2, _txtWrapper.Bottom + 25);

            _picKnight.Location = new Point(rX - _picKnight.Width / 2, pad + 10);
            int curY = _card.Height - pad - 70;

            if (_btnValor.Visible) { _btnValor.Location = new Point(rX - _btnValor.Width / 2, curY - _btnValor.Height); curY = _btnValor.Top - 12; }
            curY = LayoutAbilityRowWrapUp(new[] { _btnSkip, _btnPowerStrike, _btnXpBoost }, rX, rColW - 40, curY, 12);
            curY = LayoutAbilityRowWrapUp(new[] { _btnHint, _btnUseToken, _btnMathSight }, rX, rColW - 40, curY, 12);

            _xpBar.Location = new Point(rX - _xpBar.Width / 2, curY - _xpBar.Height);
            _lblLevel.Location = new Point(rX - _lblLevel.Width / 2, _xpBar.Top - 25);
            _card.Location = new Point((this.ClientSize.Width - _card.Width) / 2, (this.ClientSize.Height - _card.Height) / 2);
            _picKnight.SendToBack();
        }

        private int LayoutAbilityRowWrapUp(Button[] btns, int cX, int maxW, int bY, int gapY)
        {
            var visible = btns.Where(b => b.Visible).ToList();
            if (visible.Count == 0) return bY;
            int totalW = visible.Sum(b => b.Width) + (visible.Count - 1) * 10;
            int x = cX - totalW / 2, topY = bY - visible[0].Height;
            foreach (var b in visible) { b.Location = new Point(x, topY); x += b.Width + 10; }
            return topY - gapY;
        }

        private void BtnSubmit_Paint(object? sender, PaintEventArgs e)
        {
            var btn = (Button)sender!; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
            using (var b = new LinearGradientBrush(r, Color.FromArgb(230, 200, 90), Color.FromArgb(185, 150, 60), 90f)) g.FillRectangle(b, r);
            using (var s = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillRectangle(s, new Rectangle(0, 0, btn.Width, btn.Height / 4));
            using (var p = new Pen(Color.FromArgb(120, 90, 40), 2)) g.DrawRectangle(p, r);
            TextRenderer.DrawText(g, btn.Text, btn.Font, r, Color.FromArgb(40, 30, 10), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void MakeButtonRounded(Button btn, int radius)
        {
            btn.Resize += (s, e) => {
                btn.Region?.Dispose();
                using var path = UiPathHelpers.CreateRoundedRectPath(new Rectangle(0, 0, btn.Width, btn.Height), radius);
                btn.Region = new Region(path);
            };
        }

        private void CenterControlInParent(Control c, Control p) => c.Location = new Point((p.Width - c.Width) / 2, (p.Height - c.Height) / 2);

        #endregion

        #region System Overrides

        private void QuizForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_settings.EnableDeveloperHotkey && e.Control && e.Shift && e.Alt && e.KeyCode == Keys.M) Environment.Exit(0);
            if (e.KeyCode == Keys.Enter) { e.Handled = true; BtnSubmit_Click(this, EventArgs.Empty); }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter) { BtnSubmit_Click(this, EventArgs.Empty); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_solved && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
            base.OnFormClosing(e);
        }

        #endregion
    }
}