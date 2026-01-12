using System;
using System.Drawing;
using System.Windows.Forms;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
    public partial class QuizForm
    {
        private PictureBox _picKnight = null!, _picMonster = null!, _die1 = null!, _die2 = null!, _picMultiply = null!;
        private FantasyXpBar _monsterHealthBar = null!, _playerHealthBar = null!, _playerXpBar = null!;
        private Label _lblFeedback = null!, _lblLevel = null!, _lblXpStatus = null!, _lblVersion = null!;
        private TextBox _txtAnswer = null!;
        private Button _btnSubmit = null!, _btnReset = null!, _btnExit = null!, _btnContinue = null!;

        private void InitializeCombatUi()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ShowInTaskbar = false;

            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.UpdateStyles();

            this.Deactivate += (s, e) => { if (!this.IsDisposed && !_isInternalClose) { this.Activate(); this.Focus(); } };

            this.KeyDown += (s, e) => {
                if (e.Control && e.Shift && e.Alt && e.KeyCode == Keys.End) { _isInternalClose = true; this.Close(); }
                if (e.KeyCode == Keys.Enter && _btnSubmit.Visible) { e.SuppressKeyPress = true; BtnSubmit_Click(null!, null!); }
            };

            _picKnight = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _picMonster = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _die1 = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _die2 = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _picMultiply = new DoubleBufferedPictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };

            _picKnight.Parent = _picMonster.Parent = this;

            _monsterHealthBar = new FantasyXpBar();
            _playerHealthBar = new FantasyXpBar();
            _playerXpBar = new FantasyXpBar();

            _lblLevel = new Label { Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, BackColor = Color.Black };
            _lblXpStatus = new Label { Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.Aqua, AutoSize = true, BackColor = Color.Transparent };
            _lblFeedback = new Label { Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = Color.Yellow, AutoSize = true, BackColor = Color.Transparent };
            _txtAnswer = new TextBox { Font = new Font("Segoe UI", 36), TextAlign = HorizontalAlignment.Center, BackColor = Color.FromArgb(245, 240, 220), BorderStyle = BorderStyle.None };

            _btnSubmit = new Button { Text = "ATTACK", FlatStyle = FlatStyle.Flat, BackColor = Color.DarkRed, ForeColor = Color.White, Font = new Font("Segoe UI", 16, FontStyle.Bold) };
            _btnSubmit.Click += (s, e) => BtnSubmit_Click(s, e);

            
            _btnReset = new Button { Text = "RESET", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, Font = new Font("Segoe UI", 8) };
            _btnReset.Click += (s, e) =>
            {
                if (MessageBox.Show("Reset to Level 1?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _settings.ResetProgress();
                    ResetBattleState();
                }
            };

            _btnContinue = new Button { Text = "CONTINUE FIGHTING", FlatStyle = FlatStyle.Flat, BackColor = Color.ForestGreen, ForeColor = Color.White, Font = new Font("Segoe UI", 18, FontStyle.Bold), Visible = false };
            _btnContinue.Click += (s, e) => ResetForNextFight();

            _btnExit = new Button { Text = "EXIT TO DESKTOP", FlatStyle = FlatStyle.Flat, BackColor = Color.Goldenrod, ForeColor = Color.Black, Font = new Font("Segoe UI", 18, FontStyle.Bold), Visible = false };
            _btnExit.Click += (s, e) => { _isInternalClose = true; this.Close(); };

            _lblVersion = new Label { Text = $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}", Font = new Font("Segoe UI", 9, FontStyle.Italic), ForeColor = Color.Gray, AutoSize = true, BackColor = Color.Transparent };

            try
            {
                this.BackgroundImage = LoadImageNoLock(AssetPaths.Background("Background.png"));
                this.BackgroundImageLayout = ImageLayout.Stretch;
                _picMultiply.Image = LoadImageNoLock(AssetPaths.Dice("multiply.png"));
            }
            catch { }

            // Added _btnReset to the collection below
            this.Controls.AddRange(new Control[] { _picKnight, _picMonster, _die1, _picMultiply, _die2, _monsterHealthBar, _playerHealthBar, _playerXpBar, _lblLevel, _lblXpStatus, _txtAnswer, _btnSubmit, _lblFeedback, _btnReset, _btnExit, _btnContinue, _lblVersion });
            this.Resize += (s, e) => LayoutCombat();
        }

        private void LayoutCombat()
        {
            if (this.ClientSize.Width == 0 || this.ClientSize.Height == 0 || _isAnimating) return;
            float scale = this.ClientSize.Height / 1080f;
            int w = this.ClientSize.Width, h = this.ClientSize.Height;

            _picKnight.Size = new Size((int)(350 * scale), (int)(450 * scale));
            _picKnight.Location = new Point((int)(w * 0.20), (int)(h * 0.87 - _picKnight.Height));

            _picMonster.Size = new Size((int)(450 * scale), (int)(550 * scale));
            _picMonster.Location = new Point((int)(w * 0.60), (int)(h * 0.95 - _picMonster.Height));

            _playerHealthBar.Size = _monsterHealthBar.Size = new Size((int)(300 * scale), (int)(25 * scale));
            _playerHealthBar.Location = new Point(_picKnight.Left + (_picKnight.Width / 2 - 150), _picKnight.Top - 40);
            _monsterHealthBar.Location = new Point(_picMonster.Left + (_picMonster.Width / 2 - 150), _picMonster.Top - 40);

            _txtAnswer.Width = (int)(220 * scale);
            _txtAnswer.Location = new Point(w / 2 - _txtAnswer.Width / 2, h - (int)(220 * scale));
            _btnSubmit.Size = new Size((int)(180 * scale), (int)(60 * scale));
            _btnSubmit.Location = new Point(w / 2 - _btnSubmit.Width / 2, _txtAnswer.Bottom + 10);

            _die1.Size = _die2.Size = new Size((int)(120 * scale), (int)(120 * scale));
            _die1.Location = new Point(w / 2 - _die1.Width - 50, (int)(60 * scale));
            _die2.Location = new Point(w / 2 + 50, (int)(60 * scale));
            _picMultiply.Size = new Size((int)(80 * scale), (int)(80 * scale));
            _picMultiply.Location = new Point(w / 2 - 40, _die1.Top + 20);

            _lblFeedback.Location = new Point(w / 2 - _lblFeedback.Width / 2, _die1.Bottom + 20);

            // --- RESET BUTTON LAYOUT ---
            _btnReset.Size = new Size((int)(100 * scale), (int)(30 * scale));
            _btnReset.Location = new Point(w - _btnReset.Width - 20, h - _btnReset.Height - 20);
        }

        private void ShowVictoryScreen()
        {
            _monsterHealthBar.Value = 0;
            _lblFeedback.Text = "MONSTER DEFEATED!";
            _lblFeedback.ForeColor = Color.Gold;
            _txtAnswer.Visible = _btnSubmit.Visible = _die1.Visible = _die2.Visible = _picMultiply.Visible = false;
            _btnContinue.Visible = _btnExit.Visible = true;
            _btnContinue.Focus();
        }

        private void ResetForNextFight()
        {
            _btnContinue.Visible = _btnExit.Visible = false;
            _txtAnswer.Visible = _btnSubmit.Visible = _die1.Visible = _die2.Visible = _picMultiply.Visible = true;
            SpawnMonster();
            GenerateQuestion();
            _lblFeedback.Text = "A NEW FOE APPEARS!";
            _lblFeedback.ForeColor = Color.Yellow;
            _txtAnswer.Focus();
        }
    }
}