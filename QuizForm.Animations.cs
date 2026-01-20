using System;
using System.Drawing;
using System.Windows.Forms;
using MathQuizLocker.Services;


namespace MathQuizLocker
{
    public partial class QuizForm
    {
        private System.Windows.Forms.Timer _meleeTimer = new System.Windows.Forms.Timer { Interval = 15 };
        private void AnimateMeleeStrike(int damage)
        {
            _isAnimating = true;
            _knightOriginalPos = _picKnight.Location;


			bool isDefeated = _session.ApplyDamage(damage, out int xpGained, out bool leveledUp);
		    UpdatePlayerHud();
            ShowDamage(damage, _picMonster.Location, Color.Red);

			if (isDefeated)
			{
				string xpMsg = $"+{xpGained} XP";
				if (leveledUp) xpMsg = "LEVEL UP! " + xpMsg;

				// Show XP gained floating near the knight or feedback label
				_lblFeedback.Text = xpMsg;
				_lblFeedback.ForeColor = leveledUp ? Color.Gold : Color.White;
			}

			// Change the sprite once to the attack frame
			_picKnight.Image = AssetCache.GetImageClone(AssetPaths.KnightAttack(_equippedKnightStage));

            int distance = _picMonster.Left - _picKnight.Right + 50;
            int step = 0;

            // Clean up to prevent "Timer Stacking" which causes that 2 FPS drop
            _meleeTimer.Stop();
            _meleeTimer.Tick -= MeleeTickHandler;

            void MeleeTickHandler(object? s, EventArgs e)
            {
                step++;

                // Movement Logic
                if (step <= 4) _picKnight.Left += (distance / 4);
                else if (step == 5) UpdateMonsterSprite("hit");
                else if (step <= 12) _picKnight.Left -= (distance / 8);
                else
                {
                    _meleeTimer.Stop();
                    _meleeTimer.Tick -= MeleeTickHandler; 

                    _isAnimating = false;
                    _picKnight.Location = _knightOriginalPos;
                    SetKnightIdleSprite();
                    UpdateMonsterSprite("idle");

					if (_session.CurrentMonsterHealth <= 0)
					{
						// Pass victory data if your ShowVictoryScreen supports it
						ShowVictoryScreen();
					}
					else
					{
						GenerateQuestion();
					}

					this.Invalidate(); // Final full redraw to ensure UI is clean
                    return;
                }

                // PERFORMANCE FIX: Only redraw the horizontal combat lane
                this.Invalidate(GetMeleeArea());
            }

            _meleeTimer.Tick += MeleeTickHandler;
            _meleeTimer.Start();
        }
        private void AnimateMonsterAttack(int damage)
        {
            _isAnimating = true;
            UpdateMonsterSprite("attack");

            bool isHeavyHit = damage >= 40;

            // Process damage logic
            _session.ApplyPlayerDamage(damage);
            ShowDamage(damage, _picKnight.Location, isHeavyHit ? Color.DarkRed : Color.OrangeRed);

            // Swap knight sprite once
            _picKnight.Image = AssetCache.GetImageClone(AssetPaths.KnightHit(_equippedKnightStage));

            int step = 0;
            int originalMonsterX = _picMonster.Left;

            // 1. Clean up timer to prevent stacking
            _meleeTimer.Stop();
            _meleeTimer.Tick -= MonsterTickHandler;

            void MonsterTickHandler(object? s, EventArgs e)
            {
                step++;

                int lungeSpeed = isHeavyHit ? 30 : 20;

                // Movement Logic
                if (step <= 5) _picMonster.Left -= lungeSpeed;
                else if (step <= 12) _picMonster.Left += (int)(lungeSpeed * 0.75);
                else
                {
                    _meleeTimer.Stop();
                    _meleeTimer.Tick -= MonsterTickHandler;

                    _isAnimating = false;
                    _picMonster.Left = originalMonsterX;
                    UpdateMonsterSprite("idle");
                    SetKnightIdleSprite();

                    if (_session.CurrentPlayerHealth <= 0)
                    {
                        ShowGameOverScreen();
                    }
                    else
                    {
                        // Re-aktiver input etter angrepet
                        _txtAnswer.Enabled = true;
                        _btnSubmit.Enabled = true;
                        GenerateQuestion();
                    }

                    this.Invalidate();
                    return;
                }

                this.Invalidate(GetCombatZone());
            }

            _meleeTimer.Tick += MonsterTickHandler;
            _meleeTimer.Start();
        }
    }
}