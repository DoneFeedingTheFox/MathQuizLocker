using System.Drawing;
using System.Windows.Forms;
using MathQuizLocker.Controls;
using MathQuizLocker.Services;



namespace MathQuizLocker
{
    public partial class QuizForm
    {
        private DoubleBufferedPictureBox _picKnight, _picMonster, _die1, _die2, _picMultiply, _picChest, _picLoot;
        private TextBox _txtAnswer;
        private Button _btnSubmit, _btnContinue, _btnExit;
        private Label _lblLevel, _lblXpStatus, _lblFeedback, _lblGameOver;
        private Button _btnReset, _btnRestart;

		// Initializes all UI components related to combat
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

			// Configure the timer object
			_countdownTimer.Interval = 1000; // 1 second
			_countdownTimer.Tick += CountdownTimer_Tick;
			// 1. Set Form Styles First
			this.UpdateStyles();
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.Bounds = Screen.PrimaryScreen.Bounds;
       
            this.BackColor = Color.FromArgb(30, 30, 30);



            // 2. Instantiate ALL objects FIRST (Prevents NullReference errors)
            _picKnight = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _picMonster = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _die1 = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _die2 = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _picMultiply = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _picChest = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, Visible = false, BackColor = Color.Transparent };
            _picLoot = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, Visible = false, BackColor = Color.Transparent };

            _lblLevel = new Label { ForeColor = Color.White, Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent };
            _lblXpStatus = new Label { ForeColor = Color.LightGray, Font = new Font("Segoe UI", 12), AutoSize = true, BackColor = Color.Transparent };
            _lblFeedback = new Label { ForeColor = Color.Gold, Font = new Font("Segoe UI", 24), AutoSize = true, BackColor = Color.Transparent };

            _btnSubmit = new Button { Text = "ATTACK", BackColor = Color.DarkRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            _btnReset = new Button { Text = "DEBUG: RESET", BackColor = Color.Gray, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            _btnContinue = new Button { Text = "CONTINUE", Visible = false, BackColor = Color.DarkGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 14, FontStyle.Bold) };
            _btnExit = new Button { Text = "EXIT TO DESKTOP", Visible = false, BackColor = Color.DimGray, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12) };

            _lblGameOver = new Label { Text = "GAME OVER", Font = new Font("Arial Black", 72, FontStyle.Bold), ForeColor = Color.Red, BackColor = Color.FromArgb(150, 0, 0, 0), TextAlign = ContentAlignment.MiddleCenter, Visible = false, AutoSize = false };
            _btnRestart = new Button { Text = "TRY AGAIN", Font = new Font("Segoe UI", 24, FontStyle.Bold), Size = new Size(300, 80),  BackColor = Color.DarkSlateGray, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Visible = false };

            _btnRestart.Click += (s, e) => RestartGame();

            this.Controls.Add(_lblGameOver);
            this.Controls.Add(_btnRestart);
            _lblGameOver.BringToFront();
            _btnRestart.BringToFront();



            // 3. Initialize TextBox and its Enter Key Logic
            _txtAnswer = new TextBox { Font = new Font("Segoe UI", 36), TextAlign = HorizontalAlignment.Center };

            _txtAnswer.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true; // Stops the "ding"
                    if (_btnSubmit.Visible && _btnSubmit.Enabled && !_isAnimating)
                    {
                        BtnSubmit_Click(_btnSubmit, EventArgs.Empty);
                    }
                }
            };

            // Extra silence for the Enter key
            _txtAnswer.KeyPress += (s, e) => {
                if (e.KeyChar == (char)Keys.Enter) e.Handled = true;
            };

            _txtAnswer.BringToFront();
            _btnSubmit.BringToFront();
            _lblFeedback.BringToFront();
   

            // 4. Add EVERYTHING to form
            this.Controls.AddRange(new Control[] {
        _picKnight, _picMonster, _die1, _die2, _picMultiply,
        _picChest, _picLoot, _txtAnswer, _btnSubmit, _btnReset,
        _btnContinue, _btnExit, _lblLevel, _lblXpStatus, _lblFeedback

    });
            _picKnight.SendToBack();
            _picMonster.SendToBack();

            // 5. Wire up the rest of the events
            _btnReset.Click += (s, e) => ResetProgress();
            _btnSubmit.Click += BtnSubmit_Click;

			_btnContinue.Click += (s, e) => {
				// 1. If we are currently showing a chest/loot, it's time for the story
				if (_awaitingChestOpen)
				{
					_btnContinue.Visible = _btnExit.Visible = _picChest.Visible = _picLoot.Visible = false;


					// Show the story overlay
					ShowStoryScreen();
				}
				else
				{
				
					SpawnMonster();
                 

                    _btnContinue.Visible = _btnExit.Visible = _picChest.Visible = _picLoot.Visible = false;
					_txtAnswer.Visible = _btnSubmit.Visible = true;
					_die1.Visible = _die2.Visible = _picMultiply.Visible = true;

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
            // Hide combat UI
            _txtAnswer.Visible = _btnSubmit.Visible = false;

            // Show Game Over UI
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

			_session.StartNewBattle(initialConfig.MaxHealth, initialConfig.XpReward);
			GenerateQuestion();

            _txtAnswer.Visible = _btnSubmit.Visible = true;
            _txtAnswer.Focus();
            this.Invalidate();
        }

		private void ShowVictoryScreen()
		{
			_awaitingChestOpen = false;
			bool wasBossFight = _currentMonsterName.ToLower().Contains("boss");

			int currentLevel = _settings.PlayerProgress.Level;
			int requiredXp = XpSystem.GetXpRequiredForNextLevel(currentLevel);
			int killReward = _session.CurrentBattleXpReward;

			// --- DEBUG BLOCK ---
			System.Diagnostics.Debug.WriteLine($"--- VICTORY DEBUG ---");
			System.Diagnostics.Debug.WriteLine($"Monster: {_currentMonsterName} (Boss: {wasBossFight})");
			System.Diagnostics.Debug.WriteLine($"XP Before: {_settings.PlayerProgress.CurrentXp}");
			System.Diagnostics.Debug.WriteLine($"XP Reward: {killReward}");
			System.Diagnostics.Debug.WriteLine($"XP Required for Level {currentLevel}: {requiredXp}");
			// -------------------

			_settings.PlayerProgress.CurrentXp += killReward;

			// 2. Logic Branching
			if (wasBossFight)
			{
		
				_settings.PlayerProgress.Level++;
				_settings.PlayerProgress.CurrentXp = 0; // Reset or carry over

				if (_settings.MaxFactorUnlocked < 10) _settings.MaxFactorUnlocked++;

				_awaitingChestOpen = true; // This triggers the chest
				System.Diagnostics.Debug.WriteLine("EVENT: Boss Defeated! Leveling Up.");
			}
			else if (_settings.PlayerProgress.CurrentXp >= requiredXp)
				{
					// We have enough XP to fight the boss! 
					// Do NOT level up yet, and do NOT show loot.
					// Just let the player click "Continue" to encounter the boss.
				
					System.Diagnostics.Debug.WriteLine("EVENT: XP Goal Reached! Next monster will be a BOSS.");
				}
			

			System.Diagnostics.Debug.WriteLine($"XP After: {_settings.PlayerProgress.CurrentXp}");
			System.Diagnostics.Debug.WriteLine($"----------------------");

			UpdatePlayerHud();
			AppSettings.Save(_settings);

			// 3. UI Transition
			_txtAnswer.Visible = _btnSubmit.Visible = _die1.Visible = _die2.Visible = _picMultiply.Visible = false;

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

            // 1. Skjul HUD og kamp-visualiseringer
            _picKnight.Image = null;
            _picMonster.Image = null;
            _lblLevel.Visible = false;
            _lblXpStatus.Visible = false;

            // 2. Last inn tekstinnhold basert på nivå
            _lblStoryText.Text = LocalizationService.GetStory(_settings.PlayerProgress.Level);

            // 3. Dynamisk posisjonering
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            int textWidth = (int)(w * 0.65);
            int textHeight = (int)(h * 0.15);
            _lblStoryText.Size = new Size(textWidth, textHeight);
            _lblStoryText.Location = new Point((w - textWidth) / 2, (int)(h * 0.74));

            _lblStoryText.Font = new Font("Palatino Linotype", 18, FontStyle.Bold);
            _lblStoryText.ForeColor = Color.FromArgb(45, 30, 15);

            // --- LOGIKK FOR TVUNGEN INTRO ---
            // Sjekk om dette er den aller første introen
            bool isFirstIntro = (_settings.PlayerProgress.Level == 1 && _settings.PlayerProgress.CurrentXp == 0);

            if (isFirstIntro)
            {
                // Sentrer "Fortsett"-knappen siden den er alene
                _btnStoryContinue.Location = new Point((w - _btnStoryContinue.Width) / 2, (int)(h * 0.92));
                _btnStoryExit.Visible = false;
            }
            else
            {
                // Normal plassering med to knapper
                _btnStoryContinue.Location = new Point(w / 2 - _btnStoryContinue.Width - 10, (int)(h * 0.92));
                _btnStoryExit.Location = new Point(w / 2 + 10, (int)(h * 0.92));
                _btnStoryExit.Visible = true;
            }

            // 4. Endelig synlighet
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

            // Sprite Positioning
            _picKnight.Size = new Size((int)(350 * scale), (int)(450 * scale));
            _picKnight.Location = new Point((int)(w * 0.20), (int)(h * 0.85 - _picKnight.Height));

            _picMonster.Size = new Size((int)(450 * scale), (int)(550 * scale));
            _picMonster.Location = new Point((int)(w * 0.65), (int)(h * 0.90 - _picMonster.Height));

			// Timer Positioning
			_lblTimer.Location = new Point(w / 2 - _lblTimer.Width / 2, _picMultiply.Bottom + 20);

			// Dice & Math Sign Positioning (Final Landing Spots)
			int diceSize = (int)(120 * scale);

            _picMultiply.Size = new Size(diceSize, diceSize);
            _picMultiply.Location = new Point(w / 2 - _picMultiply.Width / 2, (int)(h * 0.15));

            _die1.Size = new Size(diceSize, diceSize);
            _die1.Location = new Point(_picMultiply.Left - _die1.Width - 40, _picMultiply.Top);

            _die2.Size = new Size(diceSize, diceSize);
            _die2.Location = new Point(_picMultiply.Right + 40, _picMultiply.Top);

            // Loot Positioning
            _picChest.Size = new Size((int)(250 * scale), (int)(200 * scale));
            _picChest.Location = new Point(_picMonster.Left, _picMonster.Bottom - _picChest.Height);
            _picLoot.Size = new Size((int)(80 * scale), (int)(80 * scale));
            _picLoot.Location = new Point(_picChest.Right - (int)(40 * scale), _picChest.Bottom - (int)(100 * scale));

            // Input UI Positioning
            _txtAnswer.Size = new Size((int)(220 * scale), (int)(80 * scale));
            _txtAnswer.Location = new Point(w / 2 - _txtAnswer.Width / 2, h - (int)(250 * scale));

            _btnSubmit.Size = new Size((int)(180 * scale), (int)(60 * scale));
            _btnSubmit.Location = new Point(w / 2 - _btnSubmit.Width / 2, _txtAnswer.Bottom + 10);

            // HUD Positioning
            _lblLevel.Location = new Point((int)(50 * scale), (int)(50 * scale));
            _lblXpStatus.Location = new Point(_lblLevel.Left, _lblLevel.Bottom + (int)(10 * scale));

            // Victory Buttons Positioning (Stacked in the center)
            _btnContinue.Size = new Size((int)(250 * scale), (int)(70 * scale));
            _btnContinue.Location = new Point(w / 2 - _btnContinue.Width / 2, h / 2);

            _btnExit.Size = new Size((int)(250 * scale), (int)(50 * scale));
            _btnExit.Location = new Point(w / 2 - _btnExit.Width / 2, _btnContinue.Bottom + 20);

            this.Invalidate();
        }
    }
}