using System.Windows;

namespace Commons
{
    /// <summary>
    /// Interaction logic for LinkServerWindow.xaml
    /// </summary>
    public partial class LinkSpaceWindow : Window
    {
        public string SpaceLink { get; private set; } = "";

        public LinkSpaceWindow()
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
        }

        private void Window_SourceInitialized(object sender, System.EventArgs e) => App.SetWindowDarkMode((Window)sender);

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SpaceLink = serverLinkTextBox.Text;
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
