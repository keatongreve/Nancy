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
        [DataMember]
        public string Symbol { get; set; }
        [DataMember]
        public double MeanReturnRate { get; set; }
        [DataMember]
        public Dictionary<string, double> Covariances { get; set; }
    }
}
