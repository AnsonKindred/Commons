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

        public ChannelGroupsList()
        {
            InitializeComponent();
            
            if (db == null) throw new NullReferenceException(nameof(db));
        }

        private async void OnChannelSelectionChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            IList<DataGridCellInfo> selectedCells = e.AddedCells;
            if (selectedCells.Count() != 0)
            {
                await mainWindow.SetCurrentChannel((Channel)selectedCells[0].Item);
            }
        }

        private void OnAddTextChannelClicked(object sender, RoutedEventArgs e) => OnAddChannelClicked(false);
        private void OnAddVoiceChannelClicked(object sender, RoutedEventArgs e) => OnAddChannelClicked(true);

        private async void OnAddChannelClicked(bool isVoice)
        {
            if (db == null) return;

            if (db.CurrentSpace == null) throw new NullReferenceException(nameof(db.CurrentSpace));
            if (db.CurrentSpace.SpaceNetworker == null) throw new NullReferenceException(nameof(db.CurrentSpace.SpaceNetworker));

            AddChannelWindow addChannelWindow = new AddChannelWindow();
            if (addChannelWindow.ShowDialog() == true)
            {
                Channel newChannel = new Channel { ID = Guid.NewGuid(), Name = addChannelWindow.ChannelName, IsVoiceChannel = isVoice, SpaceID = db.CurrentSpace.ID, Space = db.CurrentSpace };
                db.Channels.Add(newChannel);
                db.SaveChanges();

                await db.CurrentSpace.SpaceNetworker.ControlPeer.SendChannel(newChannel);

                await mainWindow.SetCurrentChannel(newChannel);

                //VoiceChannelsViewSource.View.Refresh();
            }
        }

        private void TextChannelFilter(object sender, FilterEventArgs e)
        {
            Channel? channel = e.Item as Channel;
            if (channel == null)
            {
                e.Accepted = false;
                return;
            }

            e.Accepted = !channel.IsVoiceChannel;
            return;
        }

        private void VoiceChannelFilter(object sender, FilterEventArgs e)
        {
            Channel? channel = e.Item as Channel;
            if (channel == null)
            {
                e.Accepted = false;
                return;
            }

            e.Accepted = channel.IsVoiceChannel;
            return;
        }
    }
}
