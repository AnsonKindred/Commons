using System.Windows;

namespace Commons
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public string LoginName { get; private set; } = "";

        public LoginWindow()
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
        }

        private void Window_SourceInitialized(object sender, System.EventArgs e) => App.SetWindowDarkMode((Window)sender);

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            LoginName = loginTextBox.Text;
            DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }

        private void OnTextBoxKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            OkButton.IsEnabled = loginTextBox.Text.Length != 0;
        }
    }
}
