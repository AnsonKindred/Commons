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
    public partial class ChatArea : DockPanel
    {
        CommonsContext? db => DesignerProperties.GetIsInDesignMode(this) ? null : ((App)Application.Current).DB;
        MainWindow mainWindow => (MainWindow)Application.Current.MainWindow;

        public ChatArea()
        {
            InitializeComponent();

            if (db == null) return;
            
            db.PropertyChanged += CorePropertyChanged;
        }

        void CorePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (db == null) return;

            if (e.PropertyName == nameof(db.CurrentSpace))
            {
                this.DataContext = db.CurrentSpace;
            }
        }

        private async void TextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (db == null) return;

            if (db.CurrentSpace == null || db.CurrentSpace.CurrentChannel == null || db.LocalClient == null || db.CurrentSpace.SpaceNetworker == null) return;

            if (e.Key == System.Windows.Input.Key.Enter)
            {
                Chat newChat = new Chat {
                    ChannelID = db.CurrentSpace.CurrentChannel.ID,
                    Channel = db.CurrentSpace.CurrentChannel,
                    ClientID = db.LocalClient.ID,
                    Client = db.LocalClient,
                    Content = ((TextBox)sender).Text,
                    Timestamp = (ulong)DateTime.UtcNow.Ticks
                };
                db.Chats.Add(newChat);
                db.SaveChanges();

                await db.CurrentSpace.SpaceNetworker.ControlPeer.SendChat(newChat);

                ((TextBox)sender).Text = "";
            }
        }

    }
}
