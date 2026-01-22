using MathQuizLocker.Services;

namespace MathQuizLocker
{
    public partial class QuizForm
    {
        private System.Windows.Forms.Timer _playerAnimationTimer = new System.Windows.Forms.Timer { Interval = 30 };
        private System.Windows.Forms.Timer _monsterAnimationTimer = new System.Windows.Forms.Timer { Interval = 30 };

        private void AnimateMeleeStrike(int damage)
        {
            _knightOriginalPos = _picKnight.Location;

            bool isDefeated = _session.ApplyDamage(damage, out int xpGained, out bool leveledUp);
            UpdatePlayerHud();
            ShowDamage(damage, _picMonster.Location, Color.Red);

            _picKnight.Image = AssetCache.GetImageClone(AssetPaths.KnightAttack(_equippedKnightStage));

            int distance = _monsterOriginalPos.X - _picKnight.Right;
            int step = 0;

            // 1. Reset the timer
            _playerAnimationTimer.Stop();
            _playerAnimationTimer.Tick -= MeleeTickHandler;

            void MeleeTickHandler(object? s, EventArgs e)
            {
                if (!this.IsHandleCreated || this.IsDisposed) return;
                step++;
                if (step <= 4) _picKnight.Left += (distance / 4);
                else if (step == 5) UpdateMonsterSprite("hit");
                else if (step <= 12) _picKnight.Left -= (distance / 8);
                else
                {
                    _playerAnimationTimer.Stop();
                    _playerAnimationTimer.Tick -= MeleeTickHandler;

                    _picKnight.Location = _knightOriginalPos;
                    SetKnightIdleSprite();
                    UpdateMonsterSprite("idle");

                    if (_session.CurrentMonsterHealth <= 0)
                    {
                        _countdownTimer.Stop();
                        _lblTimer.Visible = false;
                        ShowVictoryScreen();
                    }
                    else
                    {
                        GenerateQuestion();
                    }

                    this.Invalidate();
                    return;
                }
                this.Invalidate(GetMeleeArea());
            }

            // 2. CRITICAL: Attach and START
            _playerAnimationTimer.Tick += MeleeTickHandler;
            _playerAnimationTimer.Start();
        }

        private void AnimateMonsterAttack(int damage)
        {
            if (!this.IsHandleCreated || this.IsDisposed) return;
            UpdateMonsterSprite("attack");
            bool isHeavyHit = damage >= 40;

            _session.ApplyPlayerDamage(damage);
            ShowDamage(damage, _picKnight.Location, isHeavyHit ? Color.DarkRed : Color.OrangeRed);

            _picKnight.Image = AssetCache.GetImageClone(AssetPaths.KnightHit(_equippedKnightStage));

            int step = 0;
            int originalMonsterX = _picMonster.Left;

            // 1. Reset the timer
            _monsterAnimationTimer.Stop();
            _monsterAnimationTimer.Tick -= MonsterTickHandler;

            void MonsterTickHandler(object? s, EventArgs e)
            {
                step++;
                int lungeSpeed = isHeavyHit ? 30 : 20;

                if (step <= 5) _picMonster.Left -= lungeSpeed;
                else if (step <= 12) _picMonster.Left += (int)(lungeSpeed * 0.75);
                else
                {
                    _monsterAnimationTimer.Stop();
                    _monsterAnimationTimer.Tick -= MonsterTickHandler;

                    _picMonster.Left = originalMonsterX;
                    UpdateMonsterSprite("idle");
                    SetKnightIdleSprite();

                    if (_session.CurrentPlayerHealth <= 0)
                    {
                        _countdownTimer.Stop();
                        _monsterAnimationTimer.Stop();
                        _playerAnimationTimer.Stop();
                        _lblTimer.Visible = false;
                        _txtAnswer.Visible = false;
                        _btnSubmit.Visible = false;
                        ShowGameOverScreen();
                    }
                    else
                    {
                        _txtAnswer.Focus();
                    }

                    this.Invalidate();
                    return;
                }
                this.Invalidate(GetCombatZone());
            }

            // 2. CRITICAL: Attach and START
            _monsterAnimationTimer.Tick += MonsterTickHandler;
            _monsterAnimationTimer.Start();
        }
    }
}