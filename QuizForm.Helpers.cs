using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using MathQuizLocker.Services;
using Velopack;
using Velopack.Sources;

namespace MathQuizLocker
{
	public partial class QuizForm
	{
		// ------------------------------------------------------------
		// Configurable Loot Table
		// Level -> (EquipStage, ItemFile)
		//
		// EquipStage is the knight visual stage you want to apply AFTER the fight
		// when the player presses Continue.
		//
		// ItemFile must exist in: \assets\items\
		// Chest file is fixed to chest_01.png for now.
		// ------------------------------------------------------------
		private const string ChestFile = "chest_01.png";

		private readonly Dictionary<int, (int equipStage, string itemFile)> _lootTable =
			new Dictionary<int, (int equipStage, string itemFile)>
			{
				// Example: when reaching Level 1, show chest + helmet_01 and equip stage 1
				{ 1, (1, "helmet_01.png") },

				// Add more rewards here, for example:
				// { 5, (3, "armor_01.png") },
				// { 7, (4, "sword_01.png") },
			};

		// Prevent repeating rewards during the same run (persistence can be added later to AppSettings)
		private readonly HashSet<int> _claimedLootLevels = new HashSet<int>();

		// Pending loot item for ShowLootDrop()
		private string? _pendingLootItemFile;

