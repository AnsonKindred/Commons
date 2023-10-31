using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Commons.UI
{
    public partial class ChannelGroupsList : DockPanel
    {
        CommonsContext db => ((App)Application.Current).DB;
        MainWindow mainWindow => (MainWindow)Application.Current.MainWindow;

        public CollectionViewSource ChannelsViewSource { get; internal set; }

        public ChannelGroupsList()
        {
            InitializeComponent();
            ChannelsViewSource = (CollectionViewSource)Resources["channelsViewSource"];
        }

        private void OnChannelSelectionChanged(object sender, EventArgs e)
        {

        }
    }
}
