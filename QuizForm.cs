using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
	public partial class QuizForm : Form
	{
		// Graphics Resources (cached)
		private readonly Font _damageFont = new Font("Segoe UI", 48, FontStyle.Bold);
		private readonly SolidBrush _damageBrush = new SolidBrush(Color.Red);
		private readonly SolidBrush _overlayBrush = new SolidBrush(Color.FromArgb(180, 40, 0, 0));

		private MonsterService _monsterService;

		// Manual GDI+ Renderables (Images + Rectangles)
		private Image? _knightImg, _monsterImg;
		private Image? _die1Img, _die2Img, _mulImg;
		private Image? _chestImg, _lootImg;

		private RectangleF _knightRect, _monsterRect;
		private RectangleF _knightDrawRect, _monsterDrawRect;

		private RectangleF _die1Rect, _die2Rect, _mulRect;
		private RectangleF _chestRect, _lootRect;

		// Visibility flags for non-control sprites
		private bool _diceVisible = true;
		private bool _chestVisible = false;
		private bool _lootVisible = false;

		// Core Services
		private readonly AppSettings _settings;
		private readonly QuizEngine _quizEngine;
		private readonly GameSessionManager _session;

		// Combat & State
		private int _a, _b;
		private bool _isAnimating = false, _isInternalClose = false;
		private string _currentMonsterName = "goblin";

		// Animation / physics
		private readonly System.Windows.Forms.Timer _heartbeat = new System.Windows.Forms.Timer { Interval = 15 };
		private long _lastTickMs;

		private bool _isDicePhysicsActive = false;

		// Loot & Progression
		private bool _awaitingChestOpen = false;
		private int _pendingKnightStage = 1;
		private int _equippedKnightStage = 1;
		private string? _pendingLootItemFile;

		// Chest animation variables
		private int _chestShakeTicks = 0;
		private bool _isChestOpening = false;

		// Dice physics variables
		private readonly Random _rng = new Random();
		private PointF[] _diceVelocities = new PointF[3];
		private PointF[] _diceCurrentPositions = new PointF[3];
		private float[] _diceRotationAngles = new float[3];
		private int _scrambleTicks = 0;
		private float _diceSizePx = 120f;

		// Floating damage
		private readonly List<FloatingText> _damageNumbers = new List<FloatingText>();

		// story mode
		private Label _lblStoryText;
		private Button _btnStoryContinue, _btnStoryExit;
		private bool _isShowingStory = false;

		// Countdown Timer for answering questions
		private readonly System.Windows.Forms.Timer _countdownTimer = new System.Windows.Forms.Timer();
		private int _secondsRemaining = 10;
		private Label _lblTimer;
		private bool _isQuestionPending = false;

		private readonly Random _random = new Random();

		// Flicker prevention
		protected override CreateParams CreateParams
		{
			get
			{
				var cp = base.CreateParams;
				// Keep existing behavior unless you decide to remove it after testing.
				cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
				return cp;
			}
		}

		public QuizForm(AppSettings settings)
		{
			_settings = settings;
			_quizEngine = new QuizEngine(_settings);
			_session = new GameSessionManager(_settings, _quizEngine);

			_monsterService = new MonsterService();

			// Performance / flicker prevention
			this.DoubleBuffered = true;
			this.SetStyle(ControlStyles.AllPaintingInWmPaint |
						  ControlStyles.UserPaint |
						  ControlStyles.OptimizedDoubleBuffer, true);
			this.SetStyle(ControlStyles.Opaque, false);
			this.UpdateStyles();

			// Initialize UI
			LocalizationService.LoadLanguage("no");
			InitializeCombatUi();
			InitStoryUi();

			// Set initial progression state
			_equippedKnightStage = _settings.PlayerProgress.EquippedKnightStage > 0
				? _settings.PlayerProgress.EquippedKnightStage
				: 1;

			// Heartbeat
			_lastTickMs = Environment.TickCount64;
			_heartbeat.Tick += Heartbeat_Tick;
			_heartbeat.Start();

			// Events
			this.Resize += QuizForm_Resize;

			_countdownTimer.Tick += CountdownTimer_Tick;
			_countdownTimer.Interval = 1000;
		}

		private void Heartbeat_Tick(object? sender, EventArgs e)
		{
			if (!IsHandleCreated || IsDisposed) return;

			long now = Environment.TickCount64;
			float dt = Math.Clamp((now - _lastTickMs) / 1000f, 0f, 0.05f);
			_lastTickMs = now;

			Rectangle dirty = Rectangle.Empty;
			bool anyDirty = false;

			// Floating text lifetime handling (replaces _physicsTimer)
			if (UpdateFloatingText(dt, ref dirty)) anyDirty = true;

			// Drive animations/physics from one heartbeat
			if (_isDicePhysicsActive)
			{
				if (UpdateDicePhysics(dt, ref dirty)) anyDirty = true;
			}

			if (_meleeActive)
			{
				if (UpdateMeleeStrike(ref dirty)) anyDirty = true;
			}

			if (_monsterLungeActive)
			{
				if (UpdateMonsterLunge(ref dirty)) anyDirty = true;
			}

			if (_isChestOpening)
			{
				if (UpdateChestShake(ref dirty)) anyDirty = true;
			}

			if (anyDirty)
			{
				// Targeted redrawing (moving pixels only)
				Invalidate(dirty);
			}
		}

		private void CountdownTimer_Tick(object? sender, EventArgs e)
		{
			if (_session.CurrentPlayerHealth <= 0)
			{
				_countdownTimer.Stop();
				return;
			}

			_secondsRemaining--;
			_lblTimer.Text = $"{_secondsRemaining}";

			if (_secondsRemaining <= 0)
			{
				int damage = _session.GetTimerDamage();
				_secondsRemaining = _session.CurrentMonsterAttackInterval;
				AnimateMonsterAttack(damage);
			}
		}

		private Rectangle GetCombatZone()
		{
			var k = Rectangle.Round(_knightDrawRect);
			var m = Rectangle.Round(_monsterDrawRect);

			int yTop = Math.Min(k.Top, m.Top) - 60;
			int yBot = Math.Max(k.Bottom, m.Bottom) + 40;
			int xLeft = Math.Min(k.Left, m.Left) - 50;
			int xRight = Math.Max(k.Right, m.Right) + 50;

			return Rectangle.FromLTRB(xLeft, yTop, xRight, yBot);
		}

		private Rectangle GetMeleeArea()
		{
			var k = Rectangle.Round(_knightDrawRect);
			var m = Rectangle.Round(_monsterDrawRect);

			int yTop = k.Top - 60;
			int yBot = k.Bottom + 20;
			int xLeft = Math.Min(_meleeOrigXInt, k.Left) - 50;
			int xRight = m.Right + 100;

			return Rectangle.FromLTRB(xLeft, yTop, xRight, yBot);
		}

		private Rectangle GetDiceArea()
		{
			int w = this.ClientSize.Width;
			int h = this.ClientSize.Height;

			int areaWidth = (int)(w * 0.4);
			int areaHeight = (int)(h * 0.25);
			int x = (w - areaWidth) / 2;
			int y = 0;

			return new Rectangle(x, y, areaWidth, areaHeight);
		}

		private void CloseStoryAndResume()
		{
			// Allow OnPaint to switch back to Game drawing logic
			_isShowingStory = false;
			_isAnimating = false;
			_isDicePhysicsActive = false;
			_diceVisible = true;

			// Hide Story UI
			_lblStoryText.Visible = false;
			_btnStoryContinue.Visible = false;
			_btnStoryExit.Visible = false;

			// Restore Combat UI & Layers
			_txtAnswer.Visible = true;
			_btnSubmit.Visible = true;
			_lblLevel.Visible = true;
			_lblXpStatus.Visible = true;

			_txtAnswer.BringToFront();
			_btnSubmit.BringToFront();
			_lblFeedback.BringToFront();

			// Reset World & Session
			SpawnMonster();
			ApplyBiomeForCurrentLevel();
			SetKnightIdleSprite();

			_monsterService = new MonsterService();
			var initialMonster = _monsterService.GetMonster("goblin");
			_session.StartNewBattle(initialMonster);

			GenerateQuestion();
			_txtAnswer.Focus();
			this.Invalidate();
		}

		private void InitStoryUi()
		{
			_lblStoryText = new Label
			{
				Name = "_lblStoryText",
				ForeColor = Color.FromArgb(60, 40, 20),
				TextAlign = ContentAlignment.MiddleCenter,
				BackColor = Color.Transparent,
				Font = new Font("Palatino Linotype", 18, FontStyle.Bold),
				Visible = false,
				AutoSize = false,
				AutoEllipsis = true
			};

			_btnStoryContinue = new Button
			{
				Name = "_btnStoryContinue",
				Text = "Continue Journey",
				Size = new Size(200, 45),
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(120, 80, 40),
				ForeColor = Color.Wheat,
				Visible = false
			};
			_btnStoryContinue.FlatAppearance.BorderSize = 2;
			_btnStoryContinue.FlatAppearance.BorderColor = Color.SaddleBrown;
			_btnStoryContinue.Click += (s, e) => CloseStoryAndResume();

			_btnStoryExit = new Button
			{
				Name = "_btnStoryExit",
				Text = "Retire to Desktop",
				Size = new Size(200, 45),
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(40, 40, 40),
				ForeColor = Color.LightGray,
				Visible = false
			};
			_btnStoryExit.FlatAppearance.BorderSize = 1;
			_btnStoryExit.Click += (s, e) =>
			{
				AppSettings.Save(_settings);
				Environment.Exit(0);
			};

			this.Controls.Add(_lblStoryText);
			this.Controls.Add(_btnStoryContinue);
			this.Controls.Add(_btnStoryExit);
		}

		private void QuizForm_Resize(object? sender, EventArgs e)
		{
			if (_isShowingStory)
				ShowStoryScreen();
			else
				LayoutCombat();
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			Program.SetExternalAutostart(true);

			this.BeginInvoke(new Action(() =>
			{
				bool isFirstIntro = (_settings.PlayerProgress.Level == 1 && _settings.PlayerProgress.CurrentXp == 0);

				if (isFirstIntro)
				{
					// Ensure combat timer is not running behind the journal
					_countdownTimer.Stop();
					_lblTimer.Visible = false;

					_isShowingStory = true;
					_txtAnswer.Visible = _btnSubmit.Visible = false;
					_lblLevel.Visible = _lblXpStatus.Visible = false;

					ApplyBiomeForCurrentLevel();
					ShowStoryScreen();
				}
				else
				{
					_isShowingStory = false;

					ApplyBiomeForCurrentLevel();
					SpawnMonster();
					SetKnightIdleSprite();

					UpdatePlayerHud();
					LayoutCombat();
					GenerateQuestion();
				}

				this.Invalidate();
			}));
		}

		private void SetKnightIdleSprite()
		{
			string path = AssetPaths.KnightSprite(_equippedKnightStage);
			ReplaceImage(ref _knightImg, AssetCache.GetImageClone(path));
			RecalcKnightDrawRect();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if (_isShowingStory)
			{
				base.OnPaint(e);
				return;
			}

			e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
			e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
			e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
			e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;

			base.OnPaint(e);

			var g = e.Graphics;

			// 1. Sprites
			if (_knightImg != null) g.DrawImage(_knightImg, _knightDrawRect);
			if (_monsterImg != null) g.DrawImage(_monsterImg, _monsterDrawRect);

			// 2. Health bars
			DrawHealthBar(g, Rectangle.Round(_knightRect), _session.CurrentPlayerHealth, 100, Color.LimeGreen);
			DrawHealthBar(g, Rectangle.Round(_monsterRect), _session.CurrentMonsterHealth, _session.MaxMonsterHealth, Color.Red);

			// 3. Dice (physics or static)
			if (_isDicePhysicsActive)
			{
				for (int i = 0; i < 3; i++)
				{
					Image? img = (i == 0) ? _die1Img : (i == 1) ? _die2Img : _mulImg;
					if (img == null) continue;

					var pos = _diceCurrentPositions[i];
					var state = g.Save();
					g.TranslateTransform(pos.X + _diceSizePx / 2f, pos.Y + _diceSizePx / 2f);
					g.RotateTransform(_diceRotationAngles[i]);
					g.DrawImage(img, -_diceSizePx / 2f, -_diceSizePx / 2f, _diceSizePx, _diceSizePx);
					g.Restore(state);
				}
			}
			else if (_diceVisible)
			{
				if (_die1Img != null) g.DrawImage(_die1Img, _die1Rect);
				if (_die2Img != null) g.DrawImage(_die2Img, _die2Rect);
				if (_mulImg != null) g.DrawImage(_mulImg, _mulRect);
			}

			// 4. Loot and Chest
			if (_chestVisible && _chestImg != null) g.DrawImage(_chestImg, _chestRect);
			if (_lootVisible && _lootImg != null) g.DrawImage(_lootImg, _lootRect);

			// 5. Floating Damage
			foreach (var dp in _damageNumbers.ToList())
				g.DrawString(dp.Text, _damageFont, _damageBrush, dp.Position);

			// 6. Game Over Overlay
			if (_session.CurrentPlayerHealth <= 0)
				g.FillRectangle(_overlayBrush, this.ClientRectangle);
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			_countdownTimer?.Stop();
			_heartbeat?.Stop();

			DisposeRenderImages();

			base.OnFormClosing(e);
		}

		public class FloatingText
		{
			public string Text { get; set; } = "";
			public PointF Position;
			public Color TextColor { get; set; }
			public float Opacity { get; set; } = 1.0f;
			public float VelocityY;
		}

		private void DisposeRenderImages()
		{
			_knightImg?.Dispose(); _knightImg = null;
			_monsterImg?.Dispose(); _monsterImg = null;
			_die1Img?.Dispose(); _die1Img = null;
			_die2Img?.Dispose(); _die2Img = null;
			_mulImg?.Dispose(); _mulImg = null;
			_chestImg?.Dispose(); _chestImg = null;
			_lootImg?.Dispose(); _lootImg = null;
		}

		private static void ReplaceImage(ref Image? target, Image? next)
		{
			if (ReferenceEquals(target, next)) return;
			target?.Dispose();
			target = next;
		}

		private void RecalcKnightDrawRect()
		{
			if (_knightImg == null) { _knightDrawRect = _knightRect; return; }
			_knightDrawRect = GetPaddedBounds(_knightImg, Rectangle.Round(_knightRect));
		}

		private void RecalcMonsterDrawRect()
		{
			if (_monsterImg == null) { _monsterDrawRect = _monsterRect; return; }
			_monsterDrawRect = GetPaddedBounds(_monsterImg, Rectangle.Round(_monsterRect));
		}
	}
}
