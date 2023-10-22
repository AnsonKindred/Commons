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
using System.Windows.Media.Animation;
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
        private readonly CommonsContext _context = new CommonsContext();

        private Client? localClient;
        internal Space? CurrentSpace { get; private set; }
        private CollectionViewSource spacesViewSource;

        public MainWindow()
        {
            Logger.logger = (s) => Trace.WriteLine(s);
            Logger.logLevel = Logger.Level.Developer;

            AudioController.Init();

            InitializeComponent();
            spacesViewSource = (CollectionViewSource)FindResource(nameof(spacesViewSource));
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (var space in _context.Spaces)
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
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();

            _context.Spaces.Load();
            _context.Chats.Load();
            _context.Clients.Load();

            // Set space list data source so it pulls from the Spaces table
            spacesViewSource.Source = _context.Spaces.Local.ToObservableCollection();

            foreach (Space space in _context.Spaces.Where(s => s.IsLocal))
            {
                var spaceNetworker = new SpaceNetworker(_context, space);
                await spaceNetworker.HostSpace();
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
            if (localClient != null && _context.Spaces.Count() != 0)
            {
                await SetCurrentSpace(_context.Spaces.First());
            }
        }

        private async void SendChat(object sender, RoutedEventArgs e)
        {
            if (localClient == null) return;
            if (CurrentSpace == null) return;
            if (CurrentSpace.SpaceNetworker == null) return;

            Chat newChat = new Chat {
                SpaceID = CurrentSpace.ID,
                ClientID = localClient.ID,
                Space = CurrentSpace,
                Client = localClient,
                Content = ((TextBox)sender).Text,
                Timestamp = DateTime.UtcNow.Ticks
            };
            _context.Chats.Add(newChat);
            _context.SaveChanges();

            await CurrentSpace.SpaceNetworker.ControlPeer.SendChat(newChat);
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

        private async void AddSpace_Button_Click(object sender, RoutedEventArgs e)
        {
            if (localClient == null) return;

            AddSpaceWindow addSpaceWindow = new AddSpaceWindow();
            if (addSpaceWindow.ShowDialog().Equals(true))
            {
                Space newSpace = new Space { Name = addSpaceWindow.SpaceName, Address = IPAddress.Any.ToString(), Port = 0, IsLocal = true };
                localClient.Spaces.Add(newSpace);
                _context.SaveChanges();

                var spaceNetworker = new SpaceNetworker(_context, newSpace);
                await spaceNetworker.HostSpace();

                await SetCurrentSpace(newSpace);
            }
        }

        private async void LinkSpace_Button_Click(object sender, RoutedEventArgs e)
        {
            if (localClient == null) return;

            LinkSpaceWindow linkSpaceWindow = new LinkSpaceWindow();
            if (linkSpaceWindow.ShowDialog().Equals(true))
            {
                IPEndPoint endpoint = IPEndPoint.Parse(linkSpaceWindow.SpaceLink);
                Space newSpace = new Space { Name = "Connecting...", Address = endpoint.Address.ToString(), Port = endpoint.Port, IsLocal = false };
                localClient.Spaces.Add(newSpace);
                _context.SaveChanges();
                await SetCurrentSpace(newSpace);
            }
        }

        private async Task SetCurrentSpace(Space space)
        {
            if (localClient == null) return;

            Trace.WriteLine("Setting current space: " + space.ID);
            if (CurrentSpace != null)
            {
                ((ObservableCollection<Chat>)CurrentSpace.Chats).CollectionChanged -= OnChatsChanged;
            }
            chatWindow.Document.Blocks.Clear();
            CurrentSpace = space;
            foreach (Chat chat in CurrentSpace.Chats)
            {
                AddChatText(chat);
            }

            ((ObservableCollection<Chat>)CurrentSpace.Chats).CollectionChanged += OnChatsChanged;

            if (!CurrentSpace.IsLocal)
            {
                SpaceNetworker? networker = CurrentSpace.SpaceNetworker;
                if (networker == null)
                {
                    networker = new SpaceNetworker(_context, CurrentSpace);
                    await networker.JoinSpace();
                }

                await networker.ControlPeer.SendClient(localClient);
                await networker.ControlPeer.SendCommand(ControlPeer.Command.GET_CLIENTS);

                Chat? latestChat = CurrentSpace.Chats.MaxBy(c => c.Timestamp);
                long latestTime = latestChat == null ? 0 : latestChat.Timestamp;
                await networker.ControlPeer.SendCommand(ControlPeer.Command.GET_CHATS, BitConverter.GetBytes(latestTime));
            }

            if (CurrentSpace.SpaceNetworker != null)
            {
                AudioController.SetVoipPeer(CurrentSpace.SpaceNetworker.VoipPeer);
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
                await SetCurrentSpace((Space)selectedCells[0].Item);
            }
        }

        private void Invite_Clicked(object sender, EventArgs e)
        {
            if (CurrentSpace == null) return;

            InviteToSpaceWindow inviteToSpaceWindow = new InviteToSpaceWindow();
            inviteToSpaceWindow.SpaceLink = CurrentSpace.Address + ":" + CurrentSpace.Port;
            inviteToSpaceWindow.ShowDialog();
        }
    }
}
