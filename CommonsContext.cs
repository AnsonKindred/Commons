﻿using System;
using Microsoft.EntityFrameworkCore;

namespace Commons
{
    class CommonsContext : DbContext
    {
        public DbSet<Chat> Chats { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Space> Spaces { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseLazyLoadingProxies().UseSqlite("Data Source=commons-" + DateTime.Now.Ticks + ".db");
        }
    }
}
