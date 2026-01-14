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
                CreateParams cp = base.CreateParams;
                cp.Style &= ~0x00080000;    // WS_SYSMENU
                cp.ExStyle |= 0x02000000;   // WS_EX_COMPOSITED (Reduces flicker)
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); // Draws background
            var g = e.Graphics;

            // Set high-quality rendering for both dice and text
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // 1. Draw Bars (same surface as damage numbers => correct blending)
            DrawBars(g);

            // 2. Draw Animating Dice
            if (_isDiceAnimating && _diceCurrentPositions != null)
            {
                Control[] diceControls = { _die1, _die2, _picMultiply };
                for (int i = 0; i < 3; i++)
                {
                    string faceName = (i == 2) ? "multiply" : $"die_{_rng.Next(1, 7)}";
                    string path = AssetPaths.Dice($"{faceName}.png");

                    var master = AssetCache.GetMasterBitmap(path);
                    if (master != null)
                    {
                        var ctrl = diceControls[i];
                        var state = g.Save();

                        g.TranslateTransform(_diceCurrentPositions[i].X + ctrl.Width / 2f, _diceCurrentPositions[i].Y + ctrl.Height / 2f);
                        g.RotateTransform(_diceRotationAngles[i]);
                        g.DrawImage(master, -ctrl.Width / 2f, -ctrl.Height / 2f, ctrl.Width, ctrl.Height);

                        g.Restore(state);
                    }
                }
            }

        

            // 3. Draw Floating Damage Numbers
            lock (_damageNumbers)
            {
                for (int i = _damageNumbers.Count - 1; i >= 0; i--)
                {
                    var num = _damageNumbers[i];

                    // Calculate alpha for fade-out
                    int alpha = (int)(num.Opacity * 255);
                    int shadowAlpha = (int)(num.Opacity * 150);

                    // Use a specific font size for damage numbers
                    using (var font = new Font("Segoe UI", 32, FontStyle.Bold))
                    using (var mainBrush = new SolidBrush(Color.FromArgb(alpha, num.Color)))
                    using (var shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha, Color.Black)))
                    {
                        // Draw shadow first (offset by 2 pixels) to make text pop against background
                        g.DrawString(num.Text, font, shadowBrush, num.Position.X + 2, num.Position.Y + 2);

                        // Draw main colored text
                        g.DrawString(num.Text, font, mainBrush, num.Position);
                    }
                }
            }
        }

        private void DrawBars(Graphics g)
        {
            // Defensive: layout may not have run yet.
            if (_playerHealthBarRect.Width <= 0 || _monsterHealthBarRect.Width <= 0) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Player/Monster health
            DrawFantasyBar(g, _playerHealthBarRect, _playerHealth, _maxPlayerHealth, showText: false);
            DrawFantasyBar(g, _monsterHealthBarRect, Math.Max(0, _monsterHealth), Math.Max(1, _maxMonsterHealth), showText: false);

            // XP bar
            if (_playerXpBarRect.Width > 0)
            {
                var p = _settings.PlayerProgress;
                int nextLevelXp = XpSystem.GetXpRequiredForNextLevel(p.Level);
                DrawFantasyBar(g, _playerXpBarRect, Math.Min(p.CurrentXp, nextLevelXp), nextLevelXp, showText: true);
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
                    new Font("Segoe UI", 9f, FontStyle.Bold),
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

            Invalidate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _ = UpdateMyApp();

            // Preload core assets to avoid first-use stutter
            AssetCache.Preload(
                AssetPaths.Background("Background.png"),
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
