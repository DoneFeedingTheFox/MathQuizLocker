using System;
using System.Drawing;
using System.IO;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
    public partial class QuizForm
    {
        private void ApplyBiomeForCurrentLevel()
        {
            // 1. Clean up old background to save memory
            var oldBg = this.BackgroundImage;
            this.BackgroundImage = null;
            oldBg?.Dispose();

            // 2. Handle Story/Scroll Background
            if (_isShowingStory)
            {
                string scrollPath = AssetPaths.Background("scroll_bg.png");
                var scrollImg = AssetCache.GetImageClone(scrollPath);
                if (scrollImg != null)
                {
                    this.BackgroundImage = scrollImg;
                    this.BackgroundImageLayout = ImageLayout.Stretch;
                }
                return;
            }

            // 3. Determine base name based on level
            int level = _settings.PlayerProgress.Level;
            string baseName = (level > 4) ? "castle_01" :
                              (level > 3) ? "cave_01" :
                              (level > 2) ? "forest_01" :
                              (level > 1) ? "swamp_01" : "meadow_01";

            // 4. Check for Boss Suffix
            // Ensure SpawnMonster() was called before this method!
            if (_currentMonsterName != null && _currentMonsterName.Contains("_boss"))
            {
                baseName += "_boss";
            }

            // 5. Build the final filename with the extension
            string finalFileName = baseName + ".png";

            // 6. Generate the full path using your helper
            string fullPath = AssetPaths.Background(finalFileName);

            // 7. Load the image from the cache
            var newImg = AssetCache.GetImageClone(fullPath);

            if (newImg != null)
            {
                this.BackgroundImage = newImg;
                this.BackgroundImageLayout = ImageLayout.Stretch;
            }
            else
            {
                // Debugging: This will show in your Visual Studio Output window
                System.Diagnostics.Debug.WriteLine($"FAILED TO LOAD BACKGROUND: {fullPath}");
            }
        }

        private void ShowLootDrop()
        {
            int currentLevel = _settings.PlayerProgress.Level;

            // Level 2 player gets item_1 (the reward for reaching lvl 2)
            _pendingLootItemFile = $"item_{currentLevel}.png";
            _pendingKnightStage = currentLevel;

            _picChest.Image = AssetCache.GetImageClone(AssetPaths.Items("chest_01.png"));
            _picChest.Visible = true;
            _picLoot.Visible = false;

            _picChest.Location = new Point(_picMonster.Left + (_picMonster.Width / 4), _picMonster.Bottom - _picChest.Height);

            AnimateChestOpening();
        }




        private void ResetProgress()
        {
            var confirm = MessageBox.Show("Reset all progress for testing?", "Debug Reset", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.Yes)
            {
				// 1. Wipe the progress object
				_settings.ResetProgress();

				// 2. Re-initialize the quiz engine for level 1
				_quizEngine.InitializeForCurrentLevel();

				// 3. Restart the application
				Application.Restart();

				// 4. ESSENTIAL: Close this form and the environment to allow the restart to happen
				_isInternalClose = true; // Use your flag to skip any 'Are you sure?' prompts
				Environment.Exit(0);
			}
        }

		private void AnimateChestOpening()
		{
			_isChestOpening = true;
			_chestShakeTicks = 0;
            _btnContinue.Visible = false;
            _btnExit.Visible = false;

            int currentLevel = _settings.PlayerProgress.Level;
			_pendingLootItemFile = $"item_{currentLevel}.png";
			_pendingKnightStage = currentLevel;

			_animationTimer.Stop();
			_animationTimer.Tick -= ChestTickHandler;

			_animationTimer.Interval = 30;
			_animationTimer.Tick += ChestTickHandler;
			_animationTimer.Start();

			void ChestTickHandler(object? s, EventArgs e)
			{
				_chestShakeTicks++;

				if (_chestShakeTicks < 20)
				{
					_picChest.Left += _rng.Next(-5, 6);
				}
				else if (_chestShakeTicks == 20)
				{
					_picChest.Image = AssetCache.GetImageClone(AssetPaths.Items("chest_open_01.png"));

					var lootImg = AssetCache.GetImageClone(AssetPaths.Items(_pendingLootItemFile));
                    if (lootImg != null)
                    {
                        _picLoot.Image = lootImg;

                        // FIX: Explicitly set size and transparency
                        float scale = this.ClientSize.Height / 1080f;
                        _picLoot.Size = new Size((int)(120 * scale), (int)(120 * scale));
                        _picLoot.BackColor = Color.Transparent;

                        // Position it on the gold pile (right side of center)
                        int lootX = _picChest.Left + (_picChest.Width / 2) + 5;
                        int lootY = _picChest.Top + (_picChest.Height / 4); // Lifted slightly higher

                        _picLoot.Location = new Point(lootX, lootY);
                        _picLoot.Visible = true;
                        _picLoot.BringToFront();
                    }

                    // 4. Evolution: Update Knight to next stage
                    _equippedKnightStage = _pendingKnightStage;
                    _settings.PlayerProgress.EquippedKnightStage = _equippedKnightStage;
                    SetKnightIdleSprite();

                    // 5. Show the Victory Buttons
                    _btnContinue.Visible = true;
                    _btnExit.Visible = false;
                    _btnContinue.Focus();
                }
                else if (_chestShakeTicks > 50)
                {
                    _animationTimer.Stop();
					_animationTimer.Tick -= ChestTickHandler;
					_isChestOpening = false;
                }

                this.Invalidate();
            }
        
        }

        private Rectangle GetPaddedBounds(Image img, Rectangle target)
        {
            if (img == null) return target;

            float ratio = Math.Min((float)target.Width / img.Width, (float)target.Height / img.Height);
            int newWidth = (int)(img.Width * ratio);
            int newHeight = (int)(img.Height * ratio);

            int x = target.X + (target.Width - newWidth) / 2;
            int y = target.Y + (target.Height - newHeight) / 2;

            return new Rectangle(x, y, newWidth, newHeight);
        }
       

        

        private void UpdatePlayerHud()
        {
       
            if (_lblLevel == null || _lblXpStatus == null || _settings?.PlayerProgress == null)
                return;

            var p = _settings.PlayerProgress;
            int nextLevelXp = XpSystem.GetXpRequiredForNextLevel(p.Level);

            _lblLevel.Text = $"KNIGHT LEVEL: {p.Level}";
            _lblXpStatus.Text = $"XP: {p.CurrentXp} / {nextLevelXp}";
            this.Invalidate();
        }

        private void SpawnMonster()
        {
            int tier = Math.Max(1, _settings.MaxFactorUnlocked);
            int currentLevel = _settings.PlayerProgress.Level;
            int requiredXp = XpSystem.GetXpRequiredForNextLevel(currentLevel);

            _currentMonsterName = tier < 3 ? "goblin" : tier < 5 ? "skeleton" : tier < 6 ? "slime" : tier < 7 ? "orc" : "dragon";

            int estimatedReward = (int)(requiredXp * 0.6);
            if (_settings.PlayerProgress.CurrentXp + estimatedReward >= requiredXp)
            {
                _currentMonsterName += "_boss"; 
            }
            UpdateMonsterSprite("idle");
        }

        private void UpdateMonsterSprite(string state)
        {
            string suffix = state == "idle" ? "" : $"_{state}";
            string path = AssetPaths.Monsters($"{_currentMonsterName}{suffix}.png");

            var img = AssetCache.GetImageClone(path);
            if (img != null)
            {
                _picMonster.Image?.Dispose();
                _picMonster.Image = img;
            }
        }



		private void GenerateQuestion()
		{
			// 1. CLEAR PREVIOUS DATA
			_txtAnswer.Clear();
			_txtAnswer.Enabled = true;
			_btnSubmit.Enabled = true;

			_secondsRemaining = 10;
			if (_lblTimer != null)
			{
				_lblTimer.Text = "10";
				_lblTimer.Visible = false;
			}

			// 2. UNIFIED LOGIC
			// Determine if it's a boss
			bool isBoss = _currentMonsterName.ToLower().Contains("boss");

			// CRITICAL: Always get the question from the engine.
			// This ensures _a and _b match the engine's internal '_currentFact'.
			var q = _quizEngine.GetNextQuestion(isBoss);
			_a = q.a;
			_b = q.b;

			// 3. REFRESH VISUALS
			_die1.Image = AssetCache.GetImageClone(AssetPaths.Dice($"die_{_a}.png"));
			_die2.Image = AssetCache.GetImageClone(AssetPaths.Dice($"die_{_b}.png"));
			_picMultiply.Image = AssetCache.GetImageClone(AssetPaths.Dice("multiply.png"));

			_die1.Visible = true;
			_die2.Visible = true;
			_picMultiply.Visible = true;

			// 4. TRIGGER ANIMATION
			AnimateDiceRoll();

			_txtAnswer.Focus();
		}
		private void DrawHealthBar(Graphics g, Rectangle bounds, int current, int max, Color color)
        {
            int barWidth = (int)(bounds.Width * 0.8);
            int barHeight = 12;
            int x = bounds.X + (bounds.Width - barWidth) / 2;
            int y = bounds.Y - 30; // Float slightly above sprite

            g.FillRectangle(Brushes.DimGray, x, y, barWidth, barHeight);
            float percent = Math.Max(0, (float)current / max);
            g.FillRectangle(new SolidBrush(color), x, y, barWidth * percent, barHeight);
            g.DrawRectangle(Pens.Black, x, y, barWidth, barHeight);
        }

        public void ShowDamage(int amount, Point pos, Color color)
        {
            _damageNumbers.Add(new FloatingText
            {
              
                Position = new PointF(pos.X + 50, pos.Y),
                TextColor = color,
                Opacity = 1.0f
            });
        }

        public class FloatingText
        {
            public string Text;
            public PointF Position;
            public float Opacity = 1.0f;
            public Color TextColor;
            public float VelocityY = -2.0f;
        }

        private void AnimateDiceRoll()
        {
			// 1. Hide the static dice controls during animation
			_isDicePhysicsActive = true;
            _isAnimating = true;
            _scrambleTicks = 0; // Ensure this is 0 so the "ticks > 20" check doesn't trip early

            _animationTimer.Stop();
            _animationTimer.Interval = 15;

            // 2. Re-calculate positions to ensure they are relative to current screen size
            float centerX = this.ClientSize.Width / 2f;
            float spacing = 180f;
            float floorY = this.ClientSize.Height * 0.15f;

            float[] targetX = { centerX - spacing, centerX + spacing, centerX };

            // Start them higher up to ensure they have "falling" momentum
            _diceCurrentPositions = new PointF[] {
        new PointF(targetX[0], -200),
        new PointF(targetX[1], -250),
        new PointF(targetX[2], -220)
    };

            for (int i = 0; i < 3; i++)
            {
                // Give them a strong initial downward push
                _diceVelocities[i] = new PointF(0, _rng.Next(25, 45));
                _diceRotationAngles[i] = _rng.Next(0, 360);
            }

            _animationTimer.Tick -= DiceTickHandler;
            _animationTimer.Tick += DiceTickHandler;
            _animationTimer.Start();

            void DiceTickHandler(object? s, EventArgs e)
            {
                _scrambleTicks++;
                bool isAnyDieMoving = false; // Use a clearer flag

                for (int i = 0; i < 3; i++)
                {
                    _diceVelocities[i].Y += 3.5f; // Gravity
                    _diceCurrentPositions[i].Y += _diceVelocities[i].Y;

                    if (_diceCurrentPositions[i].Y > floorY)
                    {
                        _diceCurrentPositions[i].Y = floorY;
                        if (Math.Abs(_diceVelocities[i].Y) > 5.0f)
                        {
                            _diceVelocities[i].Y *= -0.3f; // Bounce
                        }
                        else
                        {
                            _diceVelocities[i].Y = 0;
                        }
                    }

                    // If a die is still above floor or has velocity, it's moving
                    if (_diceCurrentPositions[i].Y < floorY || Math.Abs(_diceVelocities[i].Y) > 0.1f)
                    {
                        _diceRotationAngles[i] += 25f;
                        isAnyDieMoving = true;
                    }
                }

                this.Invalidate(GetDiceArea());

                // FIX: Ensure they have at least 20 ticks of air-time before settling
                if (!isAnyDieMoving && _scrambleTicks > 20)
                {
                    _animationTimer.Stop();
                    _animationTimer.Tick -= DiceTickHandler;
                    _isDicePhysicsActive = false;
                    _isAnimating = false;
                    FinalizeDiceLand();
                }
            }
        }

        private void FinalizeDiceLand()
        {
            // 1. Re-calculate the positions for the static controls
            LayoutCombat();

            // 2. IMPORTANT: Make them visible again so OnPaint draws them
            _die1.Visible = true;
            _die2.Visible = true;
            _picMultiply.Visible = true;

            // 3. Ensure the images match the current question
            _die1.Image = AssetCache.GetImageClone(AssetPaths.Dice($"die_{_a}.png"));
            _die2.Image = AssetCache.GetImageClone(AssetPaths.Dice($"die_{_b}.png"));
            _picMultiply.Image = AssetCache.GetImageClone(AssetPaths.Dice("multiply.png"));

			// 4. RESET AND START THE TIMER
			// Stop any existing timer to prevent double-ticking
			_countdownTimer.Stop();

			// Reset the internal counter to a full 10 seconds
			_secondsRemaining = 10;

			// 5. Restart the timer and enable input
			_lblTimer.Text = "10";
			_lblTimer.Visible = true;
			_txtAnswer.Enabled = true;
			_btnSubmit.Enabled = true;
	

			_txtAnswer.Focus();

			_countdownTimer.Start();


			this.Invalidate();
        }
    }
}