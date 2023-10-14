using System.Windows;

namespace Commons
{
    /// <summary>
    /// Interaction logic for InviteToServerWindow.xaml
    /// </summary>
    public partial class InviteToServerWindow : Window
    {
        public string ServerLink { set { serverLinkTextBox.Text = value; } }

        public InviteToServerWindow()
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
        }

        private void Window_SourceInitialized(object sender, System.EventArgs e) => App.SetWindowDarkMode((Window)sender);

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ServerLink = serverLinkTextBox.Text;
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
