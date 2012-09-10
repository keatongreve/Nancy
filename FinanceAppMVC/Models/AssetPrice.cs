using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace FinanceAppMVC.Models
{
    public class AssetPrice
    {
        public double Price { get; set; }
        public DateTime Date { get; set; }
    }
}
