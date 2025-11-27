using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MathQuizLocker.Services;   // <-- for QuizEngine

namespace MathQuizLocker
{
	// Simple rounded panel with soft shadow
	public class RoundedPanel : Panel
	{
		private int _cornerRadius = 20;  // fixed radius; no public property

		public RoundedPanel()
		{
			this.DoubleBuffered = true;
			this.BackColor = Color.FromArgb(32, 32, 32);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			var g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;

			Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

			using (GraphicsPath path = GetRoundedRect(rect, _cornerRadius))
			{
				// Shadow
				using (GraphicsPath shadowPath = GetRoundedRect(
						   new Rectangle(rect.X + 4, rect.Y + 4, rect.Width, rect.Height),
						   _cornerRadius))
				{
					using (Brush shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
					{
						g.FillPath(shadowBrush, shadowPath);
					}
				}

				using (Brush fillBrush = new SolidBrush(this.BackColor))
				{
					g.FillPath(fillBrush, path);
				}

				using (Pen borderPen = new Pen(Color.FromArgb(70, 70, 70), 1.5f))
				{
					g.DrawPath(borderPen, path);
				}
			}
		}

		private GraphicsPath GetRoundedRect(Rectangle rect, int radius)
		{
			GraphicsPath path = new GraphicsPath();
			int d = radius * 2;

			path.AddArc(rect.X, rect.Y, d, d, 180, 90);
			path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
			path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
			path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
			path.CloseFigure();
			return path;
		}
	}

	public class QuizForm : Form
	{
		private readonly AppSettings _settings;
		private readonly QuizEngine _quizEngine;

		// Current question (for display + per-session counter)
		private int _a, _b;
		private (int a, int b) _currentQuestion;
		private int _correctCount = 0;
		private bool _solved = false;

		private RoundedPanel _card = null!;
		private Label _lblQuestion = null!;
		private TextBox _txtAnswer = null!;
		private Button _btnSubmit = null!;
		private Label _lblHint = null!;   // top text: instruction + success/fail messages

		public QuizForm(AppSettings settings)
		{
			_settings = settings ?? new AppSettings();
			_quizEngine = new QuizEngine(_settings);

			InitializeUi();
			GenerateQuestion();
		}

		private void InitializeUi()
		{
			// Fullscreen lock look
			this.FormBorderStyle = FormBorderStyle.None;
			this.WindowState = FormWindowState.Maximized;
			this.TopMost = true;
			this.StartPosition = FormStartPosition.CenterScreen;
			this.ShowInTaskbar = false;
			this.BackColor = Color.FromArgb(18, 18, 18);
			this.DoubleBuffered = true;

			this.KeyPreview = true;
			this.KeyDown += QuizForm_KeyDown;
			this.Resize += QuizForm_Resize;

			_card = new RoundedPanel
			{
				BackColor = Color.FromArgb(32, 32, 32),
				Size = new Size(700, 380)
			};

			_lblHint = new Label
			{
				Text = "Solve the equation to continue.",
				ForeColor = Color.FromArgb(200, 200, 200),
				Font = new Font("Segoe UI", 14, FontStyle.Regular),
				AutoSize = true,
				MaximumSize = new Size(_card.Width - 60, 0),
				TextAlign = ContentAlignment.MiddleCenter
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
				Font = new Font("Segoe UI", 18, FontStyle.Bold),
				Width = 130,
				Height = 50,
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(0, 122, 204),
				ForeColor = Color.White,
				Cursor = Cursors.Hand
			};
			_btnSubmit.FlatAppearance.BorderSize = 0;
			_btnSubmit.Click += BtnSubmit_Click;

			_card.Controls.Add(_lblHint);
			_card.Controls.Add(_lblQuestion);
			_card.Controls.Add(_txtAnswer);
			_card.Controls.Add(_btnSubmit);

			this.Controls.Add(_card);

			LayoutCard();
		}

		private void QuizForm_Resize(object? sender, EventArgs e)
		{
			LayoutCard();
		}

		private void LayoutCard()
		{
			_lblHint.MaximumSize = new Size(_card.Width - 60, 0);

			int y = 30;

			// Hint / status at the top
			_lblHint.Location = new Point(
				(_card.Width - _lblHint.Width) / 2,
				y
			);
			y += _lblHint.Height + 25;

			// Question (centered)
			_lblQuestion.Location = new Point(
				(_card.Width - _lblQuestion.Width) / 2,
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
			y += _btnSubmit.Height + 25;

			_card.Height = Math.Max(y, 280);

			_card.Location = new Point(
				(this.ClientSize.Width - _card.Width) / 2,
				(this.ClientSize.Height - _card.Height) / 2
			);
		}

		private void GenerateQuestion()
		{
			int required = _settings.RequiredCorrectAnswers > 0
				? _settings.RequiredCorrectAnswers
				: 10;

			// IMPORTANT: do NOT reset _lblHint here.
			// That way the last success/fail text stays visible.

			// Remember previous question
			var previous = _currentQuestion;

			// Try a few times to get a *different* question than last time
			for (int i = 0; i < 5; i++)
			{
				var candidate = _quizEngine.GetNextQuestion();

				if (candidate.a != previous.a || candidate.b != previous.b)
				{
					_currentQuestion = candidate;
					break;
				}

				if (i == 4)
				{
					_currentQuestion = candidate;
				}
			}

			_a = _currentQuestion.a;
			_b = _currentQuestion.b;

			_lblQuestion.Text = $"({_correctCount}/{required})  What is {_a} × {_b}?";

			_txtAnswer.Text = "";
			_txtAnswer.Focus();

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
				_lblHint.ForeColor = Color.Goldenrod;
				_txtAnswer.Focus();
				_txtAnswer.SelectAll();
				LayoutCard();
				return;
			}

			int currentA = _a;
			int currentB = _b;
			int correct = currentA * currentB;

			// Ask QuizEngine to record this answer + update difficulty
			bool isCorrect = _quizEngine.SubmitAnswer(answer);

			if (isCorrect)
			{
				_correctCount++;

				AppSettings.Save(_settings);

				_lblHint.Text = $"Correct! {currentA} × {currentB} = {correct}.";
				_lblHint.ForeColor = Color.FromArgb(0, 200, 0); // green

				if (_correctCount >= required)
				{
					_lblHint.Text += " Unlocking...";
					_solved = true;

					LayoutCard();
					this.Close();
					return;
				}

				// Immediately go to next question, while leaving the success text at the top
				GenerateQuestion();
			}
			else
			{
				AppSettings.Save(_settings);

				_lblHint.Text = $"Wrong: you answered {answer}, but {currentA} × {currentB} = {correct}.";
				_lblHint.ForeColor = Color.FromArgb(220, 50, 50); // red

				// Immediately go to next question, while leaving the fail text at the top
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
			if (_settings.EnableDeveloperHotkey &&
				e.Control && e.Shift && e.Alt && e.KeyCode == Keys.M)
			{
				e.Handled = true;
				Environment.Exit(0);
				return;
			}

			if (e.Alt && e.KeyCode == Keys.F4)
			{
				e.Handled = true;
				return;
			}

			if (e.KeyCode == Keys.Escape)
			{
				e.Handled = true;
				return;
			}

			if (e.KeyCode == Keys.Enter)
			{
				e.Handled = true;
				BtnSubmit_Click(this, EventArgs.Empty);
				return;
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
