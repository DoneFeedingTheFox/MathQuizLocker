using System;
using System.Drawing;
using System.Windows.Forms;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
	public partial class QuizForm
	{
		private TextBox _txtAnswer;
		private Button _btnSubmit, _btnContinue, _btnExit;
		private Label _lblLevel, _lblXpStatus, _lblFeedback, _lblGameOver;
		private Button _btnReset, _btnRestart;

		/// <summary>Creates and arranges combat controls: answer box, ATTACK, level/XP labels, continue/exit, timer. DEBUG reset only in Debug build.</summary>
		private void InitializeCombatUi()
		{
			_lblTimer = new Label
			{
				Name = "_lblTimer",
				ForeColor = Color.FromArgb(60, 40, 20),
				Font = new Font("Palatino Linotype", 48, FontStyle.Bold),
				TextAlign = ContentAlignment.MiddleCenter,
				BackColor = Color.Transparent,
				Visible = false,
				AutoSize = true
			};
			this.Controls.Add(_lblTimer);
			_lblTimer.BringToFront();

			// Form config
			this.UpdateStyles();
			this.SetStyle(ControlStyles.UserPaint, true);
			this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);

			this.FormBorderStyle = FormBorderStyle.None;
			this.WindowState = FormWindowState.Maximized;
			this.TopMost = true;
			this.Bounds = Screen.PrimaryScreen.Bounds;
			this.BackColor = Color.FromArgb(30, 30, 30);

			_lblLevel = new Label
			{
				ForeColor = Color.White,
				Font = new Font("Segoe UI", 18, FontStyle.Bold),
				AutoSize = true,
				BackColor = Color.Transparent
			};

			_lblXpStatus = new Label
			{
				ForeColor = Color.LightGray,
				Font = new Font("Segoe UI", 12),
				AutoSize = true,
				BackColor = Color.Transparent
			};

			_lblFeedback = new Label
			{
				ForeColor = Color.Gold,
				Font = new Font("Segoe UI", 24),
				AutoSize = true,
				BackColor = Color.Transparent
			};

			_btnSubmit = new Button
			{
				Text = "ATTACK",
				BackColor = Color.DarkRed,
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat
			};

			_btnReset = new Button
			{
				Text = "DEBUG: RESET",
				BackColor = Color.Gray,
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat
			};

			_btnContinue = new Button
			{
				Text = "CONTINUE",
				Visible = false,
				BackColor = Color.DarkGreen,
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Segoe UI", 14, FontStyle.Bold)
			};

			_btnExit = new Button
			{
				Text = "EXIT TO DESKTOP",
				Visible = false,
				BackColor = Color.DimGray,
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Segoe UI", 12)
			};

			_lblGameOver = new Label
			{
				Text = "GAME OVER",
				Font = new Font("Arial Black", 72, FontStyle.Bold),
				ForeColor = Color.Red,
				BackColor = Color.FromArgb(150, 0, 0, 0),
				TextAlign = ContentAlignment.MiddleCenter,
				Visible = false,
				AutoSize = false
			};

			_btnRestart = new Button
			{
				Text = "TRY AGAIN",
				Font = new Font("Segoe UI", 24, FontStyle.Bold),
				Size = new Size(300, 80),
				BackColor = Color.DarkSlateGray,
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Visible = false
			};

			_btnRestart.Click += (s, e) => RestartGame();

			this.Controls.Add(_lblGameOver);
			this.Controls.Add(_btnRestart);
			_lblGameOver.BringToFront();
			_btnRestart.BringToFront();

			// Answer box
			_txtAnswer = new TextBox
			{
				Font = new Font("Segoe UI", 36),
				TextAlign = HorizontalAlignment.Center
			};

			_txtAnswer.KeyDown += (s, e) =>
			{
				if (e.KeyCode == Keys.Enter)
				{
					e.SuppressKeyPress = true;
					if (_btnSubmit.Visible && _btnSubmit.Enabled && !_isAnimating)
						BtnSubmit_Click(_btnSubmit, EventArgs.Empty);
				}
			};

			_txtAnswer.KeyPress += (s, e) =>
			{
				if (e.KeyChar == (char)Keys.Enter) e.Handled = true;
			};

			this.Controls.AddRange(new Control[]
			{
				_txtAnswer, _btnSubmit, _btnReset, _btnContinue, _btnExit, _lblLevel, _lblXpStatus, _lblFeedback
			});

			_txtAnswer.BringToFront();
			_btnSubmit.BringToFront();
			_lblFeedback.BringToFront();

#if !DEBUG
			_btnReset.Visible = false;
#endif
			_btnReset.Click += (s, e) => ResetProgress();
			_btnSubmit.Click += BtnSubmit_Click;

			_btnContinue.Click += (s, e) =>
			{
				if (_awaitingChestOpen)
				{
					_btnContinue.Visible = _btnExit.Visible = false;
					_chestVisible = _lootVisible = false;

					ShowStoryScreen();
				}
				else
				{
					SpawnMonster();
					_btnContinue.Visible = _btnExit.Visible = false;
					_txtAnswer.Visible = _btnSubmit.Visible = true;

					_diceVisible = true;

					GenerateQuestion();
					LayoutCombat();
					this.Invalidate();
				}
			};

			_btnExit.Click += (s, e) =>
			{
				AppSettings.Save(_settings);
				Application.Exit();
			};
		}

		private void ShowGameOverScreen()
		{
			_txtAnswer.Visible = _btnSubmit.Visible = false;

			_lblGameOver.Bounds = this.ClientRectangle;
			_lblGameOver.Visible = true;

			_btnRestart.Location = new Point(
				(this.Width - _btnRestart.Width) / 2,
				(this.Height / 2) + 100
			);
			_btnRestart.Visible = true;
			_btnRestart.Focus();
		}

		private void RestartGame()
		{
			_lblGameOver.Visible = false;
			_btnRestart.Visible = false;

			var initialConfig = _monsterService.GetMonster("goblin");
			_session.StartNewBattle(initialConfig);
			GenerateQuestion();

			_txtAnswer.Visible = _btnSubmit.Visible = true;
			_txtAnswer.Focus();
			this.Invalidate();
		}

		/// <summary>Called when monster HP reaches 0: applies level-up on boss kill, shows loot/story or continue button.</summary>
		private void ShowVictoryScreen()
		{
			_awaitingChestOpen = false;
			bool wasBossFight = _currentMonsterName.ToLower().Contains("boss");

			int currentLevel = _settings.PlayerProgress.Level;
			int requiredXp = XpSystem.GetXpRequiredForNextLevel(currentLevel);
			int killReward = _session.CurrentBattleXpReward;

			System.Diagnostics.Debug.WriteLine($"--- VICTORY DEBUG ---");
			System.Diagnostics.Debug.WriteLine($"Monster: {_currentMonsterName} (Boss: {wasBossFight})");
			System.Diagnostics.Debug.WriteLine($"XP Before: {_settings.PlayerProgress.CurrentXp}");
			System.Diagnostics.Debug.WriteLine($"XP Reward: {killReward}");
			System.Diagnostics.Debug.WriteLine($"XP Required for Level {currentLevel}: {requiredXp}");
			System.Diagnostics.Debug.WriteLine($"----------------------");

			// XP already added by GameSessionManager.ApplyDamage via XpSystem.AddXp; do not add again.

			if (wasBossFight)
			{
				_settings.PlayerProgress.Level++;
				_settings.PlayerProgress.CurrentXp = 0;

				_quizEngine.PromoteToNextLevel();

				_awaitingChestOpen = true;
				System.Diagnostics.Debug.WriteLine("EVENT: Boss Defeated! Leveling Up.");
			}
			else if (_settings.PlayerProgress.CurrentXp >= requiredXp)
			{
				System.Diagnostics.Debug.WriteLine("EVENT: XP Goal Reached! Next monster will be a BOSS.");
			}

			UpdatePlayerHud();
			AppSettings.Save(_settings);

			// UI transition
			_txtAnswer.Visible = _btnSubmit.Visible = false;
			_diceVisible = false;

			if (_awaitingChestOpen)
			{
				_btnContinue.Visible = false;
				_btnExit.Visible = false;
				ShowLootDrop();
			}
			else
			{
				_btnContinue.Visible = true;
				_btnExit.Visible = false;
			}

			LayoutCombat();
		}

		private void ShowStoryScreen()
		{
			_isShowingStory = true;
			ApplyBiomeForCurrentLevel();

			_lblLevel.Visible = false;
			_lblXpStatus.Visible = false;

			ReplaceImage(ref _knightImg, null);
			ReplaceImage(ref _monsterImg, null);

			_lblStoryText.Text = LocalizationService.GetStory(_settings.PlayerProgress.Level);

			int w = this.ClientSize.Width;
			int h = this.ClientSize.Height;

			int textWidth = (int)(w * 0.65);
			int textHeight = (int)(h * 0.15);
			_lblStoryText.Size = new Size(textWidth, textHeight);
			_lblStoryText.Location = new Point((w - textWidth) / 2, (int)(h * 0.74));

			_lblStoryText.Font = new Font("Palatino Linotype", 18, FontStyle.Bold);
			_lblStoryText.ForeColor = Color.FromArgb(45, 30, 15);

			bool isFirstIntro = (_settings.PlayerProgress.Level == 1 && _settings.PlayerProgress.CurrentXp == 0);

			if (isFirstIntro)
			{
				_btnStoryContinue.Location = new Point((w - _btnStoryContinue.Width) / 2, (int)(h * 0.92));
				_btnStoryExit.Visible = false;
			}
			else
			{
				_btnStoryContinue.Location = new Point(w / 2 - _btnStoryContinue.Width - 10, (int)(h * 0.92));
				_btnStoryExit.Location = new Point(w / 2 + 10, (int)(h * 0.92));
				_btnStoryExit.Visible = true;
			}

			_lblStoryText.Visible = true;
			_btnStoryContinue.Visible = true;

			_lblStoryText.BringToFront();
			_btnStoryContinue.BringToFront();
			if (!isFirstIntro) _btnStoryExit.BringToFront();

			this.Invalidate();
		}

		public void LayoutCombat()
		{
			if (this.ClientSize.Width == 0 || this.ClientSize.Height == 0) return;

			float scale = this.ClientSize.Height / 1080f;
			int w = this.ClientSize.Width;
			int h = this.ClientSize.Height;

			float knightSize = 450f * scale;
			_knightRect = new RectangleF(w * 0.20f, (h * 0.85f) - knightSize, knightSize, knightSize);

			float monsterSize = 450f * scale;
			_monsterRect = new RectangleF(w * 0.65f, (h * 0.90f) - monsterSize, monsterSize, monsterSize);

			RecalcKnightDrawRect();
			RecalcMonsterDrawRect();

			_lblTimer.Location = new Point(w / 2 - _lblTimer.Width / 2, (int)(h * 0.15f) + (int)(140 * scale));

			_diceSizePx = 120f * scale;

			_mulRect = new RectangleF(w / 2f - _diceSizePx / 2f, h * 0.15f, _diceSizePx, _diceSizePx);
			_die1Rect = new RectangleF(_mulRect.Left - _diceSizePx - 40f, _mulRect.Top, _diceSizePx, _diceSizePx);
			_die2Rect = new RectangleF(_mulRect.Right + 40f, _mulRect.Top, _diceSizePx, _diceSizePx);

			_chestRect = new RectangleF(_monsterRect.X, _monsterRect.Bottom - (200f * scale), 250f * scale, 200f * scale);
			_lootRect = new RectangleF(_chestRect.X + (_chestRect.Width / 2f) + 5f, _chestRect.Y + (_chestRect.Height / 4f),
									   120f * scale, 120f * scale);

			_txtAnswer.Size = new Size((int)(220 * scale), (int)(80 * scale));
			_txtAnswer.Location = new Point(w / 2 - _txtAnswer.Width / 2, h - (int)(250 * scale));

			_btnSubmit.Size = new Size((int)(180 * scale), (int)(60 * scale));
			_btnSubmit.Location = new Point(w / 2 - _btnSubmit.Width / 2, _txtAnswer.Bottom + 10);

			_btnContinue.Size = new Size((int)(250 * scale), (int)(70 * scale));
			_btnContinue.Location = new Point(w / 2 - _btnContinue.Width / 2, h / 2);

			_btnExit.Size = new Size((int)(250 * scale), (int)(50 * scale));
			_btnExit.Location = new Point(w / 2 - _btnExit.Width / 2, _btnContinue.Bottom + 20);

			this.Invalidate();
		}
	}
}
