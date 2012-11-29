using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace PortfolioQuadraticOptimizationService.DataContracts
{
    [DataContract]
    public class OptimizationData
    {
        public double MinimumRateOfReturn { get; set; }
        public ICollection<AssetData> Assets { get; set; }

        public OptimizationData()
        {
            MinimumRateOfReturn = 0;
            Assets = new List<AssetData>();
        }

        public OptimizationData(int minimumRateOfReturn, ICollection<AssetData> assets)
        {
            MinimumRateOfReturn = minimumRateOfReturn;
            Assets = assets;
        }
    }
}