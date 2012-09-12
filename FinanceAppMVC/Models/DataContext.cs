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

        public DbSet<Asset> Assets { get; set; }
    }
}