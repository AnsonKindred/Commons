using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using Commons.Audio;
using Microsoft.EntityFrameworkCore;
using NobleConnect;

namespace Commons
{
    public partial class MainWindow : Window
    {
        CommonsContext db => ((App)Application.Current).DB;

        public MainWindow()
        {
            Logger.logger = (s) => Trace.WriteLine(s);
            Logger.logLevel = Logger.Level.Developer;

            AudioController.Init();

            InitializeComponent();
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
            SpaceListPanel.SpacesViewSource.Source = db.Spaces.Local.ToObservableCollection();

            foreach (Space space in db.Spaces.Where(s => s.IsLocal))
            {
                var spaceNetworker = new SpaceNetworker(db);
                await spaceNetworker.HostSpace(space);
            }

            await Task.Delay(1);

            if (db.Clients.Count() == 0)
            {
                LoginWindow loginWindow = new LoginWindow();
                if (loginWindow.ShowDialog() == true)
                {
                    db.LocalClient = db.Clients.Where(c => c.Name == loginWindow.LoginName).FirstOrDefault();
                    if (db.LocalClient == null)
                    {
                        db.LocalClient = new Client { Name = loginWindow.LoginName, ID = Guid.NewGuid() };
                        db.Clients.Add(db.LocalClient);
                        db.SaveChanges();
                    }
                }
                else
                {
                    Application.Current.Shutdown();
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

        public void SetCurrentSpace(Space space)
        {
            Trace.WriteLine("Setting current space: " + space.ID);

            db.CurrentSpace = space;

            // Set channels list data source so it pulls from the Channels table
            ChannelGroupsPanel.ChannelsViewSource.Source = db.CurrentSpace.Channels;

            if (db.CurrentSpace.SpaceNetworker != null)
            {
                AudioController.SetVoipPeer(db.CurrentSpace.SpaceNetworker.VoipPeer);
            }
        }

        public async Task SetCurrentChannel(Channel channel)
        {
            if (db.CurrentSpace == null) throw new NullReferenceException(nameof(db.CurrentSpace));
            if (db.CurrentSpace.SpaceNetworker == null) throw new NullReferenceException(nameof(db.CurrentSpace.SpaceNetworker));

            Trace.WriteLine("Setting current channel: " + channel.ID);

            if (db.CurrentSpace.CurrentChannel != null)
            {
                db.CurrentSpace.CurrentChannel.Chats.CollectionChanged -= OnChatsChanged;
            }
            db.CurrentSpace.CurrentChannel = channel;

            if (!db.CurrentSpace.IsLocal)
            {
                // Get the latest chat messages from the host
                Chat? latestChat = db.CurrentSpace.CurrentChannel.Chats.MaxBy(c => c.Timestamp);
                ulong latestTime = latestChat == null ? 0 : latestChat.Timestamp;
                await db.CurrentSpace.SpaceNetworker.ControlPeer.RequestChats(latestTime, channel);
            }
            ChatAreaPanel.ChatWindow.Document.Blocks.Clear();
            foreach (Chat chat in db.CurrentSpace.CurrentChannel.Chats)
            {
                AddChatText(chat);
            }

            db.CurrentSpace.CurrentChannel.Chats.CollectionChanged += OnChatsChanged;
        }

        private void OnChatsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems == null) return;
            foreach (Chat chat in e.NewItems)
            {
                AddChatText(chat);
            }
        }

        private void AddChatText(Chat chat)
        {
            if (chat.Client == null) throw new NullReferenceException(nameof(chat.Client));

            Run newRun = new Run(chat.Client.Name + ": " + chat.Content);
            Paragraph newParagraph = new Paragraph(newRun);
            newParagraph.Style = (Style)ChatAreaPanel.Resources["ChatParagraph"];
            ChatAreaPanel.ChatWindow.Document.Blocks.Add(newParagraph);
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
