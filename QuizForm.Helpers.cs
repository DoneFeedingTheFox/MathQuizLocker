using MathQuizLocker.Services;
using Velopack;
using Velopack.Sources;

namespace MathQuizLocker
{
    public partial class QuizForm
    {
        private void SpawnMonster()
        {
            int tier = Math.Max(1, _settings.MaxFactorUnlocked);
            _maxMonsterHealth = 40 + (tier * 35);
            _monsterHealth = _maxMonsterHealth;
            _monsterHealthBar.Maximum = _maxMonsterHealth;
            _monsterHealthBar.Value = _monsterHealth;
            _currentMonsterName = tier < 4 ? "slime" : tier < 7 ? "orc" : "dragon";
            UpdateMonsterSprite("idle");
        }

        private void UpdateMonsterSprite(string state)
        {
            string suffix = state == "idle" ? "" : $"_{state}";
            string path = Path.Combine(AssetPaths.AssetsRoot, "Monsters", $"{_currentMonsterName}{suffix}.png");
            var img = LoadImageNoLock(path);
            if (img != null) { _picMonster.Image?.Dispose(); _picMonster.Image = img; }
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
            _die1.Image?.Dispose(); _die2.Image?.Dispose();
            _die1.Image = LoadImageNoLock(AssetPaths.Dice($"die_{_a}.png"));
            _die2.Image = LoadImageNoLock(AssetPaths.Dice($"die_{_b}.png"));
        }

        private void UpdatePlayerStats()
        {
            if (_isAnimating) return;
            var p = _settings.PlayerProgress;
            int nextLevelXp = XpSystem.GetXpRequiredForNextLevel(p.Level);
            _lblLevel.Text = $"KNIGHT LEVEL: {p.Level}";
            _lblXpStatus.Text = $"XP: {p.CurrentXp} / {nextLevelXp}";
            _playerHealthBar.Maximum = _maxPlayerHealth;
            _playerHealthBar.Value = _playerHealth;
            _playerXpBar.Maximum = nextLevelXp;
            _playerXpBar.Value = Math.Min(p.CurrentXp, nextLevelXp);

            string path = AssetPaths.KnightSprite(KnightProgression.GetKnightStageIndex(p.Level));
            var img = LoadImageNoLock(path);
            if (img != null) { _picKnight.Image?.Dispose(); _picKnight.Image = img; }
        }

        private void HandleDeath()
        {
            _lblFeedback.Text = "YOU HAVE FALLEN...";
            var t = new System.Windows.Forms.Timer { Interval = 2000 };
            t.Tick += (s, e) => {
                t.Stop();
                _playerHealth = _maxPlayerHealth;
                _quizEngine.InitializeForCurrentLevel();
                ResetForNextFight();
            };
            t.Start();
        }

        private static Image? LoadImageNoLock(string path)
        {
            if (!File.Exists(path)) return null;
            using var ms = new MemoryStream(File.ReadAllBytes(path));
            return new Bitmap(ms);
        }

        private async Task UpdateMyApp()
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource("https://github.com/DoneFeedingTheFox/MathQuizLocker", null, false));
                var newVersion = await mgr.CheckForUpdatesAsync();
                if (newVersion != null) { await mgr.DownloadUpdatesAsync(newVersion); mgr.ApplyUpdatesAndRestart(newVersion); }
            }
            catch { }
        }
    }
}