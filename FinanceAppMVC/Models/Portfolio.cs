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
        [Display(Name="Default Start Date")]
        public DateTime DefaultStartDate { get; set; }
        public double meanRateOfReturn { get; set; }
        public double standardDeviation { get; set; }
        public double marketCorrelation { get; set; }
        public double beta { get; set; }
        public double sharpeRatio { get; set; }
        public double riskFreeRate { get; set; }
        public double MRP { get; set; }
        public bool statsCalculated { get; set; }
        public bool isCAPM { get; set; }
        public bool isSimple { get; set; }
    }
}