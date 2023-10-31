using System;
using System.ComponentModel;
using System.Windows;

namespace Commons.UI
{
    public partial class SpaceListEntryTemplate : ResourceDictionary
    {
        CommonsContext? db => DesignerProperties.GetIsInDesignMode(Application.Current.MainWindow) ? null : ((App)Application.Current).DB;

        private void OnInviteToSpaceClicked(object sender, EventArgs e)
        {
            if (db?.CurrentSpace == null) return;

            InviteToSpaceWindow inviteToSpaceWindow = new InviteToSpaceWindow();
            inviteToSpaceWindow.SpaceLink = db.CurrentSpace.Address + ":" + db.CurrentSpace.Port;
            inviteToSpaceWindow.ShowDialog();
        }
    }
}
