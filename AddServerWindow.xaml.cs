using System.Windows;

namespace Commons
{
    /// <summary>
    /// Interaction logic for AddServerWindow.xaml
    /// </summary>
    public partial class AddServerWindow : Window
    {
        public string ServerName { get; private set; } = "";

        public AddServerWindow()
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
        }

        private void Window_SourceInitialized(object sender, System.EventArgs e) => App.SetWindowDarkMode((Window)sender);

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ServerName = serverNameTextBox.Text;
            DialogResult = true;
            this.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }
    }
}
