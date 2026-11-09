using Client.Infrastructure.Service;
using System;
using System.Threading;
using System.Windows;

namespace Client.Application.Views
{
    public partial class GeodataDownloadWindow : Window
    {
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private DateTime lastUpdate = DateTime.MinValue;

        public CancellationToken CancellationToken => cancellationTokenSource.Token;

        public GeodataDownloadWindow()
        {
            InitializeComponent();
        }

        public void UpdateProgress(GeodataDownloadProgress progress)
        {
            // Throttle UI updates to ~10 per second
            var now = DateTime.UtcNow;
            if ((now - lastUpdate).TotalMilliseconds < 100 && progress.FilesDone < progress.TotalFiles)
            {
                return;
            }
            lastUpdate = now;

            var percent = progress.TotalBytes > 0
                ? (double)progress.BytesDone / progress.TotalBytes * 100.0
                : 0.0;

            ProgressBar.Value = Math.Min(100, percent);
            StatusText.Text = string.Format(
                "{0:0}%  -  {1:0.0} / {2:0.0} MB  -  {3}/{4} files",
                percent,
                progress.BytesDone / 1048576.0,
                progress.TotalBytes / 1048576.0,
                progress.FilesDone,
                progress.TotalFiles
            );
            FileText.Text = progress.CurrentFile;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            CancelButton.IsEnabled = false;
            StatusText.Text = "Cancelling...";
            cancellationTokenSource.Cancel();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            cancellationTokenSource.Cancel();
            base.OnClosing(e);
        }
    }
}
