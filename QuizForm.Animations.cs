using System;
using System.Drawing;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
	public partial class QuizForm
	{
		// Melee strike state (heartbeat-driven)
		private bool _meleeActive = false;
		private int _meleeStep = 0;
		private float _meleeDistance = 0f;
		private float _meleeOrigX = 0f;
		private int _meleeOrigXInt => (int)_meleeOrigX;

		// Monster lunge state (heartbeat-driven)
		private bool _monsterLungeActive = false;
		private int _monsterStep = 0;
		private float _monsterOrigX = 0f;
		private float _monsterLungeSpeed = 0f;
		private bool _monsterHeavyHit = false;

		private void AnimateMeleeStrike(int damage)
		{
			// Preserve underlying logic
			bool isDefeated = _session.ApplyDamage(damage, out int xpGained, out bool leveledUp);
			UpdatePlayerHud();

			ShowDamage(damage, new Point((int)_monsterRect.X, (int)_monsterRect.Y), Color.Red);

			// Prepare sprite
			ReplaceImage(ref _knightImg, AssetCache.GetImageClone(AssetPaths.KnightAttack(_equippedKnightStage)));
			RecalcKnightDrawRect();

			// Initialize melee state
			_isAnimating = true;
			_meleeActive = true;
			_meleeStep = 0;
			_meleeOrigX = _knightRect.X;

			float knightRight = _knightRect.Right;
			float target = _monsterRect.Left;
			_meleeDistance = (target - knightRight);

			Invalidate(GetMeleeArea());
		}

		private bool UpdateMeleeStrike(ref Rectangle dirty)
		{
			Rectangle oldK = Rectangle.Round(_knightDrawRect);

			_meleeStep++;

			if (_meleeStep <= 4)
			{
				_knightRect.X += (_meleeDistance / 4f);
				RecalcKnightDrawRect();
			}
			else if (_meleeStep == 5)
			{
				UpdateMonsterSprite("hit");
			}
			else if (_meleeStep <= 12)
			{
				_knightRect.X -= (_meleeDistance / 8f);
				RecalcKnightDrawRect();
			}
			else
			{
				_knightRect.X = _meleeOrigX;
				SetKnightIdleSprite();
				UpdateMonsterSprite("idle");
				_meleeActive = false;
				_isAnimating = false;

				if (_session.CurrentMonsterHealth <= 0)
				{
					_countdownTimer.Stop();
					_lblTimer.Visible = false;
					ShowVictoryScreen();
				}
				else
				{
					if (_isQuestionPending) _isQuestionPending = false;
					GenerateQuestion();
				}

				dirty = Rectangle.Union(dirty, GetCombatZone());
				return true;
			}

			Rectangle newK = Rectangle.Round(_knightDrawRect);
			dirty = dirty.IsEmpty ? Rectangle.Union(oldK, newK) : Rectangle.Union(dirty, Rectangle.Union(oldK, newK));
			return true;
		}

		private void AnimateMonsterAttack(int damage)
		{
			if (_session.CurrentPlayerHealth <= 0) return;

			if (_isAnimating)
			{
				_isQuestionPending = true;
				return;
			}

			_isAnimating = true;
			_monsterLungeActive = true;

			// Preserve your original behavior: monster goes into attack sprite first
			UpdateMonsterSprite("attack"); // :contentReference[oaicite:2]{index=2}

			_monsterStep = 0;
			_monsterOrigX = _monsterRect.X;

			bool isHeavyHit = damage >= 40; // :contentReference[oaicite:3]{index=3}
			_monsterHeavyHit = isHeavyHit;

			float lungeDistance = isHeavyHit ? 120f : 80f;
			_monsterLungeSpeed = lungeDistance / 6f;

			// Correct API from your session manager
			_session.ApplyPlayerDamage(damage); // :contentReference[oaicite:4]{index=4}
			bool playerDefeated = _session.CurrentPlayerHealth <= 0;

			UpdatePlayerHud();

			ShowDamage(
				damage,
				new Point((int)_knightRect.X, (int)_knightRect.Y),
				isHeavyHit ? Color.DarkRed : Color.OrangeRed
			);

			ReplaceImage(ref _knightImg, AssetCache.GetImageClone(AssetPaths.KnightHit(_equippedKnightStage)));
			RecalcKnightDrawRect();

			if (playerDefeated)
				_countdownTimer.Stop();

			Invalidate(GetCombatZone());
		}


		private bool UpdateMonsterLunge(ref Rectangle dirty)
		{
			Rectangle oldM = Rectangle.Round(_monsterDrawRect);

			_monsterStep++;

			if (_monsterStep <= 6)
			{
				_monsterRect.X -= _monsterLungeSpeed;
				RecalcMonsterDrawRect();
			}
			else if (_monsterStep <= 12)
			{
				_monsterRect.X += _monsterLungeSpeed;
				RecalcMonsterDrawRect();
			}
			else
			{
				_monsterRect.X = _monsterOrigX;
				RecalcMonsterDrawRect();

				SetKnightIdleSprite();

				_monsterLungeActive = false;
				_isAnimating = false;

				if (_session.CurrentPlayerHealth <= 0)
				{
					ShowGameOverScreen();
					dirty = Rectangle.Union(dirty, this.ClientRectangle);
					return true;
				}

				if (_isQuestionPending)
				{
					_isQuestionPending = false;
					GenerateQuestion();
				}

				dirty = Rectangle.Union(dirty, GetCombatZone());
				return true;
			}

			Rectangle newM = Rectangle.Round(_monsterDrawRect);
			dirty = dirty.IsEmpty ? Rectangle.Union(oldM, newM) : Rectangle.Union(dirty, Rectangle.Union(oldM, newM));
			return true;
		}

		private bool UpdateChestShake(ref Rectangle dirty)
		{
			var oldR = Rectangle.Round(_chestRect);
			oldR.Inflate(20, 20);

			_chestShakeTicks++;

			if (_chestShakeTicks < 20)
			{
				_chestRect.X += _rng.Next(-5, 6);
			}
			else if (_chestShakeTicks == 20)
			{
				ReplaceImage(ref _chestImg, AssetCache.GetImageClone(AssetPaths.Items("chest_open_01.png")));
			}
			else if (_chestShakeTicks == 25)
			{
				if (!string.IsNullOrEmpty(_pendingLootItemFile))
				{
					ReplaceImage(ref _lootImg, AssetCache.GetImageClone(AssetPaths.Items(_pendingLootItemFile)));
					_lootVisible = true;
				}

				_equippedKnightStage = _pendingKnightStage;
				_settings.PlayerProgress.EquippedKnightStage = _equippedKnightStage;
				AppSettings.Save(_settings);

				_btnContinue.Visible = true;
				_btnExit.Visible = true;

				_isChestOpening = false;
				_awaitingChestOpen = false;

				SetKnightIdleSprite();
			}

			var newR = Rectangle.Round(_chestRect);
			newR.Inflate(20, 20);

			dirty = dirty.IsEmpty ? Rectangle.Union(oldR, newR) : Rectangle.Union(dirty, Rectangle.Union(oldR, newR));
			return true;
		}
	}
}
