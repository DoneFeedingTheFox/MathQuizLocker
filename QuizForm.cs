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
		private static readonly Font _barTextFont = new Font("SegoeSegoe UI", 9f, FontStyle.Bold);

		// Dice master bitmaps: avoid per-frame string/path churn + cache lookups.
		private readonly Bitmap?[] _diceMasters = new Bitmap?[7]; // 1..6
		private Bitmap? _multiplyMaster;

		// Bars layer: render expensive gradients/paths only when values/layout change.
		private Bitmap? _barsLayerBitmap;
		private Rectangle _barsLayerBounds;
		private BarsSnapshot _barsSnapshot;

		// Loot / post-fight upgrade flow (do NOT equip visuals mid-fight)
		private bool _awaitingChestOpen = false;
		private int _pendingKnightStage = -1;   // stage earned by level-up
		private int _preVictoryKnightStage = -1;

		// Knight visuals (equipment) — what the player currently "wears" (persisted!)
		private int _equippedKnightStage = -1;

		// Loot layout tuning
		private float _lootScale = 0.85f;
		private PointF _lootChestOffset = new PointF(0.10f, 0.72f);
		private PointF _lootItemOffset = new PointF(0.26f, 0.66f);

		// Base sizes (at 1080p scale=1.0)
		private readonly Size _lootChestBaseSize = new Size(220, 220);
		private readonly Size _lootItemBaseSize = new Size(140, 140);

		// Provided elsewhere in your project (used by ShowLootDrop)
	

		private readonly System.Windows.Forms.Timer _damageTimer = new System.Windows.Forms.Timer();

		public QuizForm(AppSettings settings)
		{
			_settings = settings ?? new AppSettings();
			_quizEngine = new QuizEngine(_settings);
			_session = new GameSessionManager(_settings, _quizEngine);
			_playerHealth = _maxPlayerHealth;

			// Safety: never allow level 0
			if (_settings.PlayerProgress.Level < 1)
				_settings.PlayerProgress.Level = 1;

			// Pull equipped stage from persisted settings
			LoadEquippedKnightStageFromSettings();

			_damageTimer.Interval = 16; // ~60 FPS
			_damageTimer.Tick += (s, e) => TickFloatingDamage();

			InitializeCombatUi();
		}

		private void LoadEquippedKnightStageFromSettings()
		{
			// Requires: PlayerProgress.EquippedKnightStage property exists.
			// If it's not set yet (-1), we derive from current level once.
			int persisted = _settings.PlayerProgress.EquippedKnightStage;

			if (persisted >= 0)
			{
				_equippedKnightStage = persisted;
				return;
			}

			_equippedKnightStage = KnightProgression.GetKnightStageIndex(_settings.PlayerProgress.Level);
			_settings.PlayerProgress.EquippedKnightStage = _equippedKnightStage;
			AppSettings.Save(_settings);
		}

		private void PersistEquippedKnightStage()
		{
			_settings.PlayerProgress.EquippedKnightStage = _equippedKnightStage;
			AppSettings.Save(_settings);
		}

		protected override CreateParams CreateParams
		{
			get
			{
				var cp = base.CreateParams;
				cp.Style &= ~0x00080000;    // WS_SYSMENU
				cp.ExStyle |= 0x02000000;
				return cp;
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			var g = e.Graphics;

			// 1. Scene Setup - High quality for sprites, but no smoothing for pixel-perfect layout
			g.SmoothingMode = SmoothingMode.None;
			g.InterpolationMode = InterpolationMode.NearestNeighbor;
			g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;

			// 2. Draw Knight and Monster (Proportionally)
			if (_picKnight.Image != null)
			{
				Rectangle rect = GetPaddedBounds(_picKnight.Image, _picKnight.Bounds);
				g.DrawImage(_picKnight.Image, rect);
			}

			if (_picMonster.Image != null)
			{
				Rectangle rect = GetPaddedBounds(_picMonster.Image, _picMonster.Bounds);
				g.DrawImage(_picMonster.Image, rect);
			}

			// 3. Draw Loot (Chest and Item) - Drawn after monster to ensure it sits on top
			if (_picChest.Visible)
			{
				var chestImg = AssetCache.GetMasterBitmap(AssetPaths.Items("chest_01.png"));
				if (chestImg != null)
				{
					Rectangle rect = GetPaddedBounds(chestImg, _picChest.Bounds);
					g.DrawImage(chestImg, rect);
				}
			}

			if (_picLoot.Visible && !string.IsNullOrEmpty(_pendingLootItemFile))
			{
				var lootImg = AssetCache.GetMasterBitmap(AssetPaths.Items(_pendingLootItemFile));
				if (lootImg != null)
				{
					Rectangle rect = GetPaddedBounds(lootImg, _picLoot.Bounds);
					g.DrawImage(lootImg, rect);
				}
			}

			// 4. Draw HUD / Health & XP Bars
			DrawBars(g);

			// 5. Draw Dice (Static or Animating)
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
			else if (_die1.Visible) // Draw static dice when not in the air
			{
				if (_die1.Image != null) g.DrawImage(_die1.Image, _die1.Bounds);
				if (_die2.Image != null) g.DrawImage(_die2.Image, _die2.Bounds);
				if (_picMultiply.Image != null) g.DrawImage(_picMultiply.Image, _picMultiply.Bounds);
			}

			// 6. Floating Damage Numbers (Anti-Aliased for smooth text)
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

			lock (_damageNumbers)
			{
				for (int i = _damageNumbers.Count - 1; i >= 0; i--)
				{
					var num = _damageNumbers[i];

					int alpha = (int)(num.Opacity * 255);
					int shadowAlpha = (int)(num.Opacity * 150);

					using (var mainBrush = new SolidBrush(Color.FromArgb(alpha, num.Color)))
					using (var shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha, Color.Black)))
					{
						g.DrawString(num.Text, _damageFont, shadowBrush, num.Position.X + 2, num.Position.Y + 2);
						g.DrawString(num.Text, _damageFont, mainBrush, num.Position);
					}
				}
			}
		}

		private void DrawBars(Graphics g)
		{
			if (_playerHealthBarRect.Width <= 0 || _monsterHealthBarRect.Width <= 0) return;

			g.SmoothingMode = SmoothingMode.AntiAlias;

			DrawFantasyBar(g, _playerHealthBarRect, _playerHealth, _maxPlayerHealth, showText: true);
			DrawFantasyBar(g, _monsterHealthBarRect, Math.Max(0, _monsterHealth), Math.Max(1, _maxMonsterHealth), showText: true);

			if (_playerXpBarRect.Width > 0)
			{
				var p = _settings.PlayerProgress;
				int nextLevelXp = XpSystem.GetXpRequiredForNextLevel(p.Level);
				DrawFantasyBar(g, _playerXpBarRect, Math.Min(p.CurrentXp, nextLevelXp), nextLevelXp, showText: true);
			}
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

		private void EnsureKnightEquippedStageInitialized()
		{
			// Always prefer persisted value.
			int persisted = _settings.PlayerProgress.EquippedKnightStage;

			if (persisted >= 0)
			{
				_equippedKnightStage = persisted;
				return;
			}

			// If nothing persisted yet, derive once from current level and persist it.
			if (_equippedKnightStage < 0)
				_equippedKnightStage = KnightProgression.GetKnightStageIndex(_settings.PlayerProgress.Level);

			_settings.PlayerProgress.EquippedKnightStage = _equippedKnightStage;
			AppSettings.Save(_settings);
		}

		private void SetKnightIdleSprite()
		{
			EnsureKnightEquippedStageInitialized();

			string path = AssetPaths.KnightSprite(_equippedKnightStage);
			var img = AssetCache.GetImageClone(path);
			if (img != null)
			{
				_picKnight.Image?.Dispose();
				_picKnight.Image = img;
			}
		}

		/// <summary>
		/// IMPORTANT BEHAVIOR:
		/// If loot is shown, the equipment is considered earned and should persist
		/// even if the player exits to desktop on the victory screen.
		/// </summary>
		private void CommitPendingLootIfAny()
		{
			if (!_awaitingChestOpen) return;
			if (_pendingKnightStage < 0) return;

			// Equip immediately and persist.
			_equippedKnightStage = _pendingKnightStage;
			PersistEquippedKnightStage();
		}

		private void ShowLootDrop()
		{
			// We no longer assign .Image to the PictureBox.
			// Instead, we just set the flags that OnPaint uses to decide what to draw.
			_picChest.Visible = true;
			_picLoot.Visible = true;

			// Ensure the filename is set so OnPaint knows which item sprite to grab
			if (string.IsNullOrEmpty(_pendingLootItemFile))
			{
				_pendingLootItemFile = "helmet_01.png";
			}

			// This is critical: It tells Windows to clear the Form and call OnPaint
			this.Invalidate();
		}

		private void HideLootDrop()
		{
			_picChest.Visible = false;
			_picLoot.Visible = false;

			// Refresh the screen to remove the drawn sprites
			this.Invalidate();
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
			if (_diceMasters[1] != null && _multiplyMaster != null) return;

			for (int i = 1; i <= 6; i++)
				_diceMasters[i] = AssetCache.GetMasterBitmap(AssetPaths.Dice($"die_{i}.png"));

			_multiplyMaster = AssetCache.GetMasterBitmap(AssetPaths.Dice("multiply.png"));
		}

		private void InvalidateDiceRegion()
		{
			if (_diceCurrentPositions == null)
			{
				Invalidate();
				return;
			}

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

			this.BackgroundImage?.Dispose();
			this.BackgroundImage = AssetCache.GetImageClone(biome.BackgroundPath);
			this.BackgroundImageLayout = ImageLayout.Stretch;

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
						_damageNumbers.RemoveAt(i);
					else
						anyLeft = true;
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
			UpdatePlayerHud();
			LayoutCombat();

			// Restore correct knight visuals on startup (persisted)
			EnsureKnightEquippedStageInitialized();
			SetKnightIdleSprite();

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
			}

			base.Dispose(disposing);
		}

		// --------------------------------------------------------------------
		// IMPORTANT: Call this from your victory screen method in QuizForm.Ui.cs
		// --------------------------------------------------------------------
		// In ShowVictoryScreen(), AFTER you set _awaitingChestOpen and _pendingKnightStage:
		//
		// if (_awaitingChestOpen)
		// {
		//     CommitPendingLootIfAny();
		//     ShowLootDrop();
		// }
		//
		// This ensures: exiting on victory screen still keeps the item.
		// --------------------------------------------------------------------
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
