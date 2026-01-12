using System;
using System.Drawing;
using System.Windows.Forms;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
    public partial class QuizForm : Form
    {
        private readonly AppSettings _settings;
        private readonly QuizEngine _quizEngine;
        private readonly GameSessionManager _session;

        // Combat State
        private int _a, _b, _monsterHealth, _maxMonsterHealth;
        private int _playerHealth, _maxPlayerHealth = 100;
        private bool _isInternalClose = false;
        private string _currentMonsterName = "slime";

        // Animation logic
        private bool _isAnimating = false;
        private Point _knightOriginalPos;
        private System.Windows.Forms.Timer _animationTimer = new();

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
                cp.Style &= ~0x00080000;    // WS_SYSMENU
                cp.ExStyle |= 0x02000000;   // WS_EX_COMPOSITED (Prevents child flicker)
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isInternalClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                _lblFeedback.Text = "FINISH THE FIGHT TO UNLOCK!";
                _lblFeedback.ForeColor = Color.OrangeRed;
            }
            base.OnFormClosing(e);
        }
    }
}