using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private readonly ApiService _api = new();

        public MainWindow()
        {
            InitializeComponent();
            txtUsername.Focus();
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            lblError.Text = "";

            var username = txtUsername.Text.Trim();
            var password = txtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                lblError.Text = "Please enter username and password";
                return;
            }

            btnLogin.IsEnabled = false;
            lblLoading.Visibility = Visibility.Visible;

            try
            {
                LoginResponse? result = null;

                // If connection fails, retry up to 5 times (server may still be starting)
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        result = await _api.LoginAsync(username, password);
                        break; // Connected — stop retrying
                    }
                    catch (HttpRequestException)
                    {
                        if (attempt < 4)
                        {
                            lblError.Text = $"Connecting to server... ({attempt + 1}/5)";
                            await Task.Delay(2000);
                        }
                        else
                        {
                            throw; // All retries failed — let outer catch handle it
                        }
                    }
                }

                lblError.Text = "";

                if (result != null)
                {
                    AppState.UserCode     = result.UserCode     ?? string.Empty;
                    AppState.UserName     = result.UserName     ?? string.Empty;
                    AppState.EmployeeName = result.EmployeeName ?? string.Empty;
                    AppState.Department   = result.Department   ?? string.Empty;
                    AppState.Designation  = result.Designation  ?? string.Empty;
                    AppState.Email        = result.Email        ?? string.Empty;
                    AppState.MailPassword = string.Empty; // set later from dashboard

                    // Persist session so next launch skips login
                    SessionService.SaveLogin(password, result);

                    App.ShowDashboard();
                    this.Close();
                }
                else
                {
                    lblError.Text = "Invalid username or password!";
                }
            }
            catch (Exception ex)
            {
                lblError.Text = $"Cannot connect to server.\nCheck config.json ServerUrl.\n({ex.Message})";
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            finally
            {
                btnLogin.IsEnabled = true;
                lblLoading.Visibility = Visibility.Collapsed;
            }
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnLogin_Click(sender, e);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide(); // stays in system tray
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