		private void SpawnMonster()
		{
			int tier = Math.Max(1, _settings.MaxFactorUnlocked);
			_maxMonsterHealth = 40 + (tier * 35);
			_monsterHealth = _maxMonsterHealth;

			_currentMonsterName = tier < 4 ? "slime" : tier < 7 ? "orc" : "dragon";
			UpdateMonsterSprite("idle");
			Invalidate();
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

		private void UpdateMonsterSprite(string state)
		{
			string suffix = state == "idle" ? "" : $"_{state}";
			string path = Path.Combine(AssetPaths.AssetsRoot, "Monsters", $"{_currentMonsterName}{suffix}.png");

			var img = AssetCache.GetImageClone(path);
			if (img != null)
			{
				_picMonster.Image?.Dispose();
				_picMonster.Image = img;
			}
		}

		private void AnimateDiceRoll()
		{
			_isAnimating = true;
			_isDiceAnimating = true;
			_scrambleTicks = 0;

			_die1.Visible = _die2.Visible = _picMultiply.Visible = false;

			_diceCurrentPositions = new PointF[] {
				new PointF(_die1.Left, _die1.Top),
				new PointF(_die2.Left, _die2.Top),
				new PointF(_picMultiply.Left, _picMultiply.Top)
			};

			_diceVelocities = new PointF[3];
			_diceRotationAngles = new float[3];

			float centerX = this.ClientSize.Width / 2f;
			float deadZone = 110f;

			for (int i = 0; i < 3; i++)
			{
				_diceVelocities[i] = new PointF(_rng.Next(-15, 16), _rng.Next(-30, -20));
				_diceRotationAngles[i] = _rng.Next(0, 360);
			}

			_diceTimer?.Stop();
			_diceTimer = new System.Windows.Forms.Timer { Interval = 10 };
			_diceTimer.Tick += (s, e) =>
			{
				_scrambleTicks++;

				for (int i = 0; i < 3; i++)
				{
					_diceVelocities[i].Y += 4.5f;
					_diceCurrentPositions[i].X += _diceVelocities[i].X;
					_diceCurrentPositions[i].Y += _diceVelocities[i].Y;
					_diceRotationAngles[i] += _diceVelocities[i].X * 5.0f;

					if (i == 2)
					{
						_diceCurrentPositions[i].X = centerX - (_picMultiply.Width / 2f);
					}
					else
					{
						float relativeX = _diceCurrentPositions[i].X + (_die1.Width / 2f) - centerX;

						if (Math.Abs(relativeX) < deadZone)
						{
							float pushDir = (relativeX >= 0) ? 1 : -1;
							_diceVelocities[i].X = pushDir * 12.0f;
						}

						if (_diceCurrentPositions[i].X < centerX - 350) _diceVelocities[i].X = Math.Abs(_diceVelocities[i].X);
						if (_diceCurrentPositions[i].X > centerX + 350 - _die1.Width) _diceVelocities[i].X = -Math.Abs(_diceVelocities[i].X);
					}

					float floorY = (this.ClientSize.Height * 0.10f);
					if (_diceCurrentPositions[i].Y > floorY)
					{
						_diceVelocities[i].Y *= -0.35f;
						_diceCurrentPositions[i].Y = floorY;
						_diceVelocities[i].X *= 0.6f;
						_diceRotationAngles[i] *= 0.7f;
					}
				}

				this.Invalidate();

				if (_scrambleTicks >= 18)
				{
					_diceTimer.Stop();
					_isDiceAnimating = false;
					_isAnimating = false;
					FinalizeDiceLand();
				}
			};

			_diceTimer.Start();
		}

		private void FinalizeDiceLand()
		{
			Point die1Pos = Point.Round(_diceCurrentPositions[0]);
			Point die2Pos = Point.Round(_diceCurrentPositions[1]);
			Point multPos = Point.Round(_diceCurrentPositions[2]);

			int minGap = 15;
			int safeWidth = (_die1.Width / 2) + (_picMultiply.Width / 2) + minGap;

			if (Math.Abs(die1Pos.X - multPos.X) < safeWidth)
				die1Pos.X = multPos.X - safeWidth;

			if (Math.Abs(die2Pos.X - multPos.X) < safeWidth)
				die2Pos.X = multPos.X + safeWidth;

			_die1.Location = die1Pos;
			_die2.Location = die2Pos;
			_picMultiply.Location = multPos;

			UpdateDiceVisuals();
			_die1.Visible = _die2.Visible = _picMultiply.Visible = true;

			this.Invalidate();
		}

		private void GenerateQuestion()
		{
			try
			{
				var q = _quizEngine.GetNextQuestion();
				_a = q.a;
				_b = q.b;

				AnimateDiceRoll();

				_txtAnswer.Clear();
				_txtAnswer.Focus();
			}
			catch
			{
				_quizEngine.InitializeForCurrentLevel();
				GenerateQuestion();
			}
		}

		private void UpdateDiceVisuals()
		{
			_die1.Image?.Dispose();
			_die2.Image?.Dispose();
			_die1.Image = AssetCache.GetImageClone(AssetPaths.Dice($"die_{_a}.png"));
			_die2.Image = AssetCache.GetImageClone(AssetPaths.Dice($"die_{_b}.png"));
		}

		private void ResetBattleState()
		{
			_playerHealth = _maxPlayerHealth; //
			_quizEngine.InitializeForCurrentLevel(); //

			_btnContinue.Visible = false; //
			_btnExit.Visible = false; //

			_txtAnswer.Visible = true; //
			_btnSubmit.Visible = true; //
			_die1.Visible = true; //
			_die2.Visible = true; //
			_picMultiply.Visible = true; //

			// Hide loot visuals on reset
			HideLootDrop(); //
			_awaitingChestOpen = false; //
			_pendingKnightStage = -1; //
			_preVictoryKnightStage = -1; //
			_pendingLootItemFile = null; //

			UpdatePlayerHud(); //

			// --- FIX FOR KNIGHT RESET ---
			// 1. Force the internal stage variable back to 0 (Level 1 visual)
			_equippedKnightStage = 0;

			// 2. Persist this change to your settings so it stays reset after closing the app
			_settings.PlayerProgress.EquippedKnightStage = 0;
			AppSettings.Save(_settings); //

			// 3. Reload the actual Image into _picKnight.Image for OnPaint to see
			SetKnightIdleSprite(); //

			SpawnMonster(); //
			GenerateQuestion(); //

			_txtAnswer.Focus(); //
			this.Invalidate(); // Force redraw the whole scene
		}

		// ---- SPLIT: HUD vs Sprite ----

		private void UpdatePlayerHud()
		{
			if (_isAnimating) return;

			var p = _settings.PlayerProgress;
			int nextLevelXp = XpSystem.GetXpRequiredForNextLevel(p.Level);

			_lblLevel.Text = $"KNIGHT LEVEL: {p.Level}";
			_lblXpStatus.Text = $"XP: {p.CurrentXp} / {nextLevelXp}";
			Invalidate();
		}

		// IMPORTANT: This method must NOT update visuals from current level.
		// Keep it as a compatibility wrapper if other code still calls it.
		private void UpdateKnightSpriteForCurrentLevel()
		{
			SetKnightIdleSprite(); // uses _equippedKnightStage
		}

		private void HandleDeath()
		{
			_lblFeedback.Text = "YOU HAVE FALLEN...";
			var t = new System.Windows.Forms.Timer { Interval = 2000 };
			t.Tick += (s, e) =>
			{
				t.Stop();
				_playerHealth = _maxPlayerHealth;
				_quizEngine.InitializeForCurrentLevel();
				ResetBattleState();
			};
			t.Start();
		}

		private static Image? LoadImageNoLock(string path)
		{
			if (!File.Exists(path)) return null;
			try
			{
				byte[] bytes = File.ReadAllBytes(path);
				using var ms = new MemoryStream(bytes);
				return new Bitmap(ms);
			}
			catch { return null; }
		}

		private void EnsureHudOnTop()
		{
			_txtAnswer.BringToFront();
			_btnSubmit.BringToFront();

			_lblFeedback.BringToFront();
			_lblLevel.BringToFront();
			_lblXpStatus.BringToFront();
			_btnReset.BringToFront();
			_lblVersion.BringToFront();

			_die1.BringToFront();
			_picMultiply.BringToFront();
			_die2.BringToFront();

			if (_btnContinue.Visible) _btnContinue.BringToFront();
			if (_btnExit.Visible) _btnExit.BringToFront();

			if (_picChest.Visible) _picChest.BringToFront();
			if (_picLoot.Visible) _picLoot.BringToFront();
		}

		// ------------------------------------------------------------
		// Loot helper: call this right AFTER XP is awarded (and saved)
		// and AFTER you know the new level.
		//
		// This sets:
		//   _awaitingChestOpen
		//   _pendingKnightStage
		//   _pendingLootItemFile
		// ------------------------------------------------------------
		private void EvaluateLootForNewLevel()
		{
			int newLevel = _settings.PlayerProgress.Level;

			if (_lootTable.TryGetValue(newLevel, out var reward) && !_claimedLootLevels.Contains(newLevel))
			{
				_pendingKnightStage = reward.equipStage;
				_pendingLootItemFile = reward.itemFile;
				_awaitingChestOpen = true;
			}
			else
			{
				_pendingKnightStage = -1;
				_pendingLootItemFile = null;
				_awaitingChestOpen = false;
			}
		}

		// ------------------------------------------------------------
		// Loot helper: mark claimed after applying the reward (on Continue)
		// ------------------------------------------------------------
		private void MarkLootClaimedForCurrentLevel()
		{
			_claimedLootLevels.Add(_settings.PlayerProgress.Level);
		}

		private async Task UpdateMyApp()
		{
			try
			{
				var mgr = new UpdateManager(new GithubSource("https://github.com/DoneFeedingTheFox/MathQuizLocker", null, false));
				var newVersion = await mgr.CheckForUpdatesAsync();
				if (newVersion != null)
				{
					await mgr.DownloadUpdatesAsync(newVersion);
					mgr.ApplyUpdatesAndRestart(newVersion);
				}
			}
			catch { }
		}
	}
}
