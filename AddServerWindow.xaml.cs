using System.Windows;

namespace Commons
{
    /// <summary>
    /// Interaction logic for AddServerWindow.xaml
    /// </summary>
    public partial class AddSpaceWindow : Window
    {
        public string SpaceName { get; private set; } = "";

        public AddSpaceWindow()
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
        }

        private void Window_SourceInitialized(object sender, System.EventArgs e) => App.SetWindowDarkMode((Window)sender);

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SpaceName = serverNameTextBox.Text;
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
