using System;
using System.Drawing;
using System.Windows.Forms;

namespace MathQuizLocker
{
    public class QuizForm : Form
    {
        private readonly Random _rnd = new Random();
        private readonly AppSettings _settings;

        private int _a, _b;
        private int _correctCount = 0;
        private bool _solved = false;

        private Panel _card = null!;
        private Label _lblTitle = null!;
        private Label _lblQuestion = null!;
        private TextBox _txtAnswer = null!;
        private Button _btnSubmit = null!;
        private Label _lblHint = null!;

        public QuizForm(AppSettings settings)
        {
            _settings = settings ?? new AppSettings();

            InitializeUi();
            GenerateQuestion();
        }

        private void InitializeUi()
        {
            // Fullscreen “lock” look
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(18, 18, 18);

            this.KeyPreview = true;
            this.KeyDown += QuizForm_KeyDown;
            this.Resize += QuizForm_Resize;

            // Card panel in the middle
            _card = new Panel
            {
                BackColor = Color.FromArgb(32, 32, 32),
                Size = new Size(640, 360)
            };

            _lblTitle = new Label
            {
                Text = "Math Lock",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 30, FontStyle.Bold),
                AutoSize = true
            };

            _lblQuestion = new Label
            {
                Text = "Question",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 24, FontStyle.Regular),
                AutoSize = true
            };

            _txtAnswer = new TextBox
            {
                Font = new Font("Segoe UI", 22, FontStyle.Regular),
                Width = 220
            };

            _btnSubmit = new Button
            {
                Text = "OK",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),  // a bit smaller
                Width = 130,                                      // was 160
                Height = 50,                                      // was 60
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };

            _btnSubmit.FlatAppearance.BorderSize = 0;
            _btnSubmit.Click += BtnSubmit_Click;

            _lblHint = new Label
            {
                Text = "Solve the multiplication to continue.",
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Segoe UI", 14, FontStyle.Regular),   // was 12 + Italic
                AutoSize = true,
                MaximumSize = new Size(_card.Width - 60, 0)
            };



            // Add controls to card, then card to form
            _card.Controls.Add(_lblTitle);
            _card.Controls.Add(_lblQuestion);
            _card.Controls.Add(_txtAnswer);
            _card.Controls.Add(_btnSubmit);
            _card.Controls.Add(_lblHint);

            this.Controls.Add(_card);

            LayoutCard();
        }

        private void QuizForm_Resize(object? sender, EventArgs e)
        {
            LayoutCard();
        }

        private void LayoutCard()
        {
            // Recompute max width for the hint in case card size changed
            _lblHint.MaximumSize = new Size(_card.Width - 60, 0);

            int marginX = 40;
            int y = 20;

            // Title
            _lblTitle.Location = new Point(
                (_card.Width - _lblTitle.Width) / 2,
                y
            );
            y += _lblTitle.Height + 25;

            // Question
            _lblQuestion.Location = new Point(
                marginX,
                y
            );
            y += _lblQuestion.Height + 25;

            // Answer textbox
            _txtAnswer.Location = new Point(
                (_card.Width - _txtAnswer.Width) / 2,
                y
            );
            y += _txtAnswer.Height + 20;

            // Button
            _btnSubmit.Location = new Point(
                (_card.Width - _btnSubmit.Width) / 2,
                y
            );
            y += _btnSubmit.Height + 20;

            // Hint (can be multi-line)
            _lblHint.Location = new Point(
                (_card.Width - _lblHint.Width) / 2,
                y
            );
            y += _lblHint.Height + 20;

            // Make sure card is tall enough for everything + some padding
            _card.Height = Math.Max(y, 260);

            // Center the card on screen after sizing
            _card.Location = new Point(
                (this.ClientSize.Width - _card.Width) / 2,
                (this.ClientSize.Height - _card.Height) / 2
            );
        }


        private void GenerateQuestion()
        {
            int min = Math.Min(_settings.MinOperand, _settings.MaxOperand);
            int max = Math.Max(_settings.MinOperand, _settings.MaxOperand);

            if (min < 1) min = 1;
            if (max < 1) max = 1;

            _a = _rnd.Next(min, max + 1);
            _b = _rnd.Next(min, max + 1);

            int required = _settings.RequiredCorrectAnswers > 0
                ? _settings.RequiredCorrectAnswers
                : 10;

            _lblQuestion.Text = $"({_correctCount}/{required})  What is {_a} × {_b}?";

            _txtAnswer.Text = "";
            _txtAnswer.Focus();

            // Do NOT reset _lblHint here – we want feedback to stay visible.
            LayoutCard();
        }

        private void BtnSubmit_Click(object? sender, EventArgs e)
        {
            int required = _settings.RequiredCorrectAnswers > 0
                ? _settings.RequiredCorrectAnswers
                : 10;

            if (!int.TryParse(_txtAnswer.Text, out int answer))
            {
                _lblHint.Text = "Please enter a number (digits only).";
                _txtAnswer.Focus();
                _txtAnswer.SelectAll();
                LayoutCard();
                return;
            }

            // Capture current question before we possibly generate a new one
            int currentA = _a;
            int currentB = _b;
            int correct = currentA * currentB;

            if (answer == correct)
            {
                _correctCount++;

                if (_correctCount >= required)
                {
                    _lblHint.Text = $"Correct! {currentA} × {currentB} = {correct}. Unlocking...";
                    _solved = true;
                    LayoutCard();
                    this.Close();
                    return;
                }
                else
                {
                    _lblHint.Text = $"Correct! {currentA} × {currentB} = {correct}.";
                    GenerateQuestion();
                }

            }
            else
            {
                // Show what they answered and the correct result,
                // then immediately give a NEW question so they can’t just retype it.
                _lblHint.Text = $"Wrong: you answered {answer}, but {currentA} × {currentB} = {correct}. New question:";
                GenerateQuestion();
            }

            LayoutCard();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_solved && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
            }

            base.OnFormClosing(e);
        }

        private void QuizForm_KeyDown(object? sender, KeyEventArgs e)
        {
            // Block Alt+F4
            if (e.Alt && e.KeyCode == Keys.F4)
            {
                e.Handled = true;
            }

            // Block Escape
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
            }

            // Enter submits
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                BtnSubmit_Click(this, EventArgs.Empty);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                BtnSubmit_Click(this, EventArgs.Empty);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
