using System;
using System.Drawing;
using System.IO;
using MathQuizLocker.Services;
using MathQuizLocker.Models;



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

            string baseName = level switch
            {
                1 => "meadow_01",
                2 => "swamp_01",
                3 => "forest_01",
                4 => "cave_01",
                _ => "castle_01"
            };

            // 4. Check for Boss Suffix
            // Ensure SpawnMonster() was called before this method!
            if (!string.IsNullOrEmpty(_currentMonsterName) &&
         _currentMonsterName.Contains("boss", StringComparison.OrdinalIgnoreCase))
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

                // 1. Shaking Phase
                if (_chestShakeTicks < 20)
                {
                    _picChest.Left += _rng.Next(-5, 6);
                }
                // 2. The Big Reveal (Exactly at Tick 20)
                else if (_chestShakeTicks == 20)
                {
                    // Change chest to open image
                    _picChest.Image = AssetCache.GetImageClone(AssetPaths.Items("chest_open_01.png"));

                    // --- EVOLUTION & PROGRESSION ---
                    // Update Knight to next stage visually
                    _equippedKnightStage = _pendingKnightStage;
                    _settings.PlayerProgress.EquippedKnightStage = _equippedKnightStage;
                    SetKnightIdleSprite();

                    // Trigger the large floating "LEVEL UP!" text over the knight
                    ShowDamage(0, _picKnight.Location, Color.Gold);

                    // --- LOOT IMAGE HANDLING ---
                    var lootImg = AssetCache.GetImageClone(AssetPaths.Items(_pendingLootItemFile));
                    if (lootImg != null)
                    {
                        _picLoot.Image = lootImg;

                        float scale = this.ClientSize.Height / 1080f;
                        _picLoot.Size = new Size((int)(120 * scale), (int)(120 * scale));
                        _picLoot.BackColor = Color.Transparent;

                        // Position it on the gold pile
                        int lootX = _picChest.Left + (_picChest.Width / 2) + 5;
                        int lootY = _picChest.Top + (_picChest.Height / 4);

                        _picLoot.Location = new Point(lootX, lootY);
                        _picLoot.Visible = true;
                        _picLoot.BringToFront();
                    }

                    // --- UI CLEANUP ---
                    _btnContinue.Visible = true;
                    _btnExit.Visible = false;
                    _btnContinue.Focus();
                }
                // 3. End Animation
                else if (_chestShakeTicks > 50)
                {
                    _animationTimer.Stop();
                    _animationTimer.Tick -= ChestTickHandler;
                    _isChestOpening = false;
                }

                this.Invalidate();
            }
        }


		private Rectangle GetPaddedBounds(Image img, Rectangle targetRect)
		{
		
			float imgRatio = (float)img.Width / img.Height;
			float targetRatio = (float)targetRect.Width / targetRect.Height;

			int drawWidth, drawHeight;
			int xOffset = 0;

			if (imgRatio > targetRatio)
			{
				drawWidth = targetRect.Width;
				drawHeight = (int)(targetRect.Width / imgRatio);
			}
			else
			{
				drawHeight = targetRect.Height;
				drawWidth = (int)(targetRect.Height * imgRatio);
				xOffset = (targetRect.Width - drawWidth) / 2; // Center horizontally
			}

			
			return new Rectangle(targetRect.X + xOffset, targetRect.Bottom - drawHeight, drawWidth, drawHeight);
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

		private bool CheckIfBossShouldSpawn()
		{
			int currentXp = _settings.PlayerProgress.CurrentXp;
			int currentLevel = _settings.PlayerProgress.Level;

			// Get the XP goal for the current level (e.g., 100 XP)
			int requiredXp = XpSystem.GetXpRequiredForNextLevel(currentLevel);

			// If I have 100/100 or 105/100, the next monster IS a boss.
			return currentXp >= requiredXp;
		}

        private void SpawnMonster()
        {
            int level = _settings.PlayerProgress.Level;
            bool isBoss = CheckIfBossShouldSpawn();

            var monsterConfig = _monsterService.GetMonsterByLevel(level, isBoss);
            _currentMonsterName = monsterConfig.Name;
         


            _session.StartNewBattle(monsterConfig);

            UpdateMonsterSprite("idle");
            ApplyBiomeForCurrentLevel();
            UpdatePlayerHud();

            _secondsRemaining = _session.CurrentMonsterAttackInterval;
            _lblTimer.Text = _secondsRemaining.ToString();
            _lblTimer.Visible = true;
            _lblTimer.BringToFront();
            _countdownTimer.Start();

            // If it's the start of a level (XP is 0), show a biome label
            if (_settings.PlayerProgress.CurrentXp == 0 && !_isShowingStory)
			{
				string biomeName = level switch
				{
					1 => "THE SUNNY MEADOWS",
					2 => "THE MURKY SWAMPS",
					3 => "THE WHISPERING WOODS",
					_ => "THE FORGOTTEN CASTLE"
				};

				_lblFeedback.Text = biomeName;
				_lblFeedback.Font = new Font("Palatino Linotype", 36, FontStyle.Bold);
				_lblFeedback.ForeColor = Color.Gold;
				_lblFeedback.AutoSize = true;

				// Use LayoutEngine to calculate centered position
				_lblFeedback.Location = new Point(
					(this.ClientSize.Width - _lblFeedback.PreferredWidth) / 2,
					(this.ClientSize.Height / 2) - 100
				);

				_lblFeedback.Visible = true;
				_lblFeedback.BringToFront();


                var hideTimer = new System.Windows.Forms.Timer { Interval = 2500 };
				hideTimer.Tick += (s, e) => {
					_lblFeedback.Visible = false;
					hideTimer.Stop();
				};
				hideTimer.Start();
			}
		}



		private void UpdateMonsterSprite(string state)
		{
			var config = _monsterService.GetMonster(_currentMonsterName);
			string suffix = (state == "idle") ? "" : $"_{state}";

		
			string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
										   "Assets",
										   "Monsters", 
										   $"{config.Name}{suffix}.png");

			var img = AssetCache.GetImageClone(fullPath);
			if (img != null)
			{
				_picMonster.Image?.Dispose();
				_picMonster.Image = img;
				_picMonster.Visible = true;
				_picMonster.BringToFront();
			}
		}

        public Question GenerateRandomQuestion(int playerLevel)
        {

            // 1. Determine the range based on level (e.g., Level 3 = up to 4x10)
            int maxA = Math.Min(12, playerLevel + 1);
            int maxB = 10;

            // 2. Generate a fresh random pair every time
            // This ignores the 'Progress' dictionary entirely!
            int a = _random.Next(1, maxA + 1);
            int b = _random.Next(1, maxB + 1);

            return new Question(a, b);
        }

        private void GenerateQuestion()
		{
			// 1. CLEAR PREVIOUS DATA
			_txtAnswer.Clear();
			_txtAnswer.Enabled = true;
			_btnSubmit.Enabled = true;

					
			bool isBoss = _currentMonsterName.ToLower().Contains("boss");

            if (_isAnimating)
            {
                _isQuestionPending = true;
                return;
            }

            _isQuestionPending = false;

            // CRITICAL: Always get the question from the engine.
            // This ensures _a and _b match the engine's internal '_currentFact'.
            var q = _quizEngine.GetNextQuestion();
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

			string textToShow = amount == 0 ? "LEVEL UP!" : $"-{amount}";

			_damageNumbers.Add(new FloatingText
			{
				Text = textToShow,
				Position = new PointF(pos.X + (_picKnight.Width / 4), pos.Y - 50),
				TextColor = color,
				Opacity = 1.0f,
				
			});
		}

        public class FloatingText
        {
            public string Text { get; set; }
            public PointF Position;
            public Color TextColor { get; set; }
            public float Opacity { get; set; } = 1.0f;
            public float VelocityY; 
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

			
			_txtAnswer.Enabled = true;
			_btnSubmit.Enabled = true;
	

			_txtAnswer.Focus();

            this.Invalidate();
        }
    }
}