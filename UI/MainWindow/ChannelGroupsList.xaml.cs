using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Commons.UI
{
    public partial class ChannelGroupsList : DockPanel
    {
        CommonsContext? db => DesignerProperties.GetIsInDesignMode(this) ? null : ((App)Application.Current).DB;
        MainWindow mainWindow => (MainWindow)Application.Current.MainWindow;

        public CollectionViewSource ChannelsViewSource { get; internal set; }

        public ChannelGroupsList()
        {
            InitializeComponent();
            this.DataContext = db;
            ChannelsViewSource = (CollectionViewSource)Resources["channelsViewSource"];
        }

        private async void OnChannelSelectionChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            IList<DataGridCellInfo> selectedCells = e.AddedCells;
            if (selectedCells.Count() != 0)
            {
                await mainWindow.SetCurrentChannel((Channel)selectedCells[0].Item);
            }
        }

        private async void OnAddTextChannelClicked(object sender, RoutedEventArgs e)
        {
            if (db == null) return;

            if (db.CurrentSpace == null) throw new NullReferenceException(nameof(db.CurrentSpace));
            if (db.CurrentSpace.SpaceNetworker == null) throw new NullReferenceException(nameof(db.CurrentSpace.SpaceNetworker));

            AddChannelWindow addChannelWindow = new AddChannelWindow();
            if (addChannelWindow.ShowDialog() == true)
            {
                Trace.WriteLine("Adding channel for button press!!!!!!!!!!!!!");
                Channel newChannel = new Channel { ID = Guid.NewGuid(), Name = addChannelWindow.ChannelName };
                db.Channels.Add(newChannel);
                db.CurrentSpace.Channels.Add(newChannel);
                db.SaveChanges();

                await db.CurrentSpace.SpaceNetworker.ControlPeer.SendChannel(newChannel);

                await mainWindow.SetCurrentChannel(newChannel);
            }
        }
    }
}
