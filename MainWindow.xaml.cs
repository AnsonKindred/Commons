using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using Commons.Audio;
using Microsoft.EntityFrameworkCore;
using NobleConnect;

namespace Commons
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly CommonsContext db = new CommonsContext();

        private CollectionViewSource spacesViewSource;
        private CollectionViewSource channelsViewSource;

        public MainWindow()
        {
            Logger.logger = (s) => Trace.WriteLine(s);
            Logger.logLevel = Logger.Level.Developer;

            AudioController.Init();

            InitializeComponent();
            spacesViewSource = (CollectionViewSource)FindResource(nameof(spacesViewSource));
            channelsViewSource = (CollectionViewSource)FindResource(nameof(channelsViewSource));
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            App.SetWindowDarkMode(this);
            IconHelper.RemoveIcon(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (var space in db.Spaces)
            {
                if (space != null && space.SpaceNetworker != null)
                {
                    space.SpaceNetworker.Dispose();
                }
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // this is for demo purposes only, to make it easier
            // to get up and running
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            db.Spaces.Load();
            db.Chats.Load();
            db.Clients.Load();
            db.Channels.Load();

            // Set space list data source so it pulls from the Spaces table
            spacesViewSource.Source = db.Spaces.Local.ToObservableCollection();

            foreach (Space space in db.Spaces.Where(s => s.IsLocal))
            {
                var spaceNetworker = new SpaceNetworker(db);
                await spaceNetworker.HostSpace(space);
            }

            if (db.Clients.Count() == 0)
            {
                LoginWindow loginWindow = new LoginWindow();
                if (loginWindow.ShowDialog().Equals(true))
                {
                    db.LocalClient = db.Clients.Where(c => c.Name == loginWindow.LoginName).FirstOrDefault();
                    if (db.LocalClient == null)
                    {
                        db.LocalClient = new Client { Name = loginWindow.LoginName, ID = Guid.NewGuid() };
                        db.Clients.Add(db.LocalClient);
                        db.SaveChanges();
                    }
                }
            }
            else
            {
                db.LocalClient = db.Clients.First();
            }
            if (db.LocalClient != null && db.Spaces.Count() != 0)
            {
                SetCurrentSpace(db.Spaces.First());
            }
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

                SetCurrentSpace(newSpace);
                await SetCurrentChannel(newChannel);
            }
        }

        private async void LinkSpace_Button_Click(object sender, RoutedEventArgs e)
        {
            if (db.LocalClient == null) return;

            LinkSpaceWindow linkSpaceWindow = new LinkSpaceWindow();
            if (linkSpaceWindow.ShowDialog().Equals(true))
            {
                IPEndPoint spaceHostCoastToCoast = IPEndPoint.Parse(linkSpaceWindow.SpaceLink);

                SpaceNetworker networker = new SpaceNetworker(db);
                Space newSpace = await networker.ConnectToSpace(spaceHostCoastToCoast);
                SetCurrentSpace(newSpace);
                await SetCurrentChannel(newSpace.Channels.First());
            }
        }

        private void SetCurrentSpace(Space space)
        {
            Trace.WriteLine("Setting current space: " + space.ID);

            db.CurrentSpace = space;

            if (db.CurrentSpace.SpaceNetworker != null)
            {
                AudioController.SetVoipPeer(db.CurrentSpace.SpaceNetworker.VoipPeer);
            }
        }

        private async Task SetCurrentChannel(Channel channel)
        {
            if (db.CurrentSpace == null) throw new NullReferenceException(nameof(db.CurrentSpace));
            if (db.CurrentSpace.SpaceNetworker == null) throw new NullReferenceException(nameof(db.CurrentSpace.SpaceNetworker));

            Trace.WriteLine("Setting current channel: " + channel.ID);

            if (db.CurrentSpace.CurrentChannel != null)
            {
                ((ObservableCollection<Chat>)db.CurrentSpace.CurrentChannel.Chats).CollectionChanged -= OnChatsChanged;
            }
            db.CurrentSpace.CurrentChannel = channel;

            // Set channels list data source so it pulls from the Channels table
            channelsViewSource.Source = db.CurrentSpace.Channels;

            if (!db.CurrentSpace.IsLocal)
            {
                // Get the latest chat messages from the host
                Chat? latestChat = db.CurrentSpace.CurrentChannel.Chats.MaxBy(c => c.Timestamp);
                ulong latestTime = latestChat == null ? 0 : latestChat.Timestamp;
                await db.CurrentSpace.SpaceNetworker.ControlPeer.RequestChats(latestTime, channel);
            }

            chatWindow.Document.Blocks.Clear();
            foreach (Chat chat in db.CurrentSpace.CurrentChannel.Chats)
            {
                AddChatText(chat);
            }

            ((ObservableCollection<Chat>)db.CurrentSpace.CurrentChannel.Chats).CollectionChanged += OnChatsChanged;
        }

        private void OnChatsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems == null) return;
            foreach (Chat chat in e.NewItems)
            {
                AddChatText(chat);
            }
        }

        private async void TextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
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

        private void AddChatText(Chat chat)
        {
            if (chat.Client == null) return;

            Run newRun = new Run(chat.Client.Name + ": " + chat.Content);
            Paragraph newParagraph = new Paragraph(newRun);
            newParagraph.Style = (Style)Resources["ChatParagraph"];
            chatWindow.Document.Blocks.Add(newParagraph);
        }

        private void DataGrid_CurrentCellChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            //Get the newly selected cells
            IList<DataGridCellInfo> selectedCells = e.AddedCells;
            if (selectedCells.Count() != 0)
            {
                SetCurrentSpace((Space)selectedCells[0].Item);
            }
        }

        private void Invite_Clicked(object sender, EventArgs e)
        {
            if (db.CurrentSpace == null) return;

            InviteToSpaceWindow inviteToSpaceWindow = new InviteToSpaceWindow();
            inviteToSpaceWindow.SpaceLink = db.CurrentSpace.Address + ":" + db.CurrentSpace.Port;
            inviteToSpaceWindow.ShowDialog();
        }

        private void OnChannelSelectionChanged(object sender, EventArgs e)
        {

        }

        private async void ProcessLatencyLogs(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("Calculating latency");

            AudioController.StopMonitoringEncoding();
            AudioController.StopMonitoringDecoding();

            await Task.Delay(200);

            Dictionary<ushort, long> encoderLog = new();
            Dictionary<ushort, long> decoderLog = new();

            using (FileStream encoderMonitorFile = new FileStream("encoder.stuff", FileMode.OpenOrCreate, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(encoderMonitorFile))
            {
                try
                {
                    while (true)
                    {
                        ushort sequenceNumber = reader.ReadUInt16();
                        long time = reader.ReadInt64();
                        encoderLog.Add(sequenceNumber, time);
                        Trace.WriteLine("Encoded " + sequenceNumber + " " + time);
                    }
                }
                catch (Exception) { }
            }

            using (FileStream decoderMonitorFile = new FileStream("decoder.stuff", FileMode.OpenOrCreate, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(decoderMonitorFile))
            {
                try
                {
                    while (true)
                    {
                        ushort sequenceNumber = reader.ReadUInt16();
                        long time = reader.ReadInt64();
                        decoderLog.Add(sequenceNumber, time);
                        Trace.WriteLine("Decoded " + sequenceNumber + " " + time);
                    }
                }
                catch (Exception) { }
            }

            long total = 0;
            int count = decoderLog.Count;
            int previousSequenceNumber = -1;
            foreach (var kv in decoderLog)
            {
                ushort sequenceNumber = kv.Key;
                if (sequenceNumber != (previousSequenceNumber + 1))
                {
                    Trace.WriteLine("missing sequence number " + (previousSequenceNumber + 1));
                }
                previousSequenceNumber = sequenceNumber;
                long decodedTime = kv.Value;
                bool hasEncodedTime = encoderLog.TryGetValue(sequenceNumber, out long encodedTime);
                if (!hasEncodedTime)
                {
                    Trace.WriteLine("encoded time missing for: " + sequenceNumber);
                    count--;
                }
                else
                {
                    long dif = decodedTime - encodedTime;
                    if (dif < 0)
                    {
                        Trace.WriteLine("why is diff negative: " + sequenceNumber + " " + decodedTime + " - " + encodedTime + " = " + dif);
                    }
                    total += dif;
                }
            }
            double average = (double)total / count;
            Trace.WriteLine("AVERAGE RECORDED LATENCY: " + average + "us (" + average / 1000 + "ms) " + " (" + average / 1000000 + "s)");
        }
    }
}
