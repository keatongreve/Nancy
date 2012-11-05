using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace FinanceAppMVC.Models
{
    public class DataContext : DbContext
    {

        public DataContext()
            : base("DefaultConnection")
        {
        }

        public DbSet<Portfolio> Portfolios { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<AssetPrice> AssetPrices { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Asset>().HasMany<AssetPrice>(a => a.Prices).WithRequired(p => p.Asset).HasForeignKey(p => p.AssetID);
            modelBuilder.Entity<Portfolio>().HasMany<Asset>(p => p.Assets).WithRequired(a => a.Portfolio).HasForeignKey(a => a.PortfolioID);
            base.OnModelCreating(modelBuilder);            
        }
    }
}