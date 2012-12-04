using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PortfolioQuadraticOptimization.DataContracts
{
    public class OptimizationResult
    {
        public bool Feasible { get; set; }
        public bool Optimal { get; set; }
        public double ExpectedReturn { get; set; }
        public List<AssetResult> Results { get; set; }
    }
}