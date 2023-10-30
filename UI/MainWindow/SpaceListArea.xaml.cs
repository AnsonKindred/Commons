using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Commons.UI
{
    public partial class SpaceListArea : Grid
    {
        CommonsContext db => ((App)Application.Current).DB;
        MainWindow mainWindow => (MainWindow)Application.Current.MainWindow;

        public CollectionViewSource SpacesViewSource { get; internal set; }

        public SpaceListArea()
        {
            InitializeComponent();
            SpacesViewSource = (CollectionViewSource)Resources["spacesViewSource"];
        }

        private async void AddSpace_Button_Click(object sender, RoutedEventArgs e)
        {
            if (db.LocalClient == null) return;

            AddSpaceWindow addSpaceWindow = new AddSpaceWindow();
            if (addSpaceWindow.ShowDialog().Equals(true))
            {
                Space newSpace = new Space { ID = Guid.NewGuid(), Name = addSpaceWindow.SpaceName, Address = IPAddress.Any.ToString(), Port = 0, IsLocal = true };
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
