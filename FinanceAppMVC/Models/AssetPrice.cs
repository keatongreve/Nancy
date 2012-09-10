using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace FinanceAppMVC.Models
{
    public class AssetPrice
    {
        [Key]
        public int ID { get; set; }
        public int AssetID { get; set; }
        public double Price { get; set; }
        public DateTime Date { get; set; }
    }
}
