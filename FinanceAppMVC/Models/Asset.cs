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


        public List<AssetPrice> Prices { get; set; }
    }
}