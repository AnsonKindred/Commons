using System.Windows;

namespace Commons
{
    /// <summary>
    /// Interaction logic for AddServerWindow.xaml
    /// </summary>
    public partial class AddSpaceWindow : Window
    {
        public string Text { get; private set; } = "";
        public bool IsAddServer { get; private set; } = true;

        public AddSpaceWindow()
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
        }

        private void Window_SourceInitialized(object sender, System.EventArgs e)
        {
            IconHelper.RemoveIcon(this);
            App.SetWindowDarkMode((Window)sender);
        }

        private void OnAddServerButtonClick(object sender, RoutedEventArgs e)
        {
            if (NewSpaceTextBox.Text != "")
            {
                IsAddServer = true;
                Text = NewSpaceTextBox.Text;
                DialogResult = true;
            }
            else if (JoinSpaceTextBox.Text != "")
            {
                IsAddServer = false;
                Text = JoinSpaceTextBox.Text;
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
            this.Close();
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }
    }
}
