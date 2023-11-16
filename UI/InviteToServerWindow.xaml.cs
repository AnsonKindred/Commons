using System.Windows;

namespace Commons
{
    /// <summary>
    /// Interaction logic for InviteToServerWindow.xaml
    /// </summary>
    public partial class InviteToSpaceWindow : Window
    {
        public string SpaceLink { get; set; } = "";

        public InviteToSpaceWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            this.Owner = Application.Current.MainWindow;
        }

        private void Window_SourceInitialized(object sender, System.EventArgs e)
        {
            IconHelper.RemoveIcon(this);
            App.SetWindowDarkMode((Window)sender);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }
    }
}
