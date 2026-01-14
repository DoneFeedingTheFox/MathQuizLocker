using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
	public partial class QuizForm : Form
	{
		private readonly AppSettings _settings;
		private readonly QuizEngine _quizEngine;
		private readonly GameSessionManager _session;

		// Combat State
		private int _a, _b, _monsterHealth, _maxMonsterHealth;
		private int _playerHealth, _maxPlayerHealth = 100;
		private bool _isInternalClose = false;
		private string _currentMonsterName = "slime";

		// Animation logic
		private bool _isAnimating = false;
		private Point _knightOriginalPos;
		private System.Windows.Forms.Timer _animationTimer = new();

		// Dice Animation State
		private System.Windows.Forms.Timer _diceTimer = null!;
		private int _scrambleTicks = 0;
		private const int MaxScrambleTicks = 25;

		// Physics for 3 dice
		private PointF[] _diceVelocities = null!;
		private Point[] _diceOriginalPositions = null!;
		private PointF[] _diceCurrentPositions = null!;
		private Random _rng = new Random();
		private bool _isDiceAnimating = false;
		private float[] _diceRotationAngles = null!;

		// --- Floating Damage Numbers ---
		private List<FloatingDamage> _damageNumbers = new List<FloatingDamage>();

		// --- Bars are now rendered on the form surface (required for correct alpha blending) ---
		private Rectangle _playerHealthBarRect;
		private Rectangle _monsterHealthBarRect;
		private Rectangle _playerXpBarRect;

		private BiomeManager? _biomeManager;
		private string? _currentBiomeId;

		// --- Performance caches (avoid per-frame allocations) ---
		private readonly Font _damageFont = new Font("Segoe UI", 32f, FontStyle.Bold);
		private static readonly Font _barTextFont = new Font("Segoe UI", 9f, FontStyle.Bold);

		// Dice master bitmaps: avoid per-frame string/path churn + cache lookups.
		private readonly Bitmap?[] _diceMasters = new Bitmap?[7]; // 1..6
		private Bitmap? _multiplyMaster;

		// Bars layer: render expensive gradients/paths only when values/layout change.
		private Bitmap? _barsLayerBitmap;
		private Rectangle _barsLayerBounds;
		private BarsSnapshot _barsSnapshot;


		private readonly System.Windows.Forms.Timer _damageTimer = new System.Windows.Forms.Timer();

		public QuizForm(AppSettings settings)
		{
			_settings = settings ?? new AppSettings();
			_quizEngine = new QuizEngine(_settings);
			_session = new GameSessionManager(_settings, _quizEngine);
			_playerHealth = _maxPlayerHealth;

			_damageTimer.Interval = 16; // ~60 FPS
			_damageTimer.Tick += (s, e) => TickFloatingDamage();

			InitializeCombatUi();
		}

		protected override CreateParams CreateParams
		{
			get
			{
				var cp = base.CreateParams;
				cp.Style &= ~0x00080000;    // WS_SYSMENU
											// NOTE: WS_EX_COMPOSITED often reduces flicker but can significantly hurt performance
											// on low-end laptops because it forces full-window compositing.
				cp.ExStyle |= 0x02000000;
				return cp;
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e); // Draws background
			var g = e.Graphics;

			// Fast defaults (important on low-end laptops)
			g.SmoothingMode = SmoothingMode.None;
			g.InterpolationMode = InterpolationMode.NearestNeighbor;
			g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;

			// 1) Bars (cached layer; expensive paths/gradients rendered only when values/layout change)
			DrawBars(g);

			// 2) Animating dice
			if (_isDiceAnimating && _diceCurrentPositions != null)
			{
				EnsureDiceMastersInitialized();

				Control[] diceControls = { _die1, _die2, _picMultiply };
				for (int i = 0; i < 3; i++)
				{
					Bitmap? master = (i == 2)
						? _multiplyMaster
						: _diceMasters[_rng.Next(1, 7)];

					if (master == null) continue;

					var ctrl = diceControls[i];
					var state = g.Save();

					g.TranslateTransform(
						_diceCurrentPositions[i].X + ctrl.Width / 2f,
						_diceCurrentPositions[i].Y + ctrl.Height / 2f
					);
					g.RotateTransform(_diceRotationAngles[i]);
					g.DrawImage(master, -ctrl.Width / 2f, -ctrl.Height / 2f, ctrl.Width, ctrl.Height);
					g.Restore(state);
				}
			}

			// 3) Floating damage numbers (AA for text only)
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
			lock (_damageNumbers)
			{
				for (int i = _damageNumbers.Count - 1; i >= 0; i--)
				{
					var num = _damageNumbers[i];

					// Calculate alpha for fade-out
					int alpha = (int)(num.Opacity * 255);
					int shadowAlpha = (int)(num.Opacity * 150);

					using (var mainBrush = new SolidBrush(Color.FromArgb(alpha, num.Color)))
					using (var shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha, Color.Black)))
					{
						// Draw shadow first (offset by 2 pixels) to make text pop against background
						g.DrawString(num.Text, _damageFont, shadowBrush, num.Position.X + 2, num.Position.Y + 2);

						// Draw main colored text
						g.DrawString(num.Text, _damageFont, mainBrush, num.Position);
					}
				}
			}
		}

		private void DrawBars(Graphics g)
		{
			// Defensive: layout may not have run yet.
			if (_playerHealthBarRect.Width <= 0 || _monsterHealthBarRect.Width <= 0) return;

			EnsureBarsLayerUpToDate();

			if (_barsLayerBitmap != null)
			{
				g.DrawImageUnscaled(_barsLayerBitmap, _barsLayerBounds.Location);
				return;
			}

			// Fallback (should rarely happen)
			g.SmoothingMode = SmoothingMode.AntiAlias;
			DrawFantasyBar(g, _playerHealthBarRect, _playerHealth, _maxPlayerHealth, showText: false);
			DrawFantasyBar(g, _monsterHealthBarRect, Math.Max(0, _monsterHealth), Math.Max(1, _maxMonsterHealth), showText: false);
		}

		private struct BarsSnapshot
		{
			public Rectangle PlayerRect;
			public Rectangle MonsterRect;
			public Rectangle XpRect;
			public int Player;
			public int PlayerMax;
			public int Monster;
			public int MonsterMax;
			public int XpValue;
			public int XpMax;
			public bool XpShowText;
		}

		private void EnsureBarsLayerUpToDate()
		{
			int xpValue = 0;
			int xpMax = 1;
			bool xpShowText = false;

			if (_playerXpBarRect.Width > 0)
			{
				var p = _settings.PlayerProgress;
				xpMax = Math.Max(1, XpSystem.GetXpRequiredForNextLevel(p.Level));
				xpValue = Math.Max(0, Math.Min(xpMax, p.CurrentXp));
				xpShowText = true;
			}

			var snap = new BarsSnapshot
			{
				PlayerRect = _playerHealthBarRect,
				MonsterRect = _monsterHealthBarRect,
				XpRect = _playerXpBarRect,
				Player = _playerHealth,
				PlayerMax = _maxPlayerHealth,
				Monster = Math.Max(0, _monsterHealth),
				MonsterMax = Math.Max(1, _maxMonsterHealth),
				XpValue = xpValue,
				XpMax = xpMax,
				XpShowText = xpShowText
			};

			bool needsRebuild =
				_barsLayerBitmap == null ||
				_barsSnapshot.PlayerRect != snap.PlayerRect ||
				_barsSnapshot.MonsterRect != snap.MonsterRect ||
				_barsSnapshot.XpRect != snap.XpRect ||
				_barsSnapshot.Player != snap.Player ||
				_barsSnapshot.PlayerMax != snap.PlayerMax ||
				_barsSnapshot.Monster != snap.Monster ||
				_barsSnapshot.MonsterMax != snap.MonsterMax ||
				_barsSnapshot.XpValue != snap.XpValue ||
				_barsSnapshot.XpMax != snap.XpMax ||
				_barsSnapshot.XpShowText != snap.XpShowText;

			if (!needsRebuild) return;

			_barsSnapshot = snap;

			Rectangle bounds = Rectangle.Union(_playerHealthBarRect, _monsterHealthBarRect);
			if (_playerXpBarRect.Width > 0) bounds = Rectangle.Union(bounds, _playerXpBarRect);
			bounds = Rectangle.Inflate(bounds, 4, 4);
			if (bounds.Width <= 0 || bounds.Height <= 0) return;

			_barsLayerBounds = bounds;

			_barsLayerBitmap?.Dispose();
			_barsLayerBitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

			using (var bg = Graphics.FromImage(_barsLayerBitmap))
			{
				bg.Clear(Color.Transparent);
				bg.TranslateTransform(-bounds.X, -bounds.Y);
				bg.SmoothingMode = SmoothingMode.AntiAlias;
				bg.InterpolationMode = InterpolationMode.HighQualityBilinear;
				bg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

				DrawFantasyBar(bg, _playerHealthBarRect, _playerHealth, _maxPlayerHealth, showText: false);
				DrawFantasyBar(bg, _monsterHealthBarRect, Math.Max(0, _monsterHealth), Math.Max(1, _maxMonsterHealth), showText: false);
				if (_playerXpBarRect.Width > 0)
					DrawFantasyBar(bg, _playerXpBarRect, xpValue, xpMax, showText: true);
			}
		}

		private static void DrawFantasyBar(Graphics g, Rectangle bounds, int value, int maximum, bool showText)
		{
			if (bounds.Width <= 4 || bounds.Height <= 4) return;
			maximum = Math.Max(1, maximum);
			value = Math.Max(0, Math.Min(maximum, value));

			var outer = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
			int inset = 2;
			var inner = Rectangle.Inflate(outer, -inset, -inset);
			int radius = Math.Max(6, inner.Height / 2);

			Color trackFill = Color.FromArgb(40, 40, 40);
			Color trackBorder = Color.FromArgb(220, 220, 220);
			Color fillA = Color.FromArgb(120, 230, 140);
			Color fillB = Color.FromArgb(40, 180, 90);

			using (var trackPath = CreateRoundRect(inner, radius))
			using (var trackBrush = new SolidBrush(trackFill))
			using (var borderPen = new Pen(trackBorder, 2f))
			{
				g.FillPath(trackBrush, trackPath);
				g.DrawPath(borderPen, trackPath);
			}

			float pct = (float)value / maximum;
			pct = Math.Max(0f, Math.Min(1f, pct));

			var fillRect = Rectangle.Inflate(inner, -2, -2);
			fillRect.Width = (int)Math.Round(fillRect.Width * pct);

			if (fillRect.Width > 0)
			{
				int minWidth = Math.Min(fillRect.Height, 16);
				if (fillRect.Width < minWidth) fillRect.Width = minWidth;

				using (var fillPath = CreateRoundRect(fillRect, fillRect.Height / 2))
				using (var fillBrush = new LinearGradientBrush(fillRect, fillA, fillB, LinearGradientMode.Vertical))
				{
					g.FillPath(fillBrush, fillPath);
				}
			}

			if (showText)
			{
				string text = $"{value}/{maximum}";
				TextRenderer.DrawText(
					g,
					text,
					_barTextFont,
					inner,
					Color.White,
					TextFormatFlags.HorizontalCenter |
					TextFormatFlags.VerticalCenter |
					TextFormatFlags.NoPadding |
					TextFormatFlags.SingleLine
				);
			}
		}

		private static GraphicsPath CreateRoundRect(Rectangle r, int radius)
		{
			var path = new GraphicsPath();

			if (radius <= 0)
			{
				path.AddRectangle(r);
				path.CloseFigure();
				return path;
			}

			int d = radius * 2;
			if (d > r.Width) d = r.Width;
			if (d > r.Height) d = r.Height;

			var arc = new Rectangle(r.Location, new Size(d, d));

			path.AddArc(arc, 180, 90);
			arc.X = r.Right - d;
			path.AddArc(arc, 270, 90);
			arc.Y = r.Bottom - d;
			path.AddArc(arc, 0, 90);
			arc.X = r.Left;
			path.AddArc(arc, 90, 90);

			path.CloseFigure();
			return path;
		}

		private void AddFloatingDamage(Control target, int damage, Color color)
		{
			float x = target.Left + (target.Width / 2f) - 20f;
			float y = target.Top - 60f;

			var dmg = new FloatingDamage
			{
				Text = $"-{damage}",
				Position = new PointF(x, y),
				Opacity = 1.0f,
				Color = color,
				VelocityY = -2.2f
			};

			lock (_damageNumbers)
			{
				_damageNumbers.Add(dmg);
			}

			if (!_damageTimer.Enabled)
				_damageTimer.Start();

			InvalidateDamageRegion();
		}

		private void InvalidateDamageRegion()
		{
			Rectangle r = Rectangle.Empty;

			lock (_damageNumbers)
			{
				foreach (var d in _damageNumbers)
				{
					// Rough bounds: font size 32 => around 40-60 px height; width depends on digits.
					int w = Math.Max(80, d.Text.Length * 28);
					int h = 60;
					var rr = new Rectangle((int)d.Position.X - 10, (int)d.Position.Y - 10, w + 40, h + 40);
					r = (r == Rectangle.Empty) ? rr : Rectangle.Union(r, rr);
				}
			}

			if (r != Rectangle.Empty)
				Invalidate(Rectangle.Inflate(r, 10, 10));
		}

		private void EnsureDiceMastersInitialized()
		{
			// Lazy-init (safe to call from OnPaint)
			if (_diceMasters[1] != null && _multiplyMaster != null) return;

			for (int i = 1; i <= 6; i++)
			{
				_diceMasters[i] = AssetCache.GetMasterBitmap(AssetPaths.Dice($"die_{i}.png"));
			}
			_multiplyMaster = AssetCache.GetMasterBitmap(AssetPaths.Dice("multiply.png"));
		}

		private void InvalidateDiceRegion()
		{
			if (_diceCurrentPositions == null)
			{
				Invalidate();
				return;
			}

			// Extra padding accounts for rotation.
			int pad = 40;
			Rectangle r = Rectangle.Empty;

			Rectangle RectFor(PointF p, Control c) =>
				Rectangle.Inflate(new Rectangle((int)p.X, (int)p.Y, c.Width, c.Height), pad, pad);

			r = Rectangle.Union(r, RectFor(_diceCurrentPositions[0], _die1));
			r = Rectangle.Union(r, RectFor(_diceCurrentPositions[1], _die2));
			r = Rectangle.Union(r, RectFor(_diceCurrentPositions[2], _picMultiply));

			Invalidate(r);
		}

		private void EnsureBiomesInitialized()
		{
			if (_biomeManager != null) return;

			// Hardcoded list for now 
			// Use whatever files you actually have in your assets folder.
			var biomes = new List<BiomeDefinition>
	{
		new BiomeDefinition
		{
			Id = "meadow_01",
			BackgroundPath = AssetPaths.Background("meadow_01.png"),
			Knight = new Anchor { X = 0.18f, Y = 0.74f, Scale = 1.0f },
			MonsterSlots = { new Anchor { X = 0.78f, Y = 0.74f, Scale = 1.0f } }
		},
		new BiomeDefinition
		{
			Id = "forest_01",
			BackgroundPath = AssetPaths.Background("forest_01.png"),
			Knight = new Anchor { X = 0.18f, Y = 0.76f, Scale = 1.0f },
			MonsterSlots = { new Anchor { X = 0.78f, Y = 0.76f, Scale = 1.0f } }
		},
		 new BiomeDefinition
		{
			Id = "cave_01",
			BackgroundPath = AssetPaths.Background("cave_01.png"),
			Knight = new Anchor { X = 0.18f, Y = 0.76f, Scale = 1.0f },
			MonsterSlots = { new Anchor { X = 0.78f, Y = 0.76f, Scale = 1.0f } }
		},
		new BiomeDefinition
		{
			Id = "castle_01",
			BackgroundPath = AssetPaths.Background("castle_01.png"),
			Knight = new Anchor { X = 0.20f, Y = 0.75f, Scale = 1.0f },
			MonsterSlots = { new Anchor { X = 0.76f, Y = 0.75f, Scale = 1.0f } }
		},
	};

			_biomeManager = new BiomeManager(biomes);

			// Optional: preload the backgrounds so the first switch doesn’t hitch
			AssetCache.Preload(
				biomes[0].BackgroundPath,
				biomes[1].BackgroundPath,
				biomes[2].BackgroundPath
			);
		}

		private void ApplyBiomeForCurrentLevel()
		{
			EnsureBiomesInitialized();
			if (_biomeManager == null) return;

			int level = _settings.PlayerProgress?.Level ?? 1;
			_biomeManager.SetForLevel(level, levelsPerBiome: 3);

			var biome = _biomeManager.GetCurrent();
			if (biome.Id == _currentBiomeId) return;

			_currentBiomeId = biome.Id;

			// Set background using your RAM cache
			this.BackgroundImage?.Dispose();
			this.BackgroundImage = AssetCache.GetImageClone(biome.BackgroundPath);
			this.BackgroundImageLayout = ImageLayout.Stretch;

			// Positioning comes in Step 2 (we will NOT move sprites yet)
			Invalidate();
		}


		private void TickFloatingDamage()
		{
			bool anyLeft = false;
			lock (_damageNumbers)
			{
				for (int i = _damageNumbers.Count - 1; i >= 0; i--)
				{
					var d = _damageNumbers[i];

					d.Position = new PointF(d.Position.X, d.Position.Y + d.VelocityY);
					d.Opacity -= 0.04f;

					if (d.Opacity <= 0f)
					{
						_damageNumbers.RemoveAt(i);
					}
					else
					{
						anyLeft = true;
					}
				}
			}

			if (!anyLeft)
				_damageTimer.Stop();

			InvalidateDamageRegion();
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			_ = UpdateMyApp();
			ApplyBiomeForCurrentLevel();

			// Preload core assets to avoid first-use stutter
			AssetCache.Preload(
				AssetPaths.Background("meadow_01.png"),
				AssetPaths.Dice("multiply.png"),
				AssetPaths.Dice("die_1.png"),
				AssetPaths.Dice("die_2.png"),
				AssetPaths.Dice("die_3.png"),
				AssetPaths.Dice("die_4.png"),
				AssetPaths.Dice("die_5.png"),
				AssetPaths.Dice("die_6.png"),
				AssetPaths.Dice("die_7.png"),
				AssetPaths.Dice("die_8.png"),
				AssetPaths.Dice("die_9.png"),
				AssetPaths.Dice("die_10.png")
			);


			SpawnMonster();
			UpdatePlayerStats();
			LayoutCombat();

			// Warm caches so first animation frame does not hitch.
			EnsureDiceMastersInitialized();
			EnsureBarsLayerUpToDate();

			Refresh();
			GenerateQuestion();
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

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_barsLayerBitmap?.Dispose();
				_damageFont.Dispose();
				// _barTextFont is static and lives for the process lifetime.
			}

			base.Dispose(disposing);
		}
	}

	public class FloatingDamage
	{
		public string Text { get; set; } = "";
		public PointF Position { get; set; }
		public float Opacity { get; set; } = 1.0f;
		public Color Color { get; set; }

		public float VelocityY { get; set; } = -2.0f;
	}
}
