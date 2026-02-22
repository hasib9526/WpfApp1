using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1
{
    public partial class EmailViewWindow : Window
    {
        private readonly string _uid;
        private readonly EmailService _emailService;

        public EmailViewWindow(EmailMessage mail, EmailService emailService)
        {
            InitializeComponent();

            _uid          = mail.Uid;
            _emailService = emailService;

            lblSubject.Text = mail.Subject;
            lblFrom.Text    = mail.From;
            lblDate.Text    = mail.Date.ToString("dddd, MMMM dd yyyy  hh:mm tt");
            lblBody.Text    = "Loading...";

            Loaded += async (_, _) => await LoadBodyAsync();
        }

        private async System.Threading.Tasks.Task LoadBodyAsync()
        {
            var result = await _emailService.GetEmailBodyAsync(_uid);
            lblBody.Text = string.IsNullOrWhiteSpace(result.Body)
                ? "(empty message)"
                : result.Body;
        }

        private void BtnOpenOwa_Click(object sender, RoutedEventArgs e)
        {
            var owaUrl = "https://mail.bitopibd.com/owa/";
            try { Process.Start(new ProcessStartInfo(owaUrl) { UseShellExecute = true }); }
            catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    }
}
