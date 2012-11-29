using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace PortfolioQuadraticOptimization.DataContracts
{
    [DataContract]
    public class AssetData
    {
        public string Symbol { get; set; }
        public double MeanRateOfReturn { get; set; }
        public Dictionary<string, double> Covariances { get; set; }
    }
}
