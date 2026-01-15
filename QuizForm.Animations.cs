using System;
using System.Drawing;
using System.Windows.Forms;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
	public partial class QuizForm
	{




		private void BtnSubmit_Click(object? sender, EventArgs e)
		{
			if (_isAnimating || !int.TryParse(_txtAnswer.Text, out int ans)) return;
			var result = _session.ProcessAnswer(ans, _a, _b);

			if (result.IsCorrect)
			{
				_monsterHealth -= ans;
				_lblFeedback.ForeColor = Color.Lime;

				AnimateMeleeStrike(ans);
			}
			else
			{
				int monsterDmg = _a * _b;
				_playerHealth -= monsterDmg;
				_lblFeedback.ForeColor = Color.Red;

				AnimateMonsterAttack(monsterDmg);
			}
		}

		private void AnimateMeleeStrike(int damage)
		{
			EnsureHudOnTop();
			_isAnimating = true;
			_animationTimer.Stop();

			_knightOriginalPos = _picKnight.Location;

			// Use EQUIPPED stage for visuals (no mid-fight equipment changes)
			EnsureKnightEquippedStageInitialized();
			_picKnight.Image = LoadImageNoLock(AssetPaths.KnightAttack(_equippedKnightStage));

			int distance = _picMonster.Left - _picKnight.Right + 50;
			int step = 0;

			_animationTimer = new System.Windows.Forms.Timer { Interval = 15 };
			_animationTimer.Tick += (s, e) =>
			{
				Rectangle oldK = _picKnight.Bounds;
				Rectangle oldM = _picMonster.Bounds;
				step++;

				if (step <= 4)
				{
					_picKnight.Left += (distance / 4);
				}
				else if (step == 5)
				{
					UpdateMonsterSprite("hit");
					AddFloatingDamage(_picMonster, damage, Color.OrangeRed);
				}
				else if (step <= 12)
				{
					_picKnight.Left -= (distance / 8);
				}
				else
				{
					_animationTimer.Stop();
					_isAnimating = false;

					// Restore position and restore IDLE sprite
					_picKnight.Location = _knightOriginalPos;
					SetKnightIdleSprite();
					UpdateMonsterSprite("idle");

					// Update HUD only (no sprite swaps here)
					UpdatePlayerHud();

					if (_monsterHealth <= 0)
					{
						// IMPORTANT: stage BEFORE level-up must be the currently EQUIPPED visuals,
						// not the stage derived from PlayerProgress.Level.
						_preVictoryKnightStage = _equippedKnightStage;

						// Award XP (may level up)
						XpSystem.AddXp(_settings.PlayerProgress, 50);
						AppSettings.Save(_settings);

						// Evaluate loot based on NEW level using the configured loot table
						EvaluateLootForNewLevel();

						// If loot is pending, persist the reward immediately (so Exit won't lose it)
						// BUT do not redraw knight sprite here (we're on the victory screen anyway)
						if (_awaitingChestOpen && _pendingKnightStage > 0)
						{
							_equippedKnightStage = _pendingKnightStage;      // commit reward now
							MarkLootClaimedForCurrentLevel();                // mark as claimed (still in-memory currently)
							AppSettings.Save(_settings);                     // persist level/xp already saved, but safe to save again
						}

						// HUD text/numbers only
						UpdatePlayerHud();

						ShowVictoryScreen();
						return;

					}

					else
					{
						GenerateQuestion();
						LayoutCombat();
					}
				}

				InvalidateMovingRegion(oldK, _picKnight.Bounds, oldM, _picMonster.Bounds);
			};

			_animationTimer.Start();
		}

		private void AnimateMonsterAttack(int damage)
		{
			EnsureHudOnTop();

			_isAnimating = true;
			_animationTimer.Stop();

			Point monsterStartPos = _picMonster.Location;
			UpdateMonsterSprite("attack");

			// Use EQUIPPED stage for visuals (no mid-fight equipment changes)
			EnsureKnightEquippedStageInitialized();
			_picKnight.Image = LoadImageNoLock(AssetPaths.KnightHit(_equippedKnightStage));

			int distanceToKnight = _picMonster.Left - _picKnight.Right + 50;
			int step = 0;

			_animationTimer = new System.Windows.Forms.Timer { Interval = 15 };
			_animationTimer.Tick += (s, e) =>
			{
				Rectangle oldK = _picKnight.Bounds;
				Rectangle oldM = _picMonster.Bounds;
				step++;

				if (step <= 4)
				{
					_picMonster.Left -= (distanceToKnight / 4);
				}
				else if (step == 5)
				{
					AddFloatingDamage(_picKnight, damage, Color.Red);
					_picKnight.Top += 10;
				}
				else if (step <= 12)
				{
					_picMonster.Left += (distanceToKnight / 8);
				}
				else
				{
					_animationTimer.Stop();
					_isAnimating = false;

					_picMonster.Location = monsterStartPos;
					_picKnight.Top = (int)(this.ClientSize.Height * 0.87 - _picKnight.Height);
					UpdateMonsterSprite("idle");

					// IMPORTANT: restore idle sprite after being hit
					SetKnightIdleSprite();

					// HUD only
					UpdatePlayerHud();

					if (_playerHealth <= 0)
					{
						HandleDeath();
					}
					else
					{
						GenerateQuestion();
						LayoutCombat();
					}
				}

				InvalidateMovingRegion(oldK, _picKnight.Bounds, oldM, _picMonster.Bounds);
			};

			_animationTimer.Start();
		}

		private void InvalidateMovingRegion(Rectangle oldA, Rectangle newA, Rectangle oldB, Rectangle newB)
		{
			this.Invalidate(Rectangle.Union(Rectangle.Union(oldA, newA), Rectangle.Union(oldB, newB)));
		}

		private void InvalidateMovingRegion(Rectangle oldA, Rectangle newA)
		{
			this.Invalidate(Rectangle.Union(oldA, newA));
		}
	}
}
