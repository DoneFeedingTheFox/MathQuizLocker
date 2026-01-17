using System;
using System.Drawing;
using System.Windows.Forms;
using MathQuizLocker.Services;


namespace MathQuizLocker
{
    public partial class QuizForm : Form
    {
		// Core Services
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
		private Random _rng = new Random();
        private PointF[] _diceVelocities = new PointF[3];
        private PointF[] _diceCurrentPositions = new PointF[3];
        private float[] _diceRotationAngles = new float[3];
        private int _scrambleTicks = 0;

        private List<FloatingText> _damageNumbers = new List<FloatingText>();

		// story mode
		private PictureBox _picScroll;
		private Label _lblStoryText;
		private Button _btnStoryContinue, _btnStoryExit;
		private bool _isShowingStory = false;

		// Enable double buffering to reduce flicker
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
			LocalizationService.LoadLanguage("en");
			InitStoryUi();

			// 2. Set initial non-UI values
			_equippedKnightStage = _settings.PlayerProgress.EquippedKnightStage > 0
	            ? _settings.PlayerProgress.EquippedKnightStage
	            : 1;
		}

		private void CloseStoryAndResume()
		{
			_isShowingStory = false;

			// 1. Hide Story UI
			_lblStoryText.Visible = false;
			_btnStoryContinue.Visible = false;
			_btnStoryExit.Visible = false;

			// 2. Restore Combat UI (This is the missing part!)
			_txtAnswer.Visible = true;
			_btnSubmit.Visible = true;

			// 3. Restore HUD
			_lblLevel.Visible = true;
			_lblXpStatus.Visible = true;

			// 4. Reset World
			ApplyBiomeForCurrentLevel();
			SetKnightIdleSprite();

			_session.StartNewBattle();
			SpawnMonster();
			GenerateQuestion(); // This will put new dice on the screen

			// 5. Final Touch: Focus the text box so the player can type immediately
			_txtAnswer.Focus();

			this.Invalidate();
		}

		private void InitStoryUi()
		{
			// 1. Setup Story Text Label
			_lblStoryText = new Label
			{
				Name = "_lblStoryText",
				ForeColor = Color.FromArgb(60, 40, 20), // Deep "Ink" brown
				TextAlign = ContentAlignment.MiddleCenter,
				BackColor = Color.Transparent,
				Font = new Font("Palatino Linotype", 18, FontStyle.Bold),
				Visible = false
			};

			// 2. Setup "Continue" Button
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

			// 3. Setup "Exit" Button
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
			_btnStoryExit.Click += (s, e) => {
				AppSettings.Save(_settings);
				Environment.Exit(0);
			};

			// 4. Add to Form
			this.Controls.Add(_lblStoryText);
			this.Controls.Add(_btnStoryContinue);
			this.Controls.Add(_btnStoryExit);
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
		
			if (_isShowingStory)
			{
				
				return;
			}
			// High-performance settings for your laptop
			e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
			e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
			e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;

			base.OnPaint(e);
			var g = e.Graphics;

			// Only use AntiAlias for things that really need it (like text or rotation)
			g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

			// 1. Draw Sprites (Knight and Monster)
			if (_picKnight?.Image != null) g.DrawImage(_picKnight.Image, GetPaddedBounds(_picKnight.Image, _picKnight.Bounds));
			if (_picMonster?.Image != null) g.DrawImage(_picMonster.Image, GetPaddedBounds(_picMonster.Image, _picMonster.Bounds));

			// 2. Draw Health Bars
			DrawHealthBar(g, _picKnight.Bounds, _session.CurrentPlayerHealth, 100, Color.LimeGreen);
			DrawHealthBar(g, _picMonster.Bounds, _session.CurrentMonsterHealth, _session.MaxMonsterHealth, Color.Red);

			// 3. Draw Dice Physics OR Static UI
			// We use the specific flag _isDicePhysicsActive so they don't show up during melee attacks
			if (_isDicePhysicsActive)
			{
				// Use 120f to match your LayoutCombat exactly
				float diceSize = 120f * (this.ClientSize.Height / 1080f);

				for (int i = 0; i < 3; i++)
				{
					Image? img = (i == 0) ? _die1.Image : (i == 1) ? _die2.Image : _picMultiply.Image;
					if (img == null) continue;

					var state = g.Save();
					// Center the rotation on the die
					g.TranslateTransform(_diceCurrentPositions[i].X + diceSize / 2, _diceCurrentPositions[i].Y + diceSize / 2);
					g.RotateTransform(_diceRotationAngles[i]);

					// Draw all 3 at the same size
					g.DrawImage(img, -diceSize / 2, -diceSize / 2, diceSize, diceSize);
					g.Restore(state);
				}
			}
			else if (_die1?.Visible == true)
			{
				// Draw the static version once they have landed
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
					g.DrawString(ft.Text, new Font("Segoe UI", 50, FontStyle.Bold), b, ft.Position);
				}
				// Update positions here so they float up smoothly
				ft.Position.Y += ft.VelocityY;
				ft.Opacity -= 0.008f;
				if (ft.Opacity <= 0) _damageNumbers.RemoveAt(i);
			}

			// 6. Draw Game Over Overlay 
			if (_session != null && _session.CurrentPlayerHealth <= 0)
			{
				using (Brush overlayBrush = new SolidBrush(Color.FromArgb(180, 40, 0, 0)))
				{
					g.FillRectangle(overlayBrush, this.ClientRectangle);
				}
			}
		}

		private void BtnSubmit_Click(object? sender, EventArgs e)
        {
            if (_isAnimating || !int.TryParse(_txtAnswer.Text, out int ans)) return;

            // HIDE DICE IMMEDIATELY
            _die1.Visible = false;
            _die2.Visible = false;
            _picMultiply.Visible = false;
            this.Invalidate();
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