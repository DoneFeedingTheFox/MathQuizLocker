using System;
using System.Drawing;
using System.Windows.Forms;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
    public partial class QuizForm : Form
    {
        private readonly AppSettings _settings;
        private readonly QuizEngine _quizEngine;
        private readonly GameSessionManager _session;

        // Combat & State
        private int _a, _b;
      
        private bool _isAnimating = false, _isInternalClose = false;
        private string _currentMonsterName = "slime";

        // Animation logic
        private Point _knightOriginalPos;
        private System.Windows.Forms.Timer _animationTimer = new();

        // Loot & Progression
        private bool _awaitingChestOpen = false;
        private int _pendingKnightStage = -1;
        private int _equippedKnightStage = -1;
        private string? _pendingLootItemFile;

        private int _chestShakeTicks = 0;
        private bool _isChestOpening = false;

        private Random _rng = new Random();
        private PointF[] _diceVelocities = new PointF[3];
        private PointF[] _diceCurrentPositions = new PointF[3];
        private float[] _diceRotationAngles = new float[3];
        private int _scrambleTicks = 0;

        private List<FloatingText> _damageNumbers = new List<FloatingText>();

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }

        public QuizForm(AppSettings settings)
        {
            _settings = settings;
            _quizEngine = new QuizEngine(_settings);
            _session = new GameSessionManager(_settings, _quizEngine);

            // 1. MUST BE FIRST: Create the actual control objects
            InitializeCombatUi();

            // 2. Set initial non-UI values
            _equippedKnightStage = _settings.PlayerProgress.EquippedKnightStage >= 0
                ? _settings.PlayerProgress.EquippedKnightStage : 0;
        }



        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Now all controls like _lblLevel and _die1 are guaranteed to be non-null
            ApplyBiomeForCurrentLevel();
            SpawnMonster();
            UpdatePlayerHud();
            SetKnightIdleSprite();

            LayoutCombat();
            GenerateQuestion(); 

            this.Invalidate();
        }

        private void SetKnightIdleSprite()
        {
            string path = AssetPaths.KnightSprite(_equippedKnightStage);
            _picKnight.Image = AssetCache.GetImageClone(path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 1. Draw Sprites (Knight and Monster)
            if (_picKnight?.Image != null) g.DrawImage(_picKnight.Image, GetPaddedBounds(_picKnight.Image, _picKnight.Bounds));
            if (_picMonster?.Image != null) g.DrawImage(_picMonster.Image, GetPaddedBounds(_picMonster.Image, _picMonster.Bounds));

            // 2. Draw Health Bars
            // FIX: Pull player health from _session
            DrawHealthBar(g, _picKnight.Bounds, _session.CurrentPlayerHealth, 100, Color.LimeGreen);
            DrawHealthBar(g, _picMonster.Bounds, _session.CurrentMonsterHealth, _session.MaxMonsterHealth, Color.Red);

            // 3. Draw Dice Physics OR Static UI
            if (_isAnimating)
            {
                for (int i = 0; i < 3; i++)
                {
                    Image? img = (i == 0) ? _die1.Image : (i == 1) ? _die2.Image : _picMultiply.Image;
                    if (img == null) continue;

                    var state = g.Save();
                    float size = 100f * (this.ClientSize.Height / 1080f);
                    g.TranslateTransform(_diceCurrentPositions[i].X + size / 2, _diceCurrentPositions[i].Y + size / 2);
                    g.RotateTransform(_diceRotationAngles[i]);
                    g.DrawImage(img, -size / 2, -size / 2, size, size);
                    g.Restore(state);
                }
            }
            else if (_die1?.Visible == true)
            {
                if (_die1.Image != null) g.DrawImage(_die1.Image, _die1.Bounds);
                if (_die2.Image != null) g.DrawImage(_die2.Image, _die2.Bounds);
                if (_picMultiply?.Image != null) g.DrawImage(_picMultiply.Image, _picMultiply.Bounds);
            }

            // 4. Draw Loot and Chest
            if (_picChest?.Visible == true && _picChest.Image != null)
                g.DrawImage(_picChest.Image, _picChest.Bounds);

            if (_picLoot?.Visible == true && _picLoot.Image != null)
                g.DrawImage(_picLoot.Image, _picLoot.Bounds);

            // 5. Draw Floating Damage
            for (int i = _damageNumbers.Count - 1; i >= 0; i--)
            {
                var ft = _damageNumbers[i];
                using (Brush b = new SolidBrush(Color.FromArgb((int)(ft.Opacity * 255), ft.TextColor)))
                {
                    g.DrawString(ft.Text, new Font("Segoe UI", 28, FontStyle.Bold), b, ft.Position);
                }
                ft.Position.Y -= 2;
                ft.Opacity -= 0.02f;
                if (ft.Opacity <= 0) _damageNumbers.RemoveAt(i);
            }

            // 6. Draw Game Over Overlay 
            if (_session != null && _session.CurrentPlayerHealth <= 0)
            {
                // Ensure this ONLY runs when the player is actually defeated
                using (Brush overlayBrush = new SolidBrush(Color.FromArgb(180, 40, 0, 0)))
                {
                    g.FillRectangle(overlayBrush, this.ClientRectangle);
                }
            }
        }


        private void BtnSubmit_Click(object? sender, EventArgs e)
        {
            if (_isAnimating || !int.TryParse(_txtAnswer.Text, out int ans)) return;
            var result = _session.ProcessAnswer(ans, _a, _b);

            if (result.IsCorrect)
            {
               
                AnimateMeleeStrike(ans);
            }
            else
            {
                int dmg = _a * _b;
              
                AnimateMonsterAttack(dmg);
            }
        }
    }
}