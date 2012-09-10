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
        public string Symbol { get; set; }
        public DateTime StartDate { get; set; }
        public ICollection<AssetPrice> AssetPrices { get; set; }
    }
}