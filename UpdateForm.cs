using System;
using System.Windows.Forms;
using Velopack;
using Velopack.Sources;
using System.Threading.Tasks;

namespace MathQuizLocker
{
	/// <summary>Small dialog that checks for Velopack updates on GitHub; on success closes with OK so the main form runs.</summary>
	public partial class UpdateForm : Form
	{
		private ProgressBar _progressBar;
		private Label _lblStatus;

		public UpdateForm()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			this.Size = new System.Drawing.Size(400, 150);
			this.Text = "Checking for Updates...";
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.StartPosition = FormStartPosition.CenterScreen;
			this.ControlBox = false; // Hide close/min/max buttons

			_progressBar = new ProgressBar { Location = new System.Drawing.Point(20, 50), Size = new System.Drawing.Size(340, 23) };
			_lblStatus = new Label { Text = "Connecting to server...", Location = new System.Drawing.Point(20, 20), AutoSize = true };

			this.Controls.Add(_progressBar);
			this.Controls.Add(_lblStatus);

			this.Shown += async (s, e) => await CheckForUpdates();

			this.SetStyle(
				ControlStyles.AllPaintingInWmPaint |
				ControlStyles.UserPaint |
				ControlStyles.OptimizedDoubleBuffer,
				true);

			this.UpdateStyles();

		}

		/// <summary>Runs on Shown: check for updates, optionally download and restart; otherwise close with OK.</summary>
		private async Task CheckForUpdates()
		{
			try
			{
				var mgr = new UpdateManager(new GithubSource("https://github.com/DoneFeedingTheFox/MathQuizLocker", null, false));
				var newVersion = await mgr.CheckForUpdatesAsync();

				if (newVersion != null)
				{
					_lblStatus.Text = $"Downloading Version {newVersion.TargetFullRelease.Version}...";

					// Velopack allows us to track download progress
					await mgr.DownloadUpdatesAsync(newVersion, (progress) => {
						_progressBar.Value = progress;
					});

					_lblStatus.Text = "Restarting to apply update...";
					mgr.ApplyUpdatesAndRestart(newVersion);
				}
				else
				{
					// No update found, proceed to game
					this.DialogResult = DialogResult.OK;
					this.Close();
				}
			}
			catch (Exception ex)
			{
				AppLogger.Warn("Update check failed, starting game anyway: " + ex.Message);
				_lblStatus.Text = "Update check failed. Starting game...";
				await Task.Delay(800);
				this.DialogResult = DialogResult.OK;
				this.Close();
			}
		}
	}
}