using System;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;

namespace Commons
{
    public class CommonsContext : DbContext, INotifyPropertyChanged
    {
        public DbSet<Chat> Chats { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Space> Spaces { get; set; }
        public DbSet<Channel> Channels { get; set; }

        public Client? LocalClient { get; set; }
        private Space? currentSpace = null;
        public Space? CurrentSpace { 
            get => currentSpace; 
            set {
                currentSpace = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentSpace)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseLazyLoadingProxies().UseSqlite("Data Source=commons-" + DateTime.Now.Ticks + ".db");
        }
    }
}
