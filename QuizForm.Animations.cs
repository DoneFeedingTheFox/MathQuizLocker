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
				_monsterHealthBar.Value = Math.Max(0, _monsterHealth);
				_lblFeedback.Text = $"HIT! -{ans} DMG";
				_lblFeedback.ForeColor = Color.Lime;

				// Start animation - victory check is now inside this method's cleanup
				AnimateMeleeStrike(ans);
			}
			else
			{
				int monsterDmg = _a * _b;
				_playerHealth -= monsterDmg;
				_playerHealthBar.Value = Math.Max(0, _playerHealth);
				_lblFeedback.Text = $"Ouch! {monsterDmg} DMG!";
				_lblFeedback.ForeColor = Color.Red;

				// Start animation - death check is now inside this method's cleanup
				AnimateMonsterAttack(monsterDmg);
			}
		}

		private void AnimateMeleeStrike(int damage)
		{
			_isAnimating = true;
			_animationTimer.Stop();
			_knightOriginalPos = _picKnight.Location;
			int stage = KnightProgression.GetKnightStageIndex(_settings.PlayerProgress.Level);
			_picKnight.Image = LoadImageNoLock(AssetPaths.KnightAttack(stage));

			int distance = _picMonster.Left - _picKnight.Right + 50;
			int step = 0;
			_animationTimer = new System.Windows.Forms.Timer { Interval = 15 };
			_animationTimer.Tick += (s, e) => {
				Rectangle oldK = _picKnight.Bounds; Rectangle oldM = _picMonster.Bounds;
				step++;

				if (step <= 4) _picKnight.Left += (distance / 4);
				else if (step == 5) { UpdateMonsterSprite("hit"); ShowDamagePopup(_picMonster, damage, Color.OrangeRed); _picMonster.Left += 20; }
				else if (step <= 12) _picKnight.Left -= (distance / 8);
				else
				{
					// ANIMATION FINISHED
					_animationTimer.Stop();
					_isAnimating = false;
					_picKnight.Location = _knightOriginalPos;
					UpdateMonsterSprite("idle");
					UpdatePlayerStats();

					// CHECK FOR VICTORY NOW
					if (_monsterHealth <= 0)
					{
						XpSystem.AddXp(_settings.PlayerProgress, 50);
						ShowVictoryScreen();
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
			_isAnimating = true;
			_animationTimer.Stop();
			Point monsterStartPos = _picMonster.Location;
			UpdateMonsterSprite("attack");
			int stage = KnightProgression.GetKnightStageIndex(_settings.PlayerProgress.Level);
			_picKnight.Image = LoadImageNoLock(AssetPaths.KnightHit(stage));

			int distanceToKnight = _picMonster.Left - _picKnight.Right + 50;
			int step = 0;
			_animationTimer = new System.Windows.Forms.Timer { Interval = 15 };
			_animationTimer.Tick += (s, e) => {
				Rectangle oldK = _picKnight.Bounds; Rectangle oldM = _picMonster.Bounds;
				step++;

				if (step <= 4) _picMonster.Left -= (distanceToKnight / 4);
				else if (step == 5) { ShowDamagePopup(_picKnight, damage, Color.Red); _picKnight.Top += 10; }
				else if (step <= 12) _picMonster.Left += (distanceToKnight / 8);
				else
				{
					// ANIMATION FINISHED
					_animationTimer.Stop();
					_isAnimating = false;
					_picMonster.Location = monsterStartPos;
					_picKnight.Top = (int)(this.ClientSize.Height * 0.87 - _picKnight.Height);
					UpdateMonsterSprite("idle");
					UpdatePlayerStats();

					// CHECK FOR DEATH NOW
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

		private void ShowDamagePopup(Control target, int damage, Color color)
		{
			Label lbl = new Label { Text = $"-{damage}", Font = new Font("Segoe UI", 26, FontStyle.Bold), ForeColor = color, BackColor = Color.Transparent, AutoSize = true, Location = new Point(target.Left + (target.Width / 2), target.Top) };
			this.Controls.Add(lbl); lbl.BringToFront();
			int ticks = 0; var t = new System.Windows.Forms.Timer { Interval = 20 };
			t.Tick += (s, e) => { ticks++; Rectangle old = lbl.Bounds; lbl.Top -= 3; InvalidateMovingRegion(old, lbl.Bounds); if (ticks > 25) { t.Stop(); this.Controls.Remove(lbl); lbl.Dispose(); } };
			t.Start();
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