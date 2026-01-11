using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using MathQuizLocker.Services;
using Velopack;
using Velopack.Sources;

namespace MathQuizLocker
{
	public class QuizForm : Form
	{
		private readonly AppSettings _settings;
		private readonly QuizEngine _quizEngine;
		private readonly GameSessionManager _session;

		// Combat State
		private int _a, _b, _monsterHealth, _maxMonsterHealth;
		private int _playerHealth, _maxPlayerHealth = 100;
		private bool _isInternalClose = false;

		// UI Controls
		private PictureBox _picKnight = null!;
		private PictureBox _picMonster = null!;
		private PictureBox _die1 = null!, _die2 = null!;
		private FantasyXpBar _monsterHealthBar = null!;
		private FantasyXpBar _playerHealthBar = null!;
		private FantasyXpBar _playerXpBar = null!;
		private Label _lblFeedback = null!, _lblLevel = null!, _lblXpStatus = null!;
		private TextBox _txtAnswer = null!;
		private Button _btnSubmit = null!, _btnReset = null!, _btnExit = null!, _btnContinue = null!;
		private Label _lblVersion = null!; //

		public QuizForm(AppSettings settings)
		{
			_settings = settings ?? new AppSettings();
			_quizEngine = new QuizEngine(_settings);
			_session = new GameSessionManager(_settings, _quizEngine);
			_playerHealth = _maxPlayerHealth;

			InitializeCombatUi();
		}

		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams cp = base.CreateParams;
				cp.Style &= ~0x00080000; // WS_SYSMENU - Removes right-click close
				return cp;
			}
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			_ = UpdateMyApp();
			SpawnMonster();
			GenerateQuestion();
			UpdatePlayerStats();
			LayoutCombat();
		}

		private void InitializeCombatUi()
		{
			this.FormBorderStyle = FormBorderStyle.None;
			this.WindowState = FormWindowState.Maximized;
			this.TopMost = true;
			this.DoubleBuffered = true;
			this.KeyPreview = true;
			this.BackColor = Color.FromArgb(30, 30, 30);
			this.ShowInTaskbar = false;

			this.Deactivate += (s, e) => {
				if (!this.IsDisposed && !_isInternalClose) { this.Activate(); this.Focus(); }
			};

			this.KeyDown += (s, e) => {
				if (e.Control && e.Shift && e.Alt && e.KeyCode == Keys.End)
				{
					_isInternalClose = true;
					this.Close();
				}

				if (e.KeyCode == Keys.Enter && _btnSubmit.Visible)
				{
					e.Handled = true;
					BtnSubmit_Click(null!, null!);
				}
			};

			_picKnight = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
			_picMonster = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
			_die1 = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
			_die2 = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };

			_monsterHealthBar = new FantasyXpBar();
			_playerHealthBar = new FantasyXpBar();
			_playerXpBar = new FantasyXpBar();

			_lblLevel = new Label { Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, BackColor = Color.Black };
			_lblXpStatus = new Label { Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.Aqua, AutoSize = true, BackColor = Color.Transparent };
			_lblFeedback = new Label { Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = Color.Yellow, AutoSize = true, BackColor = Color.Transparent };

			_txtAnswer = new TextBox { Font = new Font("Segoe UI", 36), TextAlign = HorizontalAlignment.Center, BackColor = Color.FromArgb(245, 240, 220), BorderStyle = BorderStyle.None, Multiline = false };

			_btnSubmit = new Button { Text = "ATTACK", FlatStyle = FlatStyle.Flat, BackColor = Color.DarkRed, ForeColor = Color.White, Font = new Font("Segoe UI", 16, FontStyle.Bold) };
			_btnSubmit.Click += (s, e) => BtnSubmit_Click(s, e);

			_btnReset = new Button { Text = "RESET", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, Font = new Font("Segoe UI", 8) };
			_btnReset.Click += (s, e) => {
				if (MessageBox.Show("Reset to Level 1?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes) { _settings.ResetProgress(); ResetBattleState(); }
			};

			_btnContinue = new Button { Text = "CONTINUE FIGHTING", FlatStyle = FlatStyle.Flat, BackColor = Color.ForestGreen, ForeColor = Color.White, Font = new Font("Segoe UI", 18, FontStyle.Bold), Visible = false };
			_btnContinue.Click += (s, e) => ResetForNextFight();

			_btnExit = new Button { Text = "EXIT TO DESKTOP", FlatStyle = FlatStyle.Flat, BackColor = Color.Goldenrod, ForeColor = Color.Black, Font = new Font("Segoe UI", 18, FontStyle.Bold), Visible = false };
			_btnExit.Click += (s, e) => { _isInternalClose = true; this.Close(); };

			// Version Label Initialization
			_lblVersion = new Label
			{
				Text = $"v{Application.ProductVersion}",
				Font = new Font("Segoe UI", 9, FontStyle.Italic),
				ForeColor = Color.Gray,
				AutoSize = true,
				BackColor = Color.Transparent
			};

			try
			{
				string bgPath = AssetPaths.Background("Background.png");
				if (File.Exists(bgPath)) { this.BackgroundImage = Image.FromFile(bgPath); this.BackgroundImageLayout = ImageLayout.Stretch; }
			}
			catch { }

			this.Controls.AddRange(new Control[] { _picKnight, _picMonster, _die1, _die2, _monsterHealthBar, _playerHealthBar, _playerXpBar, _lblLevel, _lblXpStatus, _txtAnswer, _btnSubmit, _lblFeedback, _btnReset, _btnExit, _btnContinue, _lblVersion });
			this.Resize += (s, e) => LayoutCombat();
		}

		private void LayoutCombat()
		{
			if (this.ClientSize.Width == 0 || this.ClientSize.Height == 0) return;
			int w = this.ClientSize.Width, h = this.ClientSize.Height;
			float scale = h / 1080f;

			// Dice Scaling
			int dSize = (int)(120 * scale);
			_die1.Size = _die2.Size = new Size(dSize, dSize);
			_die1.Location = new Point(w / 2 - dSize - 10, (int)(60 * scale));
			_die2.Location = new Point(w / 2 + 10, (int)(60 * scale));
			_lblFeedback.Location = new Point(w / 2 - _lblFeedback.Width / 2, _die1.Bottom + (int)(20 * scale));

			// Sprite Scaling & Grounding
			int sW = (int)(350 * scale), sH = (int)(450 * scale);
			_picKnight.Size = _picMonster.Size = new Size(sW, sH);
			_picKnight.Location = new Point((int)(w * 0.20), (int)(h * 0.75 - sH));
			_picMonster.Location = new Point((int)(w * 0.60), (int)(h * 0.75 - sH));

			// Status Bar Scaling
			int bW = (int)(300 * scale), bH = (int)(25 * scale);
			_playerHealthBar.Size = _monsterHealthBar.Size = new Size(bW, bH);
			_playerHealthBar.Location = new Point(_picKnight.Left + (sW / 2 - bW / 2), _picKnight.Top - (int)(40 * scale));
			_monsterHealthBar.Location = new Point(_picMonster.Left + (sW / 2 - bW / 2), _picMonster.Top - (int)(40 * scale));

			// Bottom UI Scaling
			_playerXpBar.Size = new Size((int)(400 * scale), (int)(15 * scale));
			_playerXpBar.Location = new Point(w / 2 - _playerXpBar.Width / 2, h - (int)(60 * scale));
			_lblXpStatus.Location = new Point(_playerXpBar.Left, _playerXpBar.Top - (int)(20 * scale));
			_lblLevel.Location = new Point(20, h - (int)(40 * scale));

			// Input Scaling
			_txtAnswer.Width = (int)(220 * scale);
			_txtAnswer.Location = new Point(w / 2 - _txtAnswer.Width / 2, _playerXpBar.Top - (int)(140 * scale));
			_btnSubmit.Size = new Size((int)(180 * scale), (int)(60 * scale));
			_btnSubmit.Location = new Point(w / 2 - _btnSubmit.Width / 2, _txtAnswer.Bottom + 10);

			// Victory Screen Buttons
			_btnContinue.Size = _btnExit.Size = new Size((int)(350 * scale), (int)(80 * scale));
			_btnContinue.Location = new Point(w / 2 - _btnContinue.Width / 2, h / 2 - (int)(50 * scale));
			_btnExit.Location = new Point(w / 2 - _btnExit.Width / 2, _btnContinue.Bottom + 20);

			_btnReset.Size = new Size((int)(100 * scale), (int)(30 * scale));
			_btnReset.Location = new Point(w - _btnReset.Width - 20, h - _btnReset.Height - 20);

			// Version Label Positioning
			_lblVersion.Location = new Point(this.ClientSize.Width - _lblVersion.Width - 10, this.ClientSize.Height - 25);
			_lblVersion.BringToFront();
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (!_isInternalClose && e.CloseReason == CloseReason.UserClosing)
			{
				e.Cancel = true;
				_lblFeedback.Text = "FINISH THE FIGHT!";
				_lblFeedback.ForeColor = Color.OrangeRed;
			}
			base.OnFormClosing(e);
		}

		private void BtnSubmit_Click(object? sender, EventArgs e)
		{
			if (!int.TryParse(_txtAnswer.Text, out int ans)) return;
			var result = _session.ProcessAnswer(ans, _a, _b);

			if (result.IsCorrect)
			{
				_monsterHealth -= ans;
				_monsterHealthBar.Value = Math.Max(0, _monsterHealth);
				_lblFeedback.Text = $"HIT! -{ans} DMG";
				_lblFeedback.ForeColor = Color.Lime;
				if (_monsterHealth <= 0)
				{
					XpSystem.AddXp(_settings.PlayerProgress, 25 + (_settings.MaxFactorUnlocked * 10));
					ShowVictoryScreen();
				}
				else
				{
					XpSystem.AddXp(_settings.PlayerProgress, XpSystem.XpPerCorrectAnswer);
					GenerateQuestion();
				}
			}
			else
			{
				int monsterDmg = _a * _b;
				_playerHealth -= monsterDmg;
				_playerHealthBar.Value = Math.Max(0, _playerHealth);
				_lblFeedback.Text = $"Ouch! -{monsterDmg} DMG!";
				_lblFeedback.ForeColor = Color.Red;
				if (_playerHealth <= 0) HandleDeath(); else GenerateQuestion();
			}
			UpdatePlayerStats();
			LayoutCombat();
		}

		private void ShowVictoryScreen()
		{
			_monsterHealthBar.Value = 0;
			_lblFeedback.Text = "MONSTER DEFEATED!";
			_lblFeedback.ForeColor = Color.Gold;
			_txtAnswer.Visible = _btnSubmit.Visible = _die1.Visible = _die2.Visible = false;
			_btnContinue.Visible = _btnExit.Visible = true;
			_btnContinue.Focus();
		}

		private void ResetForNextFight()
		{
			_btnContinue.Visible = _btnExit.Visible = false;
			_txtAnswer.Visible = _btnSubmit.Visible = _die1.Visible = _die2.Visible = true;
			SpawnMonster(); GenerateQuestion();
			_lblFeedback.Text = "A NEW FOE APPEARS!";
			_lblFeedback.ForeColor = Color.Yellow;
			_txtAnswer.Focus();
		}

		private void ResetBattleState()
		{
			_playerHealth = _maxPlayerHealth;
			_quizEngine.InitializeForCurrentLevel();
			_btnContinue.Visible = _btnExit.Visible = false;
			_txtAnswer.Visible = _btnSubmit.Visible = _die1.Visible = _die2.Visible = true;
			UpdatePlayerStats(); SpawnMonster(); GenerateQuestion();
		}

		private void SpawnMonster()
		{
			int tier = Math.Max(1, _settings.MaxFactorUnlocked);
			_maxMonsterHealth = 40 + (tier * 35);
			_monsterHealth = _maxMonsterHealth;
			_monsterHealthBar.Maximum = _maxMonsterHealth;
			_monsterHealthBar.Value = _monsterHealth;
			string mFile = tier < 4 ? "slime.png" : tier < 7 ? "orc.png" : "dragon.png";
			try
			{
				string path = Path.Combine(AssetPaths.AssetsRoot, "Monsters", mFile);
				if (File.Exists(path)) { _picMonster.Image?.Dispose(); _picMonster.Image = Image.FromFile(path); }
			}
			catch { }
		}

		private void HandleDeath()
		{
			_lblFeedback.Text = "YOU HAVE FALLEN...";
			System.Windows.Forms.Timer t = new System.Windows.Forms.Timer { Interval = 2000 };
			t.Tick += (s, e) => {
				t.Stop();
				_playerHealth = _maxPlayerHealth;
				SpawnMonster();
				GenerateQuestion();
			};
			t.Start();
		}

		private void GenerateQuestion()
		{
			try
			{
				var q = _quizEngine.GetNextQuestion(); _a = q.a; _b = q.b;
				UpdateDiceVisuals(); _txtAnswer.Clear(); _txtAnswer.Focus();
			}
			catch { _quizEngine.InitializeForCurrentLevel(); GenerateQuestion(); }
		}

		private void UpdateDiceVisuals()
		{
			try
			{
				_die1.Image?.Dispose(); _die2.Image?.Dispose();
				string d1 = AssetPaths.Dice($"die_{_a}.png"), d2 = AssetPaths.Dice($"die_{_b}.png");
				if (File.Exists(d1)) _die1.Image = Image.FromFile(d1);
				if (File.Exists(d2)) _die2.Image = Image.FromFile(d2);
			}
			catch { }
		}

		private void UpdatePlayerStats()
		{
			var p = _settings.PlayerProgress;
			int nextLevelXp = XpSystem.GetXpRequiredForNextLevel(p.Level);
			_lblLevel.Text = $"KNIGHT LEVEL: {p.Level}";
			_lblXpStatus.Text = $"XP: {p.CurrentXp} / {nextLevelXp}";
			_playerHealthBar.Maximum = _maxPlayerHealth;
			_playerHealthBar.Value = _playerHealth;
			_playerXpBar.Maximum = nextLevelXp;
			_playerXpBar.Value = Math.Min(p.CurrentXp, nextLevelXp);

			// Version Sync
			_lblVersion.Text = $"v{Application.ProductVersion}";

			try
			{
				string path = AssetPaths.KnightSprite(KnightProgression.GetKnightStageIndex(p.Level));
				if (File.Exists(path)) { _picKnight.Image?.Dispose(); _picKnight.Image = Image.FromFile(path); }
			}
			catch { }
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