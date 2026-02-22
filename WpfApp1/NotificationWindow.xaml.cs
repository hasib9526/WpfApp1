using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace WpfApp1
{
    public partial class NotificationWindow : Window
    {
        private readonly DispatcherTimer _autoCloseTimer;

        public NotificationWindow(string title, string message, string type = "info")
        {
            InitializeComponent();

            lblTitle.Text = title;
            lblMessage.Text = message;

            // Set icon based on type
            switch (type.ToLower())
            {
                case "email":
                    lblIcon.Text = "\u2709"; // envelope
                    break;
                case "warning":
                    lblIcon.Text = "\u26A0"; // warning
                    break;
                case "reminder":
                    lblIcon.Text = "\u23F0"; // clock
                    break;
                default:
                    lblIcon.Text = "!";
                    break;
            }

            // Position at top-right corner of screen
            Left = SystemParameters.WorkArea.Width - Width - 10;
            Top = 10;

            // Auto-close after 5 seconds
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _autoCloseTimer.Tick += (_, _) => FadeOutAndClose();
            _autoCloseTimer.Start();
        }

        /// <summary>
        /// Stacks this notification below existing ones
        /// </summary>
        public void StackBelow(int existingCount)
        {
            Top = 10 + (existingCount * 110);
        }

        private void FadeOutAndClose()
        {
            _autoCloseTimer.Stop();

            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3)
            };
            fadeOut.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void BtnDismiss_Click(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer.Stop();
            FadeOutAndClose();
        }
    }
}
