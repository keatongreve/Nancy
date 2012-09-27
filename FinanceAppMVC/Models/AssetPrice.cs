using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace FinanceAppMVC.Models
{
    public class AssetPrice
    {
        public int ID { get; set; }
        public double OpenPrice { get; set; }
        public double ClosePrice { get; set; }
        public double SimpleRateOfReturn { get; set; }
        public double LogRateOfReturn { get; set; }
        public DateTime Date { get; set; }
    }
}
