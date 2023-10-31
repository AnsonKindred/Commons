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

        private void Window_SourceInitialized(object sender, System.EventArgs e) => App.SetWindowDarkMode((Window)sender);

        private void OnAddServerButtonClick(object sender, RoutedEventArgs e)
        {
            if (AddSpaceNameTextBox.Text != "")
            {
                IsAddServer = true;
                Text = AddSpaceNameTextBox.Text;
                DialogResult = true;
            }
            else if (JoinSpaceDefaultText.Text != "")
            {
                IsAddServer = false;
                Text = JoinSpaceNameTextBox.Text;
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

        private void JoinSpaceNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            JoinSpaceDefaultText.Visibility = Visibility.Hidden;
        }

        private void JoinSpaceNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (JoinSpaceNameTextBox.Text == "")
            {
                JoinSpaceDefaultText.Visibility = Visibility.Visible;
            }
        }

        private void AddSpaceNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            AddSpaceDefaultText.Visibility = Visibility.Hidden;
        }

        private void AddSpaceNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (AddSpaceNameTextBox.Text == "")
            {
                AddSpaceDefaultText.Visibility = Visibility.Visible;
            }
        }
    }
}
