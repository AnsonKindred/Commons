using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using Microsoft.EntityFrameworkCore;
using NAudio.Wave;
using NobleConnect;

namespace Commons
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly CommonsContext _context = new CommonsContext();

        private Client? localClient;
        private Server? currentServer;
        private CollectionViewSource serversViewSource;

        public static DataGrid? TheServerList;

        private Dictionary<Server, ServerNetworker> serverNetworkers = new();

        WaveInEvent waveIn;

        public MainWindow()
        {
            Logger.logger = (s) => Trace.WriteLine(s);
            Logger.logLevel = Logger.Level.Debug;

            InitializeComponent();
            serversViewSource = (CollectionViewSource)FindResource(nameof(serversViewSource));

            waveIn = new WaveInEvent();
            waveIn.StartRecording();
            waveIn.DataAvailable += WaveDataIn;
        }

        void WaveDataIn(object? sender, WaveInEventArgs waveArgs)
        {
            //waveArgs.Buffer, 0, waveArgs.BytesRecorded);
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (var kv in serverNetworkers)
            {
                kv.Value.Dispose();
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TheServerList = ServerList;

            // this is for demo purposes only, to make it easier
            // to get up and running
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();

            _context.Servers.Load();
            _context.Chats.Load();
            _context.Clients.Load();

            // Set server list data source so it pulls from the Servers table
            serversViewSource.Source = _context.Servers.Local.ToObservableCollection();

            foreach (Server server in _context.Servers.Where(s => s.IsLocal))
            {
                var serverNetworker = new ServerNetworker(_context, server);
                await serverNetworker.StartHosting();
                serverNetworkers.Add(server, serverNetworker);
            }

            if (_context.Clients.Count() == 0)
            {
                LoginWindow loginWindow = new LoginWindow();
                if (loginWindow.ShowDialog().Equals(true))
                {
                    localClient = _context.Clients.Where(c => c.Name == loginWindow.LoginName).FirstOrDefault();
                    if (localClient == null)
                    {
                        localClient = new Client { Name = loginWindow.LoginName, Guid = Guid.NewGuid() };
                        _context.Clients.Add(localClient);
                        _context.SaveChanges();
                    }
                }
            }
            else
            {
                localClient = _context.Clients.First();
            }
            if (localClient != null && _context.Servers.Count() != 0)
            {
                await SetCurrentServer(_context.Servers.First());
            }
        }

        private async void SendChat(object sender, RoutedEventArgs e)
        {
            if (localClient == null) return;
            if (currentServer == null) return;

            Chat newChat = new Chat {
                ServerID = currentServer.ID,
                ClientID = localClient.ID,
                Server = currentServer,
                Client = localClient,
                Content = ((TextBox)sender).Text,
                Timestamp = DateTime.UtcNow.Ticks
            };
            _context.Chats.Add(newChat);
            _context.SaveChanges();

            await serverNetworkers[currentServer].ControlPeer.SendChat(newChat);
        }

        private void AddChatText(Chat chat)
        {
            if (chat.Client == null) return;

            Run newRun = new Run(chat.Client.Name + ": " + chat.Content);
            Paragraph newParagraph = new Paragraph(newRun);
            newParagraph.Style = (Style)Resources["ChatParagraph"];
            chatWindow.Document.Blocks.Add(newParagraph);
        }

        private void TextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                SendChat(sender, e);
                ((TextBox)sender).Text = "";
            }
        }

        private async void AddServer_Button_Click(object sender, RoutedEventArgs e)
        {
            if (localClient == null) return;

            AddServerWindow addServerWindow = new AddServerWindow();
            if (addServerWindow.ShowDialog().Equals(true))
            {
                Server newServer = new Server { Name = addServerWindow.ServerName, Address = IPAddress.Any.ToString(), Port = 0, IsLocal = true };
                localClient.Servers.Add(newServer);
                _context.SaveChanges();

                var serverNetworker = new ServerNetworker(_context, newServer);
                serverNetworkers.Add(newServer, serverNetworker);
                Trace.WriteLine("Made it here");
                await serverNetworker.StartHosting();
                Trace.WriteLine("but here?");

                await SetCurrentServer(newServer);
            }
        }

        private async void LinkServer_Button_Click(object sender, RoutedEventArgs e)
        {
            if (localClient == null) return;

            LinkServerWindow linkServerWindow = new LinkServerWindow();
            if (linkServerWindow.ShowDialog().Equals(true))
            {
                IPEndPoint endpoint = IPEndPoint.Parse(linkServerWindow.ServerLink);
                Server newServer = new Server { Name = "Connecting...", Address = endpoint.Address.ToString(), Port = endpoint.Port, IsLocal = false };
                localClient.Servers.Add(newServer);
                _context.SaveChanges();
                await SetCurrentServer(newServer);
            }
        }

        private async Task SetCurrentServer(Server server)
        {
            if (localClient == null) return;

            Trace.WriteLine("Setting current server: " + server.ID);
            if (currentServer != null)
            {
                ((ObservableCollection<Chat>)currentServer.Chats).CollectionChanged -= OnChatsChanged;
            }
            chatWindow.Document.Blocks.Clear();
            currentServer = server;
            foreach (Chat chat in currentServer.Chats)
            {
                AddChatText(chat);
            }

            ((ObservableCollection<Chat>)currentServer.Chats).CollectionChanged += OnChatsChanged;

            if (!currentServer.IsLocal)
            {
                ServerNetworker? networker = null;
                serverNetworkers.TryGetValue(currentServer, out networker);
                if (networker == null)
                {
                    networker = new ServerNetworker(_context, currentServer);
                    serverNetworkers.Add(currentServer, networker);
                    await networker.JoinServer();
                }

                await networker.ControlPeer.SendClient(localClient);
                await networker.ControlPeer.SendCommand(ControlPeer.Command.GET_CLIENTS);

                Chat? latestChat = currentServer.Chats.MaxBy(c => c.Timestamp);
                long latestTime = latestChat == null ? 0 : latestChat.Timestamp;
                await networker.ControlPeer.SendCommand(ControlPeer.Command.GET_CHATS, null, BitConverter.GetBytes(latestTime));
            }
        }

        private void OnChatsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems == null) return;
            foreach (Chat chat in e.NewItems)
            {
                AddChatText(chat);
            }
        }

        public void Window_SourceInitialized(object sender, EventArgs e) => App.SetWindowDarkMode((Window)sender);

        private async void DataGrid_CurrentCellChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            //Get the newly selected cells
            IList<DataGridCellInfo> selectedCells = e.AddedCells;
            if (selectedCells.Count() != 0)
            {
                await SetCurrentServer((Server)selectedCells[0].Item);
            }
        }

        private void Invite_Clicked(object sender, EventArgs e)
        {
            if (currentServer == null) return;

            InviteToServerWindow inviteToServerWindow = new InviteToServerWindow();
            inviteToServerWindow.ServerLink = currentServer.Address + ":" + currentServer.Port;
            if (inviteToServerWindow.ShowDialog().Equals(true))
            {
            }
        }
    }
}
