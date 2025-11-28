using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MathQuizLocker.Services;


namespace MathQuizLocker
{
    // Simple rounded panel with soft shadow
    public class RoundedPanel : Panel
    {
        private int _cornerRadius = 20;  // fixed radius; no public property

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

            using (GraphicsPath path = GetRoundedRect(rect, _cornerRadius))
            {
                // Shadow
                using (GraphicsPath shadowPath = GetRoundedRect(
                           new Rectangle(rect.X + 4, rect.Y + 4, rect.Width, rect.Height),
                           _cornerRadius))
                {
                    using (Brush shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                    {
                        g.FillPath(shadowBrush, shadowPath);
                    }
                }

                using (Brush fillBrush = new SolidBrush(this.BackColor))
                {
                    g.FillPath(fillBrush, path);
                }

                using (Pen borderPen = new Pen(Color.FromArgb(70, 70, 70), 1.5f))
                {
                    g.DrawPath(borderPen, path);
                }
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class QuizForm : Form
    {
        private readonly AppSettings _settings;
        private readonly QuizEngine _quizEngine;

        // Current question (for display + per-session counter)
        private int _a, _b;
        private (int a, int b) _currentQuestion;
        private int _correctCount = 0;
        private bool _solved = false;

        // Ability state
        private bool _secondChanceUsedThisQuestion = false;
        private bool _powerStrikeQueued = false;
        private bool _xpBoostActive = false;
        private bool _valorUsedThisSession = false;

        // Streak system + per-session ability use
        private const int StreakForToken = 5;
        private const int StreakForValor = 8;

        private int _correctStreak = 0;

        private bool _hintUsedThisSession = false;
        private bool _mathSightUsedThisSession = false;
        private bool _skipUsedThisSession = false;
        private bool _powerStrikeUsedThisSession = false;
        private bool _xpBoostUsedThisSession = false;
        private bool _valorUnlockedThisSession = false;

        private RoundedPanel _card = null!;
        private Label _lblQuestion = null!;
        private TextBox _txtAnswer = null!;
        private Button _btnSubmit = null!;
        private Label _lblHint = null!;   // top text: instruction + success/fail messages
        private Label _lblLevel = null!;
        private ProgressBar _xpBar = null!;
        private PictureBox _picKnight = null!;

        // Ability buttons
        private Button _btnHint = null!;
        private Button _btnUseToken = null!;
        private Button _btnMathSight = null!;
        private Button _btnSkip = null!;
        private Button _btnPowerStrike = null!;
        private Button _btnXpBoost = null!;
        private Button _btnValor = null!;

        public QuizForm(AppSettings settings)
        {
            _settings = settings ?? new AppSettings();
            _quizEngine = new QuizEngine(_settings);

            InitializeUi();
            GenerateQuestion();
        }

        private void InitializeUi()
        {
            // Fullscreen lock look
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.DoubleBuffered = true;

            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.DoubleBuffered = true;

            // NEW: try to load a background image next to the exe
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string bgPath = Path.Combine(baseDir, "Background.png");

                if (File.Exists(bgPath))
                {
                    this.BackgroundImage = Image.FromFile(bgPath);
                    this.BackgroundImageLayout = ImageLayout.Stretch;
                }
            }
            catch
            {
                // Ignore errors – we don't want the lock screen to crash because of an image
            }


            this.KeyPreview = true;
            this.KeyDown += QuizForm_KeyDown;
            this.Resize += QuizForm_Resize;

            _card = new RoundedPanel
            {
                BackColor = Color.FromArgb(32, 32, 32),
                Size = new Size(900, 420)   // wider card for side-by-side layout
            };

            _picKnight = new PictureBox
            {
                Size = new Size(220, 220),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            _lblHint = new Label
            {
                Text = "Solve the equation to continue.",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 14, FontStyle.Regular),
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _lblLevel = new Label
            {
                Text = $"Level: {_settings.PlayerProgress.Level}",
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                AutoSize = true
            };

            _xpBar = new ProgressBar
            {
                Style = ProgressBarStyle.Continuous,
                Width = 220,
                Height = 14,
                Maximum = 100   // temporary, updated in UpdateXpBar
            };

            _lblQuestion = new Label
            {
                Text = "Question",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 24, FontStyle.Regular),
                AutoSize = true
            };

            _txtAnswer = new TextBox
            {
                Font = new Font("Segoe UI", 22, FontStyle.Regular),
                Width = 220
            };

            _btnSubmit = new Button
            {
                Text = "OK",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                Width = 130,
                Height = 50,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _btnSubmit.FlatAppearance.BorderSize = 0;
            _btnSubmit.Click += BtnSubmit_Click;

            // --- Ability buttons ---

            _btnHint = new Button
            {
                Text = "Hint",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Width = 90,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Visible = false
            };
            _btnHint.FlatAppearance.BorderSize = 0;
            _btnHint.Click += BtnHint_Click;

            _btnUseToken = new Button
            {
                Text = $"Token ({_settings.PlayerProgress.CheatTokens})",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Width = 110,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(120, 60, 60),
                ForeColor = Color.White
            };
            _btnUseToken.FlatAppearance.BorderSize = 0;
            _btnUseToken.Click += BtnUseToken_Click;

            _btnMathSight = new Button
            {
                Text = "MathSight",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Width = 110,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 80, 120),
                ForeColor = Color.White,
                Visible = false
            };
            _btnMathSight.FlatAppearance.BorderSize = 0;
            _btnMathSight.Click += BtnMathSight_Click;

            _btnSkip = new Button
            {
                Text = "Skip",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Width = 90,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(80, 80, 80),
                ForeColor = Color.White,
                Visible = false
            };
            _btnSkip.FlatAppearance.BorderSize = 0;
            _btnSkip.Click += BtnSkip_Click;

            _btnPowerStrike = new Button
            {
                Text = "PowerStrike",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Width = 110,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(100, 80, 40),
                ForeColor = Color.White,
                Visible = false
            };
            _btnPowerStrike.FlatAppearance.BorderSize = 0;
            _btnPowerStrike.Click += BtnPowerStrike_Click;

            _btnXpBoost = new Button
            {
                Text = "XP Boost",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Width = 110,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 100, 40),
                ForeColor = Color.White,
                Visible = false
            };
            _btnXpBoost.FlatAppearance.BorderSize = 0;
            _btnXpBoost.Click += BtnXpBoost_Click;

            _btnValor = new Button
            {
                Text = "Valor",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Width = 110,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(120, 100, 0),
                ForeColor = Color.White,
                Visible = false
            };
            _btnValor.FlatAppearance.BorderSize = 0;
            _btnValor.Click += BtnValor_Click;

            // Add controls to card
            _card.Controls.Add(_lblHint);
            _card.Controls.Add(_lblLevel);
            _card.Controls.Add(_xpBar);
            _card.Controls.Add(_lblQuestion);
            _card.Controls.Add(_txtAnswer);
            _card.Controls.Add(_btnSubmit);
            _card.Controls.Add(_picKnight);

            _card.Controls.Add(_btnHint);
            _card.Controls.Add(_btnUseToken);
            _card.Controls.Add(_btnMathSight);
            _card.Controls.Add(_btnSkip);
            _card.Controls.Add(_btnPowerStrike);
            _card.Controls.Add(_btnXpBoost);
            _card.Controls.Add(_btnValor);

            this.Controls.Add(_card);

            LayoutCard();
            UpdateXpBar();
            UpdateKnightImage();
            UpdateAbilityButtons();
        }

        private void QuizForm_Resize(object? sender, EventArgs e)
        {
            LayoutCard();
        }

        private void LayoutCard()
        {
            _lblHint.MaximumSize = new Size(400, 0);

            int padding = 30;

            // Split card into left (question area) and right (avatar + abilities)
            int rightColumnWidth = 260;
            int rightXCenter = _card.Width - padding - rightColumnWidth / 2;

            int leftColumnWidth = _card.Width - rightColumnWidth - padding * 3;
            int leftXCenter = padding + leftColumnWidth / 2;

            // --- Right column: Avatar, level, XP, abilities ---

            // Knight image at top of right column
            _picKnight.Location = new Point(
                rightXCenter - _picKnight.Width / 2,
                20
            );

            // Level label under knight
            _lblLevel.Text = $"Level: {_settings.PlayerProgress.Level}";
            _lblLevel.Location = new Point(
                rightXCenter - _lblLevel.Width / 2,
                _picKnight.Bottom + 6
            );

            // XP bar under level label
            _xpBar.Width = rightColumnWidth;
            _xpBar.Location = new Point(
                rightXCenter - _xpBar.Width / 2,
                _lblLevel.Bottom + 6
            );

            int abilityTop = _xpBar.Bottom + 12;
            int rowGapY = 8;

            int currentRowY = abilityTop;

            // Row 1: Hint, Token, MathSight
            LayoutAbilityRow(new[] { _btnHint, _btnUseToken, _btnMathSight }, rightXCenter, ref currentRowY, rowGapY);

            // Row 2: Skip, PowerStrike, XP Boost
            LayoutAbilityRow(new[] { _btnSkip, _btnPowerStrike, _btnXpBoost }, rightXCenter, ref currentRowY, rowGapY);

            // Row 3: Valor (centered alone if visible)
            if (_btnValor.Visible)
            {
                _btnValor.Location = new Point(
                    rightXCenter - _btnValor.Width / 2,
                    currentRowY
                );
                currentRowY += _btnValor.Height + rowGapY;
            }

            int rightBottom = currentRowY;

            // --- Left column: Hint text, question, answer, submit button ---

            int leftY = 40;

            _lblHint.Location = new Point(
                leftXCenter - _lblHint.Width / 2,
                leftY
            );
            leftY = _lblHint.Bottom + 25;

            _lblQuestion.Location = new Point(
                leftXCenter - _lblQuestion.Width / 2,
                leftY
            );
            leftY = _lblQuestion.Bottom + 25;

            _txtAnswer.Location = new Point(
                leftXCenter - _txtAnswer.Width / 2,
                leftY
            );
            leftY = _txtAnswer.Bottom + 20;

            _btnSubmit.Location = new Point(
                leftXCenter - _btnSubmit.Width / 2,
                leftY
            );
            leftY = _btnSubmit.Bottom + 25;

            int contentBottom = Math.Max(leftY, rightBottom + 20);

            _card.Height = Math.Max(contentBottom, 280);

            _card.Location = new Point(
                (this.ClientSize.Width - _card.Width) / 2,
                (this.ClientSize.Height - _card.Height) / 2
            );
        }

        private void LayoutAbilityRow(Button[] buttons, int centerX, ref int currentY, int gapY)
        {
            var visible = buttons.Where(b => b.Visible).ToArray();
            if (visible.Length == 0)
                return;

            int totalWidth = visible.Sum(b => b.Width) + (visible.Length - 1) * 10;
            int startX = centerX - totalWidth / 2;
            int x = startX;

            foreach (var b in visible)
            {
                b.Location = new Point(x, currentY);
                x += b.Width + 10;
            }

            // Assume all buttons have the same height
            currentY += visible[0].Height + gapY;
        }



        private void GenerateQuestion()
        {
            int required = _settings.RequiredCorrectAnswers > 0
                ? _settings.RequiredCorrectAnswers
                : 10;

            // Reset per-question state
            _secondChanceUsedThisQuestion = false;

            // IMPORTANT: do NOT reset _lblHint here.
            // That way the last success/fail text stays visible.

            // Remember previous question
            var previous = _currentQuestion;

            // Try a few times to get a *different* question than last time
            for (int i = 0; i < 5; i++)
            {
                var candidate = _quizEngine.GetNextQuestion();

                if (candidate.a != previous.a || candidate.b != previous.b)
                {
                    _currentQuestion = candidate;
                    break;
                }

                if (i == 4)
                {
                    _currentQuestion = candidate;
                }
            }

            _a = _currentQuestion.a;
            _b = _currentQuestion.b;

            _lblQuestion.Text = $"({_correctCount}/{required})  What is {_a} × {_b}?";

            _txtAnswer.Text = "";
            _txtAnswer.Focus();

            UpdateXpBar();
            UpdateKnightImage();
            UpdateAbilityButtons();
            LayoutCard();
        }

        // --- Ability button handlers ---

        private void BtnHint_Click(object? sender, EventArgs e)
        {
            if (_hintUsedThisSession)
                return;

            int bigger = Math.Max(_a, _b);
            int smaller = Math.Min(_a, _b);

            string message;

            if (smaller == 1)
            {
                message = "MathSight: Any number × 1 stays the same number.";
            }
            else if (smaller <= 4)
            {
                string repeated = bigger.ToString();
                for (int i = 1; i < smaller; i++)
                {
                    repeated += " + " + bigger;
                }

                message = $"MathSight: Think of it as {repeated}.";
            }
            else
            {
                int pairs = smaller / 2;
                int remainder = smaller % 2;

                string repeated = string.Empty;

                for (int i = 0; i < pairs; i++)
                {
                    if (repeated.Length > 0)
                        repeated += " + ";

                    repeated += (bigger * 2).ToString();
                }

                if (remainder == 1)
                {
                    if (repeated.Length > 0)
                        repeated += " + ";

                    repeated += bigger.ToString();
                }

                message = $"MathSight: Think of it as {repeated}.";
            }

            _lblHint.Text = message;
            _lblHint.ForeColor = Color.FromArgb(150, 200, 255);

            _hintUsedThisSession = true;
            UpdateAbilityButtons();
            LayoutCard();
        }

        private void BtnUseToken_Click(object? sender, EventArgs e)
        {
            var progress = _settings.PlayerProgress;

            if (progress.CheatTokens <= 0)
                return;

            int correct = _a * _b;

            // Spend a token
            progress.CheatTokens--;
            AppSettings.Save(_settings);

            // Treat as a correct answer
            _correctCount++;
            bool leveledUp = AwardXpForCorrectAnswer();

            if (leveledUp)
            {
                _lblHint.Text = $"LEVEL UP! (Token) You reached level {_settings.PlayerProgress.Level}! 🎉";
                _lblHint.ForeColor = Color.Gold;
            }
            else
            {
                _lblHint.Text = $"Token used! Correct answer: {correct}.";
                _lblHint.ForeColor = Color.MediumPurple;
            }

            RegisterCorrectForStreak();

            GenerateQuestion();
            LayoutCard();
        }

        private void BtnMathSight_Click(object? sender, EventArgs e)
        {
            if (_mathSightUsedThisSession)
                return;

            int bigger = Math.Max(_a, _b);
            int smaller = Math.Min(_a, _b);

            string message;

            if (smaller == 1)
            {
                message = "MathSight: Any number × 1 stays the same number.";
            }
            else if (smaller <= 4)
            {
                string repeated = bigger.ToString();
                for (int i = 1; i < smaller; i++)
                {
                    repeated += " + " + bigger;
                }

                message = $"MathSight: Think of it as {repeated}.";
            }
            else
            {
                int pairs = smaller / 2;
                int remainder = smaller % 2;

                string repeated = string.Empty;

                for (int i = 0; i < pairs; i++)
                {
                    if (repeated.Length > 0)
                        repeated += " + ";

                    repeated += (bigger * 2).ToString();
                }

                if (remainder == 1)
                {
                    if (repeated.Length > 0)
                        repeated += " + ";

                    repeated += bigger.ToString();
                }

                message = $"MathSight: Think of it as {repeated}.";
            }

            _lblHint.Text = message;
            _lblHint.ForeColor = Color.FromArgb(150, 200, 255);

            _mathSightUsedThisSession = true;
            UpdateAbilityButtons();
            LayoutCard();
        }

        private void BtnSkip_Click(object? sender, EventArgs e)
        {
            if (_skipUsedThisSession)
                return;

            _skipUsedThisSession = true;
            _correctStreak = 0; // skipping breaks streak

            _lblHint.Text = "SkipStone used! Skipping this question...";
            _lblHint.ForeColor = Color.Orange;

            UpdateAbilityButtons();
            GenerateQuestion();
            LayoutCard();
        }

        private void BtnPowerStrike_Click(object? sender, EventArgs e)
        {
            if (_powerStrikeUsedThisSession)
                return;

            // Next correct answer will give double XP
            _powerStrikeQueued = true;
            _powerStrikeUsedThisSession = true;

            _lblHint.Text = "PowerStrike ready! Your next correct answer gives extra XP.";
            _lblHint.ForeColor = Color.FromArgb(255, 215, 128);

            UpdateAbilityButtons();
            LayoutCard();
        }

        private void BtnXpBoost_Click(object? sender, EventArgs e)
        {
            if (_xpBoostUsedThisSession)
                return;

            _xpBoostActive = true;
            _xpBoostUsedThisSession = true;

            _lblHint.Text = "XP Boost activated! All correct answers give extra XP this session.";
            _lblHint.ForeColor = Color.LightGreen;

            UpdateAbilityButtons();
            LayoutCard();
        }

        private void BtnValor_Click(object? sender, EventArgs e)
        {
            if (_valorUsedThisSession || !_valorUnlockedThisSession)
                return;

            _valorUsedThisSession = true;

            _lblHint.Text = "Valor unleashed! The knight pushes through and unlocks the screen!";
            _lblHint.ForeColor = Color.LightGoldenrodYellow;

            UpdateAbilityButtons();
            LayoutCard();

            _solved = true;
            AppSettings.Save(_settings);
            this.Close();
        }

        private void UpdateAbilityButtons()
        {
            var progress = _settings.PlayerProgress;

            // Hint – once per session, only if unlocked
            bool hasHint = RewardSystem.HasAbility(progress, AbilityType.HintPower);
            _btnHint.Visible = hasHint;
            _btnHint.Enabled = hasHint && !_hintUsedThisSession;

            // Token – always visible but only enabled if you have some
            _btnUseToken.Visible = true;
            _btnUseToken.Text = $"Token ({progress.CheatTokens})";
            _btnUseToken.Enabled = progress.CheatTokens > 0;

            // MathSight – once per session
            bool hasMathSight = RewardSystem.HasAbility(progress, AbilityType.MathSight);
            _btnMathSight.Visible = hasMathSight;
            _btnMathSight.Enabled = hasMathSight && !_mathSightUsedThisSession;

            // Skip – once per session
            bool hasSkip = RewardSystem.HasAbility(progress, AbilityType.SkipStone);
            _btnSkip.Visible = hasSkip;
            _btnSkip.Enabled = hasSkip && !_skipUsedThisSession;

            // PowerStrike – once per session
            bool hasPowerStrike = RewardSystem.HasAbility(progress, AbilityType.PowerStrike);
            _btnPowerStrike.Visible = hasPowerStrike;
            _btnPowerStrike.Enabled = hasPowerStrike && !_powerStrikeUsedThisSession && !_powerStrikeQueued;

            // XP Boost – once per session
            bool hasXpBoost = RewardSystem.HasAbility(progress, AbilityType.XpBoost);
            _btnXpBoost.Visible = hasXpBoost;
            _btnXpBoost.Text = _xpBoostActive ? "XP Boost (ON)" : "XP Boost";
            _btnXpBoost.Enabled = hasXpBoost && !_xpBoostUsedThisSession && !_xpBoostActive;

            // Valor – only visible if unlocked by streak, once per session
            _btnValor.Visible = _valorUnlockedThisSession;
            _btnValor.Enabled = _valorUnlockedThisSession && !_valorUsedThisSession;
        }

        private void RegisterCorrectForStreak()
        {
            _correctStreak++;

            var progress = _settings.PlayerProgress;
            bool tokenEarned = false;

            // Token every 5 correct in a row
            if (_correctStreak >= StreakForToken)
            {
                progress.CheatTokens++;
                _correctStreak -= StreakForToken;
                tokenEarned = true;
            }

            // Valor unlock at long streak
            if (!_valorUnlockedThisSession && _correctStreak >= StreakForValor)
            {
                _valorUnlockedThisSession = true;
                _lblHint.Text += "  Valor unlocked for this session!";
            }

            if (tokenEarned)
            {
                _lblHint.Text += "  Streak bonus: you earned a Token!";
            }

            AppSettings.Save(_settings);
            UpdateAbilityButtons();
        }

        // --- XP / Knight helpers ---

        private void UpdateXpBar()
        {
            var progress = _settings.PlayerProgress;

            // XP needed for next level
            int xpNeeded = XpSystem.GetXpRequiredForNextLevel(progress.Level);

            _xpBar.Maximum = xpNeeded;
            _xpBar.Value = Math.Min(progress.CurrentXp, xpNeeded);
        }

        private void UpdateKnightImage()
        {
            try
            {
                var progress = _settings.PlayerProgress;
                int stageIndex = KnightProgression.GetKnightStageIndex(progress.Level);

                // Folder next to the exe: KnightSprites\knight_stage_0.png ...
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string spritesDir = System.IO.Path.Combine(baseDir, "KnightSprites");
                string fileName = $"knight_stage_{stageIndex}.png";
                string fullPath = System.IO.Path.Combine(spritesDir, fileName);

                if (System.IO.File.Exists(fullPath))
                {
                    // Dispose old image to avoid file locks
                    var oldImage = _picKnight.Image;
                    _picKnight.Image = Image.FromFile(fullPath);
                    oldImage?.Dispose();
                }
                else
                {
                    // No file yet → just clear or leave as-is
                    _picKnight.Image = null;
                }
            }
            catch
            {
                // Fail silently – we don't want the lock screen to crash because of an image
                _picKnight.Image = null;
            }
        }

        private bool AwardXpForCorrectAnswer()
        {
            var progress = _settings.PlayerProgress;

            // Base XP for correct answer
            int xpGain = XpSystem.XpPerCorrectAnswer;

            // Global XP boost
            if (_xpBoostActive && RewardSystem.HasAbility(progress, AbilityType.XpBoost))
            {
                xpGain *= 2;
            }

            // PowerStrike on next correct
            if (_powerStrikeQueued && RewardSystem.HasAbility(progress, AbilityType.PowerStrike))
            {
                xpGain *= 2;
                _powerStrikeQueued = false;
            }

            // Add XP
            bool leveledUp = XpSystem.AddXp(progress, xpGain);

            // Save updated progress, knight, abilities
            AppSettings.Save(_settings);

            _lblLevel.Text = $"Level: {_settings.PlayerProgress.Level}";
            UpdateXpBar();
            UpdateKnightImage();
            UpdateAbilityButtons();

            return leveledUp;
        }

        private void ResetProgress()
        {
            _settings.PlayerProgress = new PlayerProgress();

            _powerStrikeQueued = false;
            _xpBoostActive = false;
            _valorUsedThisSession = false;
            _correctStreak = 0;
            _hintUsedThisSession = false;
            _mathSightUsedThisSession = false;
            _skipUsedThisSession = false;
            _powerStrikeUsedThisSession = false;
            _xpBoostUsedThisSession = false;
            _valorUnlockedThisSession = false;
            _secondChanceUsedThisQuestion = false;

            AppSettings.Save(_settings);

            _lblLevel.Text = $"Level: {_settings.PlayerProgress.Level}";
            UpdateXpBar();
            UpdateKnightImage();
            UpdateAbilityButtons();

            _lblHint.Text = "Progress has been reset.";
            _lblHint.ForeColor = Color.Goldenrod;
            LayoutCard();
        }

        // --- Main quiz flow ---

        private void BtnSubmit_Click(object? sender, EventArgs e)
        {
            int required = _settings.RequiredCorrectAnswers > 0
                ? _settings.RequiredCorrectAnswers
                : 10;

            if (!int.TryParse(_txtAnswer.Text, out int answer))
            {
                _lblHint.Text = "Please enter a number (digits only).";
                _lblHint.ForeColor = Color.Goldenrod;
                _txtAnswer.Focus();
                _txtAnswer.SelectAll();
                LayoutCard();
                return;
            }

            int currentA = _a;
            int currentB = _b;
            int correct = currentA * currentB;

            // Ask QuizEngine to record this answer + update difficulty
            bool isCorrect = _quizEngine.SubmitAnswer(answer);

            if (isCorrect)
            {
                _correctCount++;

                // Give XP for correct answer and see if we leveled up
                bool leveledUp = AwardXpForCorrectAnswer();

                if (leveledUp)
                {
                    _lblHint.Text = $"LEVEL UP! You reached level {_settings.PlayerProgress.Level}! 🎉";
                    _lblHint.ForeColor = Color.Gold;
                }
                else
                {
                    _lblHint.Text = $"Correct! {currentA} × {currentB} = {correct}.";
                    _lblHint.ForeColor = Color.FromArgb(0, 200, 0); // green
                }

                RegisterCorrectForStreak();

                if (_correctCount >= required)
                {
                    _lblHint.Text += " Unlocking...";
                    _solved = true;

                    LayoutCard();
                    this.Close();
                    return;
                }

                // Immediately go to next question, while leaving the success/level-up text at the top
                GenerateQuestion();
            }
            else
            {
                AppSettings.Save(_settings);

                var progress = _settings.PlayerProgress;
                bool hasSecondChance = RewardSystem.HasAbility(progress, AbilityType.SecondChance);

                if (hasSecondChance && !_secondChanceUsedThisQuestion)
                {
                    // Use Second Chance: let him retry the SAME question
                    _secondChanceUsedThisQuestion = true;

                    _lblHint.Text = $"Wrong: you answered {answer}. ⚠️ Second Chance lets you try again on this one!";
                    _lblHint.ForeColor = Color.Goldenrod;

                    _txtAnswer.Focus();
                    _txtAnswer.SelectAll();
                    // Do NOT change question here, and don't break streak yet
                }
                else
                {
                    _lblHint.Text = $"Wrong: you answered {answer}, but {currentA} × {currentB} = {correct}.";
                    _lblHint.ForeColor = Color.FromArgb(220, 50, 50); // red

                    // Wrong answer breaks the streak
                    _correctStreak = 0;
                    UpdateAbilityButtons();

                    // Go to next question, while leaving the fail text at the top
                    GenerateQuestion();
                }
            }

            LayoutCard();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_solved && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
            }

            base.OnFormClosing(e);
        }

        private void QuizForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_settings.EnableDeveloperHotkey &&
                e.Control && e.Shift && e.Alt && e.KeyCode == Keys.M)
            {
                e.Handled = true;
                Environment.Exit(0);
                return;
            }

            // Reset progress: Ctrl+Shift+Alt+L
            if (_settings.EnableDeveloperHotkey &&
                e.Control && e.Shift && e.Alt && e.KeyCode == Keys.L)
            {
                e.Handled = true;
                ResetProgress();
                return;
            }

            if (e.Alt && e.KeyCode == Keys.F4)
            {
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                BtnSubmit_Click(this, EventArgs.Empty);
                return;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                BtnSubmit_Click(this, EventArgs.Empty);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
