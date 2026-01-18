using System;
using System.Drawing;
using System.Windows.Forms;
using MathQuizLocker.Services;


namespace MathQuizLocker
{
    public partial class QuizForm
    {
        private System.Windows.Forms.Timer _meleeTimer = new System.Windows.Forms.Timer { Interval = 15 };
        private void AnimateMeleeStrike(int damage)
        {
            _isAnimating = true;
            _knightOriginalPos = _picKnight.Location;

            // Process game logic immediately
            _session.ApplyDamage(damage);
            ShowDamage(damage, _picMonster.Location, Color.Red);

            // Change the sprite once to the attack frame
            _picKnight.Image = AssetCache.GetImageClone(AssetPaths.KnightAttack(_equippedKnightStage));

            int distance = _picMonster.Left - _picKnight.Right + 50;
            int step = 0;

            // Clean up to prevent "Timer Stacking" which causes that 2 FPS drop
            _meleeTimer.Stop();
            _meleeTimer.Tick -= MeleeTickHandler;

            void MeleeTickHandler(object? s, EventArgs e)
            {
                step++;

                // Movement Logic
                if (step <= 4) _picKnight.Left += (distance / 4);
                else if (step == 5) UpdateMonsterSprite("hit");
                else if (step <= 12) _picKnight.Left -= (distance / 8);
                else
                {
                    _meleeTimer.Stop();
                    _meleeTimer.Tick -= MeleeTickHandler; // Detach to prevent memory leaks

                    _isAnimating = false;
                    _picKnight.Location = _knightOriginalPos;
                    SetKnightIdleSprite();
                    UpdateMonsterSprite("idle");

                    if (_session.CurrentMonsterHealth <= 0)
                        ShowVictoryScreen();
                    else
                        GenerateQuestion();

                    this.Invalidate(); // Final full redraw to ensure UI is clean
                    return;
                }

                // PERFORMANCE FIX: Only redraw the horizontal combat lane
                this.Invalidate(GetMeleeArea());
            }

            _meleeTimer.Tick += MeleeTickHandler;
            _meleeTimer.Start();
        }
        private void AnimateMonsterAttack(int damage)
        {
            _isAnimating = true;
            UpdateMonsterSprite("attack");

            // Process damage logic
            _session.ApplyPlayerDamage(damage);
            ShowDamage(damage, _picKnight.Location, Color.OrangeRed);

            // Swap knight sprite once
            _picKnight.Image = AssetCache.GetImageClone(AssetPaths.KnightHit(_equippedKnightStage));

            int step = 0;
            int originalMonsterX = _picMonster.Left;

            // 1. Clean up timer to prevent stacking
            _meleeTimer.Stop();
            _meleeTimer.Tick -= MonsterTickHandler;

            void MonsterTickHandler(object? s, EventArgs e)
            {
                step++;

                // Movement Logic
                if (step <= 5) _picMonster.Left -= 20;
                else if (step <= 12) _picMonster.Left += 15;
                else
                {
                    _meleeTimer.Stop();
                    _meleeTimer.Tick -= MonsterTickHandler; // Detach

                    _isAnimating = false;
                    _picMonster.Left = originalMonsterX;
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

                    this.Invalidate(); // Final full redraw for UI cleanup
                    return;
                }

                // 2. PERFORMANCE FIX: Only redraw the active combat area
                // This captures both the monster lunge and the knight's health bar reaction
                this.Invalidate(GetCombatZone());
            }

            _meleeTimer.Tick += MonsterTickHandler;
            _meleeTimer.Start();
        }
    }
}