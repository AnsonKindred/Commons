using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Commons.UI
{
    public partial class ChannelGroupsList : Grid
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
