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
            int level = _settings.PlayerProgress.Level;

           
            string bgName = "meadow_01.png";
            if (level > 4) bgName = "castle_01.png";
            else if (level > 3) bgName = "cave_01.png";
            else if (level > 2) bgName = "forest_01.png";
			else bgName = "meadow_01.png";

			this.BackgroundImage?.Dispose();
            this.BackgroundImage = AssetCache.GetImageClone(AssetPaths.Background(bgName));
            this.BackgroundImageLayout = ImageLayout.Stretch;
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
                _settings.PlayerProgress = new PlayerProgress();

                // 2. Pass _settings as the argument to the static Save method
                AppSettings.Save(_settings);

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

            int currentLevel = _settings.PlayerProgress.Level;

           
            _pendingLootItemFile = $"item_{currentLevel}.png";
            _pendingKnightStage = currentLevel;

            _animationTimer.Stop();
            _animationTimer = new System.Windows.Forms.Timer { Interval = 30 };
            _animationTimer.Tick += (s, e) =>
            {
                _chestShakeTicks++;

                // Phase 1: Shake the chest (Anticipation)
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
                    _btnExit.Visible = true;
                    _btnContinue.Focus();
                }
                else if (_chestShakeTicks > 50)
                {
                    _animationTimer.Stop();
                    _isChestOpening = false;
                }

                // Force the form to redraw the new positions
                this.Invalidate();
            };
            _animationTimer.Start();
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
    

            _currentMonsterName = tier < 4 ? "slime" : tier < 7 ? "orc" : "dragon";
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
            var q = _quizEngine.GetNextQuestion();
            _a = q.a;
            _b = q.b;

            // Load the images into the controls before the animation starts
            _die1.Image = AssetCache.GetImageClone(AssetPaths.Dice($"die_{_a}.png"));
            _die2.Image = AssetCache.GetImageClone(AssetPaths.Dice($"die_{_b}.png"));

            // Ensure this path matches your "multiply.png" filename in the Assets/Dice folder
            _picMultiply.Image = AssetCache.GetImageClone(AssetPaths.Dice("multiply.png"));
            AnimateDiceRoll();

            _txtAnswer.Clear();
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
            // 1. Enable specific physics flag so OnPaint draws the falling dice
            _isDicePhysicsActive = true;
            _isAnimating = true;
            _scrambleTicks = 0;

			_animationTimer.Stop();
			_animationTimer.Dispose(); // Kill the old timer
			_animationTimer = new System.Windows.Forms.Timer { Interval = 10 }; // Create a fresh one

			float centerX = this.ClientSize.Width / 2f;
            float spacing = 180f;
            float floorY = this.ClientSize.Height * 0.15f;

            // Target X lanes: Die 1 (Left), Die 2 (Right), Multiply (Middle)
            float[] targetX = { centerX - spacing, centerX + spacing, centerX };

            // Initialize positions exactly on their lanes to prevent flickering
            _diceCurrentPositions = new PointF[] {
        new PointF(targetX[0], -100),
        new PointF(targetX[1], -150),
        new PointF(targetX[2], -120)
    };

            for (int i = 0; i < 3; i++)
            {
                // Random fall speed
                _diceVelocities[i] = new PointF(0, _rng.Next(20, 39));
				_diceRotationAngles[i] += 25f;
			}

            _animationTimer.Stop();
			_animationTimer.Tick += (s, e) => {
				
                _scrambleTicks++;
                bool moving = false;

                for (int i = 0; i < 3; i++)
                {
                    // Apply Gravity
                    _diceVelocities[i].Y += 3.0f;
                    _diceCurrentPositions[i].Y += _diceVelocities[i].Y;

                    // Collision with floor
                    if (_diceCurrentPositions[i].Y > floorY)
                    {
                        _diceCurrentPositions[i].Y = floorY;

                        // Bounce logic: Reverse Y velocity and reduce it (dampening)
                        if (Math.Abs(_diceVelocities[i].Y) > 5.0f)
                        {
                            _diceVelocities[i].Y *= -0.3f; // 30% bounce back
                        }
                        else
                        {
                            _diceVelocities[i].Y = 0; // Settled
                        }
                    }

                    // Keep them rotationally spinning only while moving vertically
                    if (Math.Abs(_diceVelocities[i].Y) > 0.1f)
                    {
                        _diceRotationAngles[i] += 25f;
                        moving = true;
                    }

                    // Force X to stay at target to keep them in a neat central row
                    _diceCurrentPositions[i].X = targetX[i];
                }

                this.Invalidate();

                // End the animation once the dice stop moving and minimum time has passed
                if (!moving && _scrambleTicks > 20)
                {
                    _animationTimer.Stop();
					_animationTimer = new System.Windows.Forms.Timer { Interval = 10 };
					// 2. Disable physics flag so OnPaint switches to static PictureBox drawing
					_isDicePhysicsActive = false;
                    _isAnimating = false;

                    FinalizeDiceLand();
                }
            };
            _animationTimer.Start();
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

            // 4. Force a final redraw to show the static dice
            this.Invalidate();
        }
    }
}