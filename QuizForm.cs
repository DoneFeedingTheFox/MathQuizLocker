using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
	// Simple rounded panel with soft shadow (currently unused but kept)
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

		private Panel _card = null!;

		private Label _lblQuestion = null!;
		private TextBox _txtAnswer = null!;
		private Button _btnSubmit = null!;
		private Label _lblHint = null!;   // top text: instruction + success/fail messages
		private Label _lblLevel = null!;
		private FantasyXpBar _xpBar = null!;
		private PictureBox _picKnight = null!;
		private Panel _txtWrapper = null!;

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

			// Sync initial UI with stored progress BEFORE first real layout
			UpdateXpBar();
			UpdateKnightImage();
			UpdateAbilityButtons();
			LayoutCard();

			// Now start the quiz (this also calls LayoutCard)
			GenerateQuestion();
		}

		private static string GetAssetPath(params string[] relativeParts)
		{
			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			string assetsDir = Path.Combine(baseDir, "Assets");

			string fullPath = assetsDir;
			foreach (var part in relativeParts)
			{
				fullPath = Path.Combine(fullPath, part);
			}

			return fullPath;
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

			_txtWrapper = new Panel
			{
				BackColor = Color.FromArgb(220, 200, 170),    // slightly darker border color
				Width = 360,
				Height = 70
			};
			_txtWrapper.Padding = new Padding(4);
			_txtWrapper.Paint += (s, e) =>

			{
				var g = e.Graphics;
				g.SmoothingMode = SmoothingMode.AntiAlias;
				using (var path = CreateRoundedRect(
						   new Rectangle(0, 0, _txtWrapper.Width - 1, _txtWrapper.Height - 1),
						   20))
				using (var pen = new Pen(Color.FromArgb(100, 80, 50), 2))
				{
					g.DrawPath(pen, path);
				}
			};


			// Load background from Assets\Backgrounds\Background.png
			try
			{
				string bgPath = GetAssetPath("Backgrounds", "Background.png");

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

			// Main fantasy panel
			_card = new Panel
			{
				BackColor = Color.Transparent
			};

			try
			{
				string panelPath = GetAssetPath("UI", "panel.png");
				if (File.Exists(panelPath))
				{
					var panelImage = Image.FromFile(panelPath);
					_card.BackgroundImage = panelImage;
					_card.BackgroundImageLayout = ImageLayout.Stretch;

					// Scale card to ~75% of screen width, keep aspect ratio
					var screenBounds = Screen.PrimaryScreen.Bounds;
					int targetWidth = (int)(screenBounds.Width * 0.75);    // 75% of screen
					float scale = targetWidth / (float)panelImage.Width;
					int targetHeight = (int)(panelImage.Height * scale);

					_card.Size = new Size(targetWidth, targetHeight);
				}
				else
				{
					// Fallback if image missing
					_card.Size = new Size(1300, 480);
				}
			}
			catch
			{
				_card.Size = new Size(1300, 480);
			}

			_picKnight = new PictureBox
			{
				Size = new Size(360, 360),
				SizeMode = PictureBoxSizeMode.Zoom,
				BackColor = Color.Transparent
			};

			_lblHint = new Label
			{
				Text = "Solve the equation to continue.",
				ForeColor = Color.FromArgb(200, 200, 200),
				Font = new Font("Segoe UI", 20, FontStyle.Regular),
				AutoSize = true,
				MaximumSize = new Size(700, 0),
				TextAlign = ContentAlignment.MiddleCenter
			};

			_lblLevel = new Label
			{
				Text = $"Level: {_settings.PlayerProgress.Level}",
				ForeColor = Color.FromArgb(180, 180, 180),
				Font = new Font("Segoe UI", 14, FontStyle.Regular),
				AutoSize = true
			};

			_xpBar = new FantasyXpBar
			{
				Width = 260,
				Height = 22
			};

			_lblQuestion = new Label
			{
				Text = "Question",
				ForeColor = Color.White,
				Font = new Font("Segoe UI", 28, FontStyle.Bold),
				AutoSize = false,                               
				TextAlign = ContentAlignment.MiddleCenter      
			};


			_txtAnswer = new TextBox
			{
				Font = new Font("Segoe UI", 28, FontStyle.Regular),
				Width = 350,
				BorderStyle = BorderStyle.None,
				BackColor = Color.FromArgb(245, 240, 220),
				ForeColor = Color.Black,
				TextAlign = HorizontalAlignment.Center,
				Padding = new Padding(6),
			};


			_txtWrapper.Controls.Add(_txtAnswer);

			// center the textbox inside the wrapper
			_txtAnswer.Location = new Point(
				(_txtWrapper.Width - _txtAnswer.Width) / 2,
				(_txtWrapper.Height - _txtAnswer.Height) / 2
			);


			_btnSubmit = new Button
			{
				Text = "OK",
				Font = new Font("Segoe UI", 22, FontStyle.Bold),
				Width = 220,
				Height = 70,
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(210, 170, 70),     // golden base
				ForeColor = Color.FromArgb(35, 25, 10),       // dark brown text
				Cursor = Cursors.Hand
			};


			_btnSubmit.FlatAppearance.BorderSize = 2;
			_btnSubmit.FlatAppearance.BorderColor = Color.FromArgb(140, 100, 30);  // darker gold edge

			_btnSubmit.Click += BtnSubmit_Click;
			_btnSubmit.Paint += BtnSubmit_Paint;
			MakeButtonRounded(_btnSubmit, 20);  // a bit more rounded



			_btnSubmit.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 190, 90); // lighter on hover
			_btnSubmit.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 140, 55); // darker on click

			_btnSubmit.TabStop = false;


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
			_card.Controls.Add(_btnSubmit);
			_card.Controls.Add(_picKnight);
			_card.Controls.Add(_txtWrapper);


			_card.Controls.Add(_btnHint);
			_card.Controls.Add(_btnUseToken);
			_card.Controls.Add(_btnMathSight);
			_card.Controls.Add(_btnSkip);
			_card.Controls.Add(_btnPowerStrike);
			_card.Controls.Add(_btnXpBoost);
			_card.Controls.Add(_btnValor);

			this.Controls.Add(_card);

			this.Resize += QuizForm_Resize;
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			LayoutCard(); // make sure everything is correct once the form is visible
		}

		private void QuizForm_Resize(object? sender, EventArgs e)
		{
			LayoutCard();
		}

		private void LayoutCard()
		{
			if (_card == null) return;

			int padding = 60;

			// Right column (knight + abilities)
			int rightColumnWidth = 360;
			int rightXCenter = _card.Width - padding - rightColumnWidth / 2;

			// Left inner area (big question area)
			int innerLeft = padding;
			int innerRight = _card.Width - rightColumnWidth - padding;
			int innerTop = padding + 50;
			int innerBottom = _card.Height - padding;

			int leftColumnWidth = innerRight - innerLeft;
			int leftXCenter = innerLeft + leftColumnWidth / 2;

			// LEFT COLUMN — Hint, Question, Answer, Submit
			int y = innerTop;

			_lblHint.Location = new Point(leftXCenter - _lblHint.Width / 2, y);
			y = _lblHint.Bottom + 30;

			// make the question label span the whole left column and center inside itself
			_lblQuestion.Width = leftColumnWidth;
			_lblQuestion.Location = new Point(innerLeft, y);
			_lblQuestion.Height = 60;   // enough for the 28pt text

			y = _lblQuestion.Bottom + 30;


			_txtWrapper.Location = new Point(leftXCenter - _txtWrapper.Width / 2, y);
			y = _txtWrapper.Bottom + 25;


			_btnSubmit.Location = new Point(leftXCenter - _btnSubmit.Width / 2, y);

			// RIGHT COLUMN — Knight, Level, XP Bar, Abilities
			int rightTop = innerTop + 10;

			_picKnight.Location = new Point(
				rightXCenter - _picKnight.Width / 2,
				rightTop
			);

			_lblLevel.Location = new Point(
				rightXCenter - _lblLevel.Width / 2,
				_picKnight.Bottom + 10
			);

			_xpBar.Width = rightColumnWidth - 120;
			_xpBar.Height = 22;
			_xpBar.Location = new Point(
				rightXCenter - _xpBar.Width / 2,
				_lblLevel.Bottom + 12
			);

			int currentRowY = _xpBar.Bottom + 18;
			int rowGapY = 12;

			LayoutAbilityRow(new[] { _btnHint, _btnUseToken, _btnMathSight },
				rightXCenter, ref currentRowY, rowGapY);

			LayoutAbilityRow(new[] { _btnSkip, _btnPowerStrike, _btnXpBoost },
				rightXCenter, ref currentRowY, rowGapY);

			if (_btnValor.Visible)
			{
				_btnValor.Location = new Point(
					rightXCenter - _btnValor.Width / 2,
					currentRowY
				);
			}

			// Center the entire panel on screen
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

			currentY += visible[0].Height + gapY;
		}

		private void MakeButtonRounded(Button btn, int radius = 10)
		{
			var path = new GraphicsPath();
			int d = radius * 2;
			var rect = new Rectangle(0, 0, btn.Width, btn.Height);

			path.AddArc(rect.X, rect.Y, d, d, 180, 90);
			path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
			path.AddArc(rect.X, rect.Bottom - d, d, d, 0, 90);
			path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
			path.CloseFigure();

			btn.Region = new Region(path);
		}

		private void BtnSubmit_Paint(object? sender, PaintEventArgs e)
		{
			var btn = (Button)sender!;
			var g = e.Graphics;
			g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

			Rectangle rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);

			// Fantasy gold gradient
			using (var brush = new LinearGradientBrush(
					   rect,
					   Color.FromArgb(230, 200, 90),   // top light gold
					   Color.FromArgb(185, 150, 60),   // bottom deeper gold
					   LinearGradientMode.Vertical))
			{
				g.FillRectangle(brush, rect);
			}

			// Subtle inner shadow on top
			using (var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
			{
				g.FillRectangle(shadow, new Rectangle(0, 0, btn.Width, btn.Height / 4));
			}

			// Outer border
			using (var pen = new Pen(Color.FromArgb(120, 90, 40), 2))
			{
				g.DrawRectangle(pen, rect);
			}

			// Draw text centered
			TextRenderer.DrawText(
				g,
				btn.Text,
				btn.Font,
				rect,
				Color.FromArgb(40, 30, 10),   // dark brown
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
			);
		}


		private GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
		{
			var path = new GraphicsPath();
			int d = radius * 2;

			path.AddArc(rect.X, rect.Y, d, d, 180, 90);
			path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
			path.AddArc(rect.X, rect.Bottom - d, d, d, 0, 90);
			path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
			path.CloseFigure();
			return path;
		}


		private void GenerateQuestion()
		{
			int required = _settings.RequiredCorrectAnswers > 0
				? _settings.RequiredCorrectAnswers
				: 10;

			_secondChanceUsedThisQuestion = false;

			var previous = _currentQuestion;

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

			progress.CheatTokens--;
			AppSettings.Save(_settings);

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
			_correctStreak = 0;

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

			bool hasHint = RewardSystem.HasAbility(progress, AbilityType.HintPower);
			_btnHint.Visible = hasHint;
			_btnHint.Enabled = hasHint && !_hintUsedThisSession;

			_btnUseToken.Visible = true;
			_btnUseToken.Text = $"Token ({progress.CheatTokens})";
			_btnUseToken.Enabled = progress.CheatTokens > 0;

			bool hasMathSight = RewardSystem.HasAbility(progress, AbilityType.MathSight);
			_btnMathSight.Visible = hasMathSight;
			_btnMathSight.Enabled = hasMathSight && !_mathSightUsedThisSession;

			bool hasSkip = RewardSystem.HasAbility(progress, AbilityType.SkipStone);
			_btnSkip.Visible = hasSkip;
			_btnSkip.Enabled = hasSkip && !_skipUsedThisSession;

			bool hasPowerStrike = RewardSystem.HasAbility(progress, AbilityType.PowerStrike);
			_btnPowerStrike.Visible = hasPowerStrike;
			_btnPowerStrike.Enabled = hasPowerStrike && !_powerStrikeUsedThisSession && !_powerStrikeQueued;

			bool hasXpBoost = RewardSystem.HasAbility(progress, AbilityType.XpBoost);
			_btnXpBoost.Visible = hasXpBoost;
			_btnXpBoost.Text = _xpBoostActive ? "XP Boost (ON)" : "XP Boost";
			_btnXpBoost.Enabled = hasXpBoost && !_xpBoostUsedThisSession && !_xpBoostActive;

			_btnValor.Visible = _valorUnlockedThisSession;
			_btnValor.Enabled = _valorUnlockedThisSession && !_valorUsedThisSession;

			LayoutCard();
		}

		private void RegisterCorrectForStreak()
		{
			_correctStreak++;

			var progress = _settings.PlayerProgress;
			bool tokenEarned = false;

			if (_correctStreak >= StreakForToken)
			{
				progress.CheatTokens++;
				_correctStreak -= StreakForToken;
				tokenEarned = true;
			}

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

				string fileName = $"knight_stage_{stageIndex}.png";
				string fullPath = GetAssetPath("KnightSprites", fileName);

				if (File.Exists(fullPath))
				{
					var oldImage = _picKnight.Image;
					_picKnight.Image = Image.FromFile(fullPath);
					oldImage?.Dispose();
				}
				else
				{
					_picKnight.Image = null;
				}
			}
			catch
			{
				_picKnight.Image = null;
			}
		}

		private bool AwardXpForCorrectAnswer()
		{
			var progress = _settings.PlayerProgress;

			int xpGain = XpSystem.XpPerCorrectAnswer;

			if (_xpBoostActive && RewardSystem.HasAbility(progress, AbilityType.XpBoost))
			{
				xpGain *= 2;
			}

			if (_powerStrikeQueued && RewardSystem.HasAbility(progress, AbilityType.PowerStrike))
			{
				xpGain *= 2;
				_powerStrikeQueued = false;
			}

			bool leveledUp = XpSystem.AddXp(progress, xpGain);

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

			bool isCorrect = _quizEngine.SubmitAnswer(answer);

			if (isCorrect)
			{
				_correctCount++;

				bool leveledUp = AwardXpForCorrectAnswer();

				if (leveledUp)
				{
					_lblHint.Text = $"LEVEL UP! You reached level {_settings.PlayerProgress.Level}! 🎉";
					_lblHint.ForeColor = Color.Gold;
				}
				else
				{
					_lblHint.Text = $"Correct! {currentA} × {currentB} = {correct}.";
					_lblHint.ForeColor = Color.FromArgb(0, 200, 0);
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

				GenerateQuestion();
			}
			else
			{
				AppSettings.Save(_settings);

				var progress = _settings.PlayerProgress;
				bool hasSecondChance = RewardSystem.HasAbility(progress, AbilityType.SecondChance);

				if (hasSecondChance && !_secondChanceUsedThisQuestion)
				{
					_secondChanceUsedThisQuestion = true;

					_lblHint.Text = $"Wrong: you answered {answer}. ⚠️ Second Chance lets you try again on this one!";
					_lblHint.ForeColor = Color.Goldenrod;

					_txtAnswer.Focus();
					_txtAnswer.SelectAll();
				}
				else
				{
					_lblHint.Text = $"Wrong: you answered {answer}, but {currentA} × {currentB} = {correct}.";
					_lblHint.ForeColor = Color.FromArgb(220, 50, 50);

					_correctStreak = 0;
					UpdateAbilityButtons();

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
