using System.Drawing;
using System.Windows.Forms;
using MathQuizLocker.Controls;



namespace MathQuizLocker
{
    public partial class QuizForm
    {
        private DoubleBufferedPictureBox _picKnight, _picMonster, _die1, _die2, _picMultiply, _picChest, _picLoot;
        private TextBox _txtAnswer;
        private Button _btnSubmit, _btnContinue, _btnExit;
        private Label _lblLevel, _lblXpStatus, _lblFeedback, _lblGameOver;
        private Button _btnReset, _btnRestart;

        private void InitializeCombatUi()
        {
            // 1. Set Form Styles First
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            this.DoubleBuffered = true;
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
                _session.StartNewBattle();
            
                _btnContinue.Visible = _btnExit.Visible = _picChest.Visible = _picLoot.Visible = false;
                _txtAnswer.Visible = _btnSubmit.Visible = true;
                _die1.Visible = _die2.Visible = _picMultiply.Visible = true; // Ensure these stay true

                GenerateQuestion();
                LayoutCombat();
                this.Invalidate();
            };

            _btnExit.Click += (s, e) =>
            {
                AppSettings.Save(_settings);
                Environment.Exit(0);
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

            _session.StartNewBattle(); // Resets health and monsters
            GenerateQuestion();

            _txtAnswer.Visible = _btnSubmit.Visible = true;
            _txtAnswer.Focus();
            this.Invalidate();
        }

        private void ShowVictoryScreen()
        {
          
     

            // 1. Calculate XP Reward (60% of current level requirement)
            int requiredXp = XpSystem.GetXpRequiredForNextLevel(_settings.PlayerProgress.Level);
            int killReward = (int)(requiredXp * 0.6);

            _settings.PlayerProgress.CurrentXp += killReward;

            // 2. Check for Level Up
            if (_settings.PlayerProgress.CurrentXp >= requiredXp)
            {
                _settings.PlayerProgress.CurrentXp -= requiredXp;
                _settings.PlayerProgress.Level++;
                _awaitingChestOpen = true; // Signals that we should drop loot
            }

            // 3. Update HUD to show new XP/Level
            UpdatePlayerHud();
            AppSettings.Save(_settings);

            // 4. Transition UI
            _txtAnswer.Visible = _btnSubmit.Visible = _die1.Visible = _die2.Visible = _picMultiply.Visible = false;

            if (_awaitingChestOpen)
            {
                ShowLootDrop();
            }
            else
            {
                // If they didn't level up yet, just show the continue buttons
                _btnContinue.Visible = true;
                _btnExit.Visible = true;
                _btnContinue.Focus();
            }

            LayoutCombat();
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