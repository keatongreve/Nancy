using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FinanceAppMVC.Models
{
    public class Asset
    {
        [Key]
        public int ID { get; set; }
        [Required]
        public string Symbol { get; set; }
        public double dailyMeanRate { get; set; }
        public double annualizedMeanRate { get; set; }
        public double dailyVariance { get; set; }
        public double annualizedVariance { get; set; }
        public double dailyStdDev { get; set; }
        public double annualizedStdDev { get; set; }

        public List<AssetPrice> Prices { get; set; }
    }
}