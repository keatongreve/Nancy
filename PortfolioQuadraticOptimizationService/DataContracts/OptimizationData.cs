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
        [DataMember]
        public double MinimumReturn { get; set; }
        [DataMember]
        public List<AssetData> Stocks { get; set; }

        public OptimizationData()
        {
            MinimumReturn = 0;
            Stocks = new List<AssetData>();
        }

        public OptimizationData(int minimumRateOfReturn)
        {
            MinimumReturn = minimumRateOfReturn;
            Stocks = new List<AssetData>();
        }

        public OptimizationData(int minimumRateOfReturn, List<AssetData> assets)
        {
            MinimumReturn = minimumRateOfReturn;
            Stocks = assets;
        }
    }
}