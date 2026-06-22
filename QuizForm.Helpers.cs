using System;
using System.Drawing;
using System.IO;
using MathQuizLocker.Services;
using MathQuizLocker.Models;

namespace MathQuizLocker
{
	public partial class QuizForm
	{
		/// <summary>Sets the form background from Assets/Backgrounds based on player level and boss flag (e.g. meadow_01.png or castle_01_boss.png).</summary>
		private void ApplyBiomeForCurrentLevel()
		{
			ReleaseBackgroundImage();
			if (_isShowingStory)
			{
				SetBackgroundImage(AssetCache.GetBackgroundForDisplay(AssetPaths.BackgroundBase("scroll_bg")));
				return;
			}

			int level = _settings.PlayerProgress.Level;

			string biomeBase = level switch
			{
				1 => "meadow_01",
				2 => "swamp_01",
				3 => "forest_01",
				4 => "cave_01",
				_ => "castle_01"
			};

			_currentBiome = biomeBase.Replace("_01", "");

			bool isBoss =
				!string.IsNullOrEmpty(_currentMonsterName) &&
				_currentMonsterName.Contains("boss", StringComparison.OrdinalIgnoreCase);

			string bgBase = isBoss ? $"{biomeBase}_boss" : biomeBase;
			var bg = AssetCache.GetBackgroundForDisplay(AssetPaths.BackgroundBase(bgBase));
			if (bg != null)
				SetBackgroundImage(bg);
			else
				System.Diagnostics.Debug.WriteLine($"[BG LOAD FAIL] {AssetPaths.BackgroundBase(bgBase)}");
		}

		/// <summary>Scales image to fit inside container preserving aspect ratio; returns centered rectangle.</summary>
		private Rectangle GetPaddedBounds(Image img, Rectangle container)
		{
			float iw = img.Width;
			float ih = img.Height;
			float cw = container.Width;
			float ch = container.Height;

			float scale = Math.Min(cw / iw, ch / ih);
			float w = iw * scale;
			float h = ih * scale;

			float x = container.X + (cw - w) / 2f;
			float y = container.Y + (ch - h) / 2f;

			return Rectangle.Round(new RectangleF(x, y, w, h));
		}

		private void UpdatePlayerHud()
		{
			_lblLevel.Text = $"LEVEL {_settings.PlayerProgress.Level}";
			_lblLevel.Location = new Point(30, 20);

			int required = XpSystem.GetXpRequiredForNextLevel(_settings.PlayerProgress.Level);
			_lblXpStatus.Text = $"XP: {_settings.PlayerProgress.CurrentXp}/{required}";
			_lblXpStatus.Location = new Point(30, 60);

			_lblFeedback.Location = new Point(30, 100);
		}

		private bool CheckIfBossShouldSpawn()
		{
			int currentLevel = _settings.PlayerProgress.Level;
			int requiredXp = XpSystem.GetXpRequiredForNextLevel(currentLevel);
			return _settings.PlayerProgress.CurrentXp >= requiredXp;
		}

		/// <summary>Starts a horizontal scroll transition to the next fight scene.</summary>
		private void StartTransition()
		{
			ClearLootDisplay();
			ClearCachedImage(ref _knightImg);
			ClearCachedImage(ref _monsterImg);
			ClearCachedImage(ref _die1Img);
			ClearCachedImage(ref _die2Img);
			ClearCachedImage(ref _mulImg);
			_diceVisible = false;
			_txtAnswer.Visible = false;
			_btnSubmit.Visible = false;
			_lblTimer.Visible = false;
			_countdownTimer.Stop();

			// Determine next biome
			int level = _settings.PlayerProgress.Level;
			string nextBiomeBase = level switch
			{
				1 => "meadow",
				2 => "swamp",
				3 => "forest",
				4 => "cave",
				_ => "castle"
			};

			// Pre-load next background
			bool isBoss = CheckIfBossShouldSpawn();
			string nextBgBase = level switch
			{
				1 => isBoss ? "meadow_01_boss" : "meadow_01",
				2 => isBoss ? "swamp_01_boss" : "swamp_01",
				3 => isBoss ? "forest_01_boss" : "forest_01",
				4 => isBoss ? "cave_01_boss" : "cave_01",
				_ => isBoss ? "castle_01_boss" : "castle_01"
			};

			AssignDisplayBackground(ref _nextBackgroundImage, AssetPaths.BackgroundBase(nextBgBase));

			// Pre-load next monster
			var nextMonsterConfig = _monsterService.GetMonsterByLevel(level, isBoss);
			string nextMonsterName = nextMonsterConfig.Name;
			var nextConfig = _monsterService.GetMonster(nextMonsterName);
			string nextMonsterSpritePath = nextConfig.SpritePath;
			if (nextMonsterSpritePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
				nextMonsterSpritePath = nextMonsterSpritePath.Substring(0, nextMonsterSpritePath.Length - 4);
			string nextMonsterFullPath = nextMonsterSpritePath + ".png";
			AssignCachedImage(ref _nextMonsterImg, nextMonsterFullPath);

			// Calculate next monster position
			float scale = this.ClientSize.Height / 1080f;
			float monsterSize = 450f * scale;
			_nextMonsterRect = new RectangleF(
				this.ClientSize.Width * 0.65f,
				(this.ClientSize.Height * 0.90f) - monsterSize,
				monsterSize,
				monsterSize);

			// Look up and pre-load transition graphic
			string? transitionGraphicPath = _transitionGraphicService.GetTransitionGraphic(_currentBiome, nextBiomeBase);
			if (!string.IsNullOrEmpty(transitionGraphicPath))
			{
				transitionGraphicPath = AssetPaths.ResolveExistingPath(transitionGraphicPath);
				if (File.Exists(transitionGraphicPath))
				{
					AssignCachedImage(ref _nextTransitionGraphicImg, transitionGraphicPath);

					if (_nextTransitionGraphicImg != null)
					{
						float targetHeight = this.ClientSize.Height;
						float aspectRatio = (float)_nextTransitionGraphicImg.Width / _nextTransitionGraphicImg.Height;
						float graphicWidth = targetHeight * aspectRatio;
						float graphicHeight = targetHeight;

						_nextTransitionGraphicRect = new RectangleF(
							-graphicWidth / 2f, // Left side (half visible as next scene scrolls in)
							0,
							graphicWidth,
							graphicHeight);
					}
				}
			}

			// Initialize transition state
			_isTransitioning = true;
			_transitionOffsetX = 0f;
			_transitionStartTime = 0f;

			// Position transition graphic on right side (half visible)
			if (_transitionGraphicImg != null)
			{
				// Scale to full screen height while preserving aspect ratio
				float targetHeight = this.ClientSize.Height;
				float aspectRatio = (float)_transitionGraphicImg.Width / _transitionGraphicImg.Height;
				float graphicWidth = targetHeight * aspectRatio;
				float graphicHeight = targetHeight;
				_transitionGraphicRect = new RectangleF(
					this.ClientSize.Width - graphicWidth / 2f,
					0,
					graphicWidth,
					graphicHeight);
			}

			this.Invalidate();
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
				_lblFeedback.Visible = true;
			}

			// Load transition graphic for current scene (positioned on right side)
			LoadTransitionGraphicForCurrentBiome();

			LayoutCombat();
			Invalidate(GetCombatZone());
		}

		/// <summary>Loads the transition graphic for the current biome and positions it on the right side.</summary>
		private void LoadTransitionGraphicForCurrentBiome()
		{
			// Determine next biome (same as current for now, will be updated on transition)
			int level = _settings.PlayerProgress.Level;
			string nextBiome = level switch
			{
				1 => "meadow",
				2 => "swamp",
				3 => "forest",
				4 => "cave",
				_ => "castle"
			};

			// Look up transition graphic (same biome to same biome for initial scene)
			string? transitionGraphicPath = _transitionGraphicService.GetTransitionGraphic(_currentBiome, nextBiome);
			if (string.IsNullOrEmpty(transitionGraphicPath))
			{
				// Fallback: try same-to-same transition
				transitionGraphicPath = _transitionGraphicService.GetTransitionGraphic(_currentBiome, _currentBiome);
			}

			if (!string.IsNullOrEmpty(transitionGraphicPath))
			{
				if (File.Exists(transitionGraphicPath))
				{
					if (AssetCache.GetMasterBitmap(transitionGraphicPath) != null)
					{
						AssignCachedImage(ref _transitionGraphicImg, transitionGraphicPath);

						float targetHeight = this.ClientSize.Height;
						float aspectRatio = _transitionGraphicImg != null
							? (float)_transitionGraphicImg.Width / _transitionGraphicImg.Height
							: 0.2f;
						float graphicWidth = targetHeight * aspectRatio;
						float graphicHeight = targetHeight;

						_transitionGraphicRect = new RectangleF(
							this.ClientSize.Width - graphicWidth / 2f,
							0,
							graphicWidth,
							graphicHeight);

						System.Diagnostics.Debug.WriteLine($"[TRANSITION GRAPHIC] Loaded: {transitionGraphicPath}, Scaled: {graphicWidth:F0}x{graphicHeight:F0}");
					}
					else
					{
						System.Diagnostics.Debug.WriteLine($"[TRANSITION GRAPHIC] Failed to load image: {transitionGraphicPath}");
					}
				}
				else
				{
					System.Diagnostics.Debug.WriteLine($"[TRANSITION GRAPHIC] File not found: {transitionGraphicPath}");
				}
			}
			else
			{
				System.Diagnostics.Debug.WriteLine($"[TRANSITION GRAPHIC] No path found for {_currentBiome} -> {nextBiome}");
			}
		}

		/// <summary>Loads monster sprite for current monster and state ("idle", "hit", "attack") and updates _monsterImg.</summary>
		private void UpdateMonsterSprite(string state)
		{
			var config = _monsterService.GetMonster(_currentMonsterName);
			if (config == null || string.IsNullOrWhiteSpace(config.SpritePath))
				return;

			string basePath = config.SpritePath;

			// SpritePath from JSON may be ".../goblin" or ".../goblin.png"; we append _state and .png
			if (basePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
				basePath = basePath.Substring(0, basePath.Length - 4);

			string suffix = (state == "idle") ? "" : $"_{state}";
			string fullPath = basePath + suffix + ".png";

			if (AssetCache.GetMasterBitmap(fullPath) != null)
			{
				AssignCachedImage(ref _monsterImg, fullPath);
				RecalcMonsterDrawRect();
			}
		}

		/// <summary>Clears input, gets next question from engine, updates dice images and starts dice roll animation.</summary>
		private void GenerateQuestion()
		{
			_txtAnswer.Clear();
			_txtAnswer.Enabled = true;
			_btnSubmit.Enabled = true;

			if (_isAnimating)
			{
				_isQuestionPending = true;
				return;
			}

			_isQuestionPending = false;

			var q = _quizEngine.GetNextQuestion();
			_a = q.a;
			_b = q.b;

			// 3. REFRESH VISUALS (dispose old sprite images explicitly)
			AssignCachedImage(ref _die1Img, AssetPaths.Dice($"die_{_a}.png"));
			AssignCachedImage(ref _die2Img, AssetPaths.Dice($"die_{_b}.png"));
			AssignCachedImage(ref _mulImg, AssetPaths.Dice("multiply.png"));

			_diceVisible = true;

			// 4. TRIGGER ANIMATION
			AnimateDiceRoll();

			_txtAnswer.Focus();
		}

		private void DrawHealthBar(Graphics g, Rectangle bounds, int current, int max, bool isPlayer)
		{
			int barWidth = (int)(bounds.Width * 0.8);
			int barHeight = 12;
			int x = bounds.X + (bounds.Width - barWidth) / 2;
			int y = bounds.Y - 30;

			g.FillRectangle(Brushes.DimGray, x, y, barWidth, barHeight);
			float percent = Math.Max(0, (float)current / max);
			g.FillRectangle(isPlayer ? _healthGreenBrush : _healthRedBrush, x, y, barWidth * percent, barHeight);
			g.DrawRectangle(Pens.Black, x, y, barWidth, barHeight);
		}

		public void ShowDamage(int amount, Point pos, Color color)
		{
			string textToShow = amount == 0 ? "LEVEL UP!" : $"-{amount}";

			float xOffset = _knightRect.Width / 4f;

			_damageNumbers.Add(new FloatingText
			{
				Text = textToShow,
				Position = new PointF(pos.X + xOffset, pos.Y - 50),
				TextColor = color,
				Opacity = 1.0f
			});
		}

		private void ShowLootDrop()
		{
			int currentLevel = _settings.PlayerProgress.Level;

			_pendingLootItemFile = $"item_{currentLevel}.png";
			_pendingKnightStage = currentLevel;

			AssignCachedImage(ref _chestImg, AssetPaths.Items("chest_01.png"));
			_chestVisible = true;
			_lootVisible = false;

			// Position chest relative to monster
			float chestW = _chestRect.Width > 0 ? _chestRect.Width : 250f;
			float chestH = _chestRect.Height > 0 ? _chestRect.Height : 200f;
			_chestRect = new RectangleF(
				_monsterRect.X + (_monsterRect.Width / 4f),
				_monsterRect.Bottom - chestH,
				chestW,
				chestH);

			AnimateChestOpening();
		}

		/// <summary>Hides chest/loot sprites and resets loot animation state.</summary>
		private void ClearLootDisplay()
		{
			_chestVisible = false;
			_lootVisible = false;
			_isChestOpening = false;
			_chestShakeTicks = 0;
			_awaitingChestOpen = false;
			ClearCachedImage(ref _chestImg);
			ClearCachedImage(ref _lootImg);
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

			var r = Rectangle.Round(_chestRect);
			r.Inflate(20, 20);
			Invalidate(r);
		}

		private void FinalizeDiceLand()
		{
			LayoutCombat();

			_diceVisible = true;

			_txtAnswer.Enabled = true;
			_btnSubmit.Enabled = true;
			_txtAnswer.Focus();

			Invalidate(GetDiceArea());
		}

		private void ResetProgress()
		{
			var confirm = MessageBox.Show("Reset all progress for testing?", "Debug Reset", MessageBoxButtons.YesNo);
			if (confirm == DialogResult.Yes)
			{
				_settings.ResetProgress();
				Application.Restart();
				_isInternalClose = true;
				Environment.Exit(0);
			}
		}

		private void BtnSubmit_Click(object? sender, EventArgs e)
		{
			if (_session.CurrentPlayerHealth <= 0 || _isAnimating || !int.TryParse(_txtAnswer.Text, out int ans))
				return;

			var result = _session.ProcessAnswer(ans, _a, _b);

			if (result.IsCorrect)
			{
				AnimateMeleeStrike(ans);
			}
			else
			{
				_secondsRemaining = _session.CurrentMonsterAttackInterval;
				int damage = _a * _b;
				AnimateMonsterAttack(damage);
				GenerateQuestion();
			}
		}


		private void AnimateDiceRoll()
		{
			_isDicePhysicsActive = true;
			_isAnimating = true;
			_scrambleTicks = 0;

			float centerX = this.ClientSize.Width / 2f;
			float spacing = 180f;
			float floorY = this.ClientSize.Height * 0.15f;

			float[] targetX = { centerX - spacing, centerX + spacing, centerX };

			_diceCurrentPositions = new PointF[]
			{
				new PointF(targetX[0], -200),
				new PointF(targetX[1], -250),
				new PointF(targetX[2], -220)
			};

			for (int i = 0; i < 3; i++)
			{
				_diceVelocities[i] = new PointF(0, _rng.Next(25, 45));
				_diceRotationAngles[i] = _rng.Next(0, 360);
			}

			this.Invalidate(GetDiceArea());
		}

		// Heartbeat helpers ----------------------------------------------------

		private bool UpdateFloatingText(float dt, ref Rectangle dirty)
		{
			if (_damageNumbers.Count == 0) return false;

			bool removedAny = false;

			for (int i = _damageNumbers.Count - 1; i >= 0; i--)
			{
				_damageNumbers[i].Opacity -= 0.05f;
				if (_damageNumbers[i].Opacity <= 0f)
				{
					_damageNumbers.RemoveAt(i);
					removedAny = true;
				}
			}

			if (removedAny)
			{
				dirty = Rectangle.Union(dirty, GetCombatZone());
				return true;
			}

			return false;
		}

		private bool UpdateDicePhysics(float dt, ref Rectangle dirty)
		{
			_scrambleTicks++;
			bool isAnyDieMoving = false;

			float floorY = this.ClientSize.Height * 0.15f;

			Rectangle diceDirty = Rectangle.Empty;

			for (int i = 0; i < 3; i++)
			{
				var oldR = new Rectangle((int)_diceCurrentPositions[i].X, (int)_diceCurrentPositions[i].Y, (int)_diceSizePx, (int)_diceSizePx);

				_diceVelocities[i].Y += 3.5f;
				_diceCurrentPositions[i].Y += _diceVelocities[i].Y;

				if (_diceCurrentPositions[i].Y > floorY)
				{
					_diceCurrentPositions[i].Y = floorY;
					if (Math.Abs(_diceVelocities[i].Y) > 5.0f) _diceVelocities[i].Y *= -0.3f;
					else _diceVelocities[i].Y = 0;
				}

				if (_diceCurrentPositions[i].Y < floorY || Math.Abs(_diceVelocities[i].Y) > 0.1f)
				{
					_diceRotationAngles[i] += 25f;
					isAnyDieMoving = true;
				}

				var newR = new Rectangle((int)_diceCurrentPositions[i].X, (int)_diceCurrentPositions[i].Y, (int)_diceSizePx, (int)_diceSizePx);
				var union = Rectangle.Union(oldR, newR);
				diceDirty = diceDirty.IsEmpty ? union : Rectangle.Union(diceDirty, union);
			}

			dirty = dirty.IsEmpty ? diceDirty : Rectangle.Union(dirty, diceDirty);

			if (!isAnyDieMoving && _scrambleTicks > 20)
			{
				_isDicePhysicsActive = false;
				_isAnimating = false;

				FinalizeDiceLand();
			}

			return true;
		}
	}
}
