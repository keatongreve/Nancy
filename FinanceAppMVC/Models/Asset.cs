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
        public double DailyMeanRate { get; set; }
        public double AnnualizedMeanRate { get; set; }
        public double DailyVariance { get; set; }
        public double AnnualizedVariance { get; set; }
        public double DailyStandardDeviation { get; set; }
        public double AnnualizedStandardDeviation { get; set; }
        public double SharpeRatio { get; set; }
        public double Beta { get; set; }
        public List<AssetPrice> Prices { get; set; }
    }
}