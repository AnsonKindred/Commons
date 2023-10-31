using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Commons.UI
{
    public partial class SpaceListArea : DockPanel
    {
        CommonsContext? db => DesignerProperties.GetIsInDesignMode(this) ? null : ((App)Application.Current).DB;
        MainWindow mainWindow => (MainWindow)Application.Current.MainWindow;

        public CollectionViewSource SpacesViewSource { get; internal set; }

        public SpaceListArea()
        {
            InitializeComponent();
            SpacesViewSource = (CollectionViewSource)Resources["spacesViewSource"];
        }

        private async void AddSpace_Button_Click(object sender, RoutedEventArgs e)
        {
            if (db == null) return;
            if (db.LocalClient == null) return;

            AddSpaceWindow addSpaceWindow = new AddSpaceWindow();
            if (addSpaceWindow.ShowDialog().Equals(true))
            {
                if (addSpaceWindow.IsAddServer)
                {
                    Space newSpace = new Space { ID = Guid.NewGuid(), Name = addSpaceWindow.Text, Address = IPAddress.Any.ToString(), Port = 0, IsLocal = true };
                    db.Spaces.Add(newSpace);
                    db.LocalClient.Spaces.Add(newSpace);
                    newSpace.Clients.Add(db.LocalClient);
                    db.SaveChanges();

                    Channel newChannel = new Channel { ID = Guid.NewGuid(), Name = "General" };
                    db.Channels.Add(newChannel);
                    newSpace.Channels.Add(newChannel);
                    db.SaveChanges();

                    var spaceNetworker = new SpaceNetworker(db);
                    await spaceNetworker.HostSpace(newSpace);

                    mainWindow.SetCurrentSpace(newSpace);
                    await mainWindow.SetCurrentChannel(newChannel);
                }
                else
                {
                    IPEndPoint spaceHostCoastToCoast = IPEndPoint.Parse(addSpaceWindow.Text);

                    SpaceNetworker networker = new SpaceNetworker(db);
                    Space newSpace = await networker.ConnectToSpace(spaceHostCoastToCoast);
                    mainWindow.SetCurrentSpace(newSpace);
                    await mainWindow.SetCurrentChannel(newSpace.Channels.First());
                }
            }
        }

        private void DataGrid_CurrentCellChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            //Get the newly selected cells
            IList<DataGridCellInfo> selectedCells = e.AddedCells;
            if (selectedCells.Count() != 0)
            {
                mainWindow.SetCurrentSpace((Space)selectedCells[0].Item);
            }
        }

    }
}
