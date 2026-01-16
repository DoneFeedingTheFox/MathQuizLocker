using System;
using System.Drawing;
using System.Windows.Forms;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
    public partial class QuizForm
    {
        private void AnimateMeleeStrike(int damage)
        {
            _isAnimating = true;
            _knightOriginalPos = _picKnight.Location;
            _session.ApplyDamage(damage);
            ShowDamage(damage, _picMonster.Location, Color.Red);
            _picKnight.Image = AssetCache.GetImageClone(AssetPaths.KnightAttack(_equippedKnightStage));

            int distance = _picMonster.Left - _picKnight.Right + 50;
            int step = 0;

            _animationTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _animationTimer.Tick += (s, e) =>
            {
                step++;
                if (step <= 4) _picKnight.Left += (distance / 4);
                else if (step == 5) UpdateMonsterSprite("hit");
                else if (step <= 12) _picKnight.Left -= (distance / 8);
                else
                {
                    _animationTimer.Stop();
                    _isAnimating = false;
                    _picKnight.Location = _knightOriginalPos;
                    SetKnightIdleSprite();
                    UpdateMonsterSprite("idle");

                    if (_session.CurrentMonsterHealth <= 0)
                        ShowVictoryScreen();
                    else
                        GenerateQuestion();
                }
                this.Invalidate();
            };
            _animationTimer.Start();
        }

        private void AnimateMonsterAttack(int damage)
        {
            _isAnimating = true;
            UpdateMonsterSprite("attack");
            _session.ApplyPlayerDamage(damage);
            ShowDamage(damage, _picKnight.Location, Color.OrangeRed);
            _picKnight.Image = AssetCache.GetImageClone(AssetPaths.KnightHit(_equippedKnightStage));

            int step = 0;
            _animationTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _animationTimer.Tick += (s, e) =>
            {
                step++;
                if (step <= 5) _picMonster.Left -= 20;
                else if (step <= 12) _picMonster.Left += 15;
                else
                {
                    _animationTimer.Stop();
                    _isAnimating = false;
                    UpdateMonsterSprite("idle");
                    SetKnightIdleSprite();

                    if (_session.CurrentPlayerHealth <= 0)
                    {
                        ShowGameOverScreen();
                    }
                    else
                    {
                        GenerateQuestion();
                    }
                }
                this.Invalidate();
            };
            _animationTimer.Start();
        }
    }
}