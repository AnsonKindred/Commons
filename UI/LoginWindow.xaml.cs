using System.Windows;

namespace Commons
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public string LoginName { get; private set; } = "";
        public string Password { get; private set; } = "";
        public bool IsLoggingIn = false;

        public LoginWindow()
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            this.DataContext = this;
        }

        private void Window_SourceInitialized(object sender, System.EventArgs e)
        {
            IconHelper.RemoveIcon(this);
            App.SetWindowDarkMode((Window)sender);
        }

        private void OnLoginClick(object sender, RoutedEventArgs e)
        {
            IsLoggingIn = true;
            LoginName = loginTextBox.TextBox.Text;
            Password = passwordTextBox.TextBox.Text;
            DialogResult = true;
            this.Close();
        }

        private void OnCreateAccountClick(object sender, RoutedEventArgs e)
        {
            IsLoggingIn = false;
            LoginName = loginTextBox.TextBox.Text;
            Password = passwordTextBox.TextBox.Text;
            DialogResult = true;
            this.Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }

        private void OnTextBoxKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            LoginButton.IsEnabled = loginTextBox.TextBox.Text.Length != 0 && passwordTextBox.TextBox.Text.Length != 0;
            CreateAccountButton.IsEnabled = LoginButton.IsEnabled;
        }
    }
}
