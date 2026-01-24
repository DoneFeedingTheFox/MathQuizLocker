using MathQuizLocker.Services;

namespace MathQuizLocker
{
    public partial class QuizForm
    {
        private System.Windows.Forms.Timer _playerAnimationTimer = new System.Windows.Forms.Timer { Interval = 30 };
        private System.Windows.Forms.Timer _monsterAnimationTimer = new System.Windows.Forms.Timer { Interval = 30 };

        private void AnimateMeleeStrike(int damage)
        {
			// 1. Initial Logic
			bool isDefeated = _session.ApplyDamage(damage, out int xpGained, out bool leveledUp);
			UpdatePlayerHud();
			ShowDamage(damage, _picMonster.Location, Color.Red);

			// 2. Prepare the Sprite and the RENDER RECTANGLE
			_picKnight.Image = AssetCache.GetImageClone(AssetPaths.KnightAttack(_equippedKnightStage));

			// Crucial: Update the rectangle to match the new Attack sprite dimensions
			_knightRenderRect = GetPaddedBounds(_picKnight.Image, _picKnight.Bounds);
			int originalX = _knightRenderRect.X;

			int distance = _monsterOriginalPos.X - _picKnight.Right;
			int step = 0;

			_playerAnimationTimer.Stop();
			_playerAnimationTimer.Tick -= MeleeTickHandler;

			void MeleeTickHandler(object? s, EventArgs e)
			{
				if (!this.IsHandleCreated || this.IsDisposed) return;
				step++;

				
				if (step <= 4)
				{
					_knightRenderRect.X += (distance / 4);
				}
				else if (step == 5)
				{
					UpdateMonsterSprite("hit");
				}
				else if (step <= 12)
				{
					_knightRenderRect.X -= (distance / 8);
				}
				else
				{
					_playerAnimationTimer.Stop();
					_playerAnimationTimer.Tick -= MeleeTickHandler;

					// Reset Sprite and Rectangle to Idle state
					SetKnightIdleSprite();
					_knightRenderRect = GetPaddedBounds(_picKnight.Image, _picKnight.Bounds);

					UpdateMonsterSprite("idle");

					if (_session.CurrentMonsterHealth <= 0)
					{
						_countdownTimer.Stop();
						_lblTimer.Visible = false;
						ShowVictoryScreen();
					}
					else GenerateQuestion();

					this.Invalidate(GetCombatZone());
					return;
				}

				// Redraw only the combat area to save CPU cycles
				this.Invalidate(GetCombatZone());
			}

			_playerAnimationTimer.Tick += MeleeTickHandler;
			_playerAnimationTimer.Start();
		}

		private void AnimateMonsterAttack(int damage)
        {

			if (_playerAnimationTimer.Enabled)
			{
				_isQuestionPending = true; // Use your existing flag to queue the event
				return;
			}
			if (!this.IsHandleCreated || this.IsDisposed) return;
            UpdateMonsterSprite("attack");
            bool isHeavyHit = damage >= 40;

            _session.ApplyPlayerDamage(damage);
            ShowDamage(damage, _picKnight.Location, isHeavyHit ? Color.DarkRed : Color.OrangeRed);

            _picKnight.Image = AssetCache.GetImageClone(AssetPaths.KnightHit(_equippedKnightStage));

            int step = 0;
            int originalMonsterX = _picMonster.Left;

            // 1. Reset the timer
            _monsterAnimationTimer.Stop();
            _monsterAnimationTimer.Tick -= MonsterTickHandler;

            void MonsterTickHandler(object? s, EventArgs e)
            {
                // 1. Safety Guard: Stop if form is closing or disposed
                if (!this.IsHandleCreated || this.IsDisposed) return;

                step++;
                int lungeSpeed = isHeavyHit ? 30 : 20;

				// Movement Logic
				if (step <= 5)
				{
					_monsterRenderRect.X -= lungeSpeed;
				}
				else if (step <= 12)
				{
					_monsterRenderRect.X += (int)(lungeSpeed * 0.75);
				}
				else
				{
					_monsterAnimationTimer.Stop();
					_monsterAnimationTimer.Tick -= MonsterTickHandler;
					_isAnimating = false;

					// Reset the render rect to its original "Idle" position
					_monsterRenderRect = GetPaddedBounds(_picMonster.Image, _picMonster.Bounds);

					UpdateMonsterSprite("idle");
					SetKnightIdleSprite();

					// 4. Handle Death or Resume
					if (_session.CurrentPlayerHealth <= 0)
                    {
                        _countdownTimer.Stop();
                        _monsterAnimationTimer.Stop();
                        _playerAnimationTimer.Stop();
                        _damageNumbers.Clear(); // Clean up stuck numbers
                        _lblTimer.Visible = false;
                        _txtAnswer.Visible = false;
                        _btnSubmit.Visible = false;
                        ShowGameOverScreen();
                    }
                    else
                    {
                        _txtAnswer.Focus();

                        // 5. QUEUE CHECK: If a question was waiting, start the dice now
                        if (_isQuestionPending)
                        {
                            GenerateQuestion();
                        }
                    }
                }

                // 6. Targeted Invalidate: Only redraw the monster lane during movement
                this.Invalidate(GetCombatZone());
            }

            // 2. CRITICAL: Attach and START
            _monsterAnimationTimer.Tick += MonsterTickHandler;
            _monsterAnimationTimer.Start();
        }
    }
}