using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace PortfolioQuadraticOptimization.DataContracts
{
    [DataContract]
    public class OptimizationData
    {
        public double MinimumRateOfReturn { get; set; }
        public List<AssetData> Assets { get; set; }

        public OptimizationData()
        {
            MinimumRateOfReturn = 0;
            Assets = new List<AssetData>();
        }

        public OptimizationData(int minimumRateOfReturn)
        {
            MinimumRateOfReturn = minimumRateOfReturn;
            Assets = new List<AssetData>();
        }

        public OptimizationData(int minimumRateOfReturn, List<AssetData> assets)
        {
            MinimumRateOfReturn = minimumRateOfReturn;
            Assets = assets;
        }
    }
}