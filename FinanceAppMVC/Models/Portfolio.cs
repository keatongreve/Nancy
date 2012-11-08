using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FinanceAppMVC.Models
{
    public class Portfolio
    {
        [Key]
        public int ID { get; set; }
        public ICollection<Asset> Assets { get; set; }        
        [Display(Name = "Portfolio Name")]
        public string PortfolioName { get; set; }
        [Display(Name="Date Created")]
        public DateTime DateCreated { get; set; }
    }
}