﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PortfolioQuadraticOptimizationService.DataContracts
{
    public class OptimizationResult
    {
        public bool Feasible { get; set; }
        public bool Optimal { get; set; }
        public double ExpectedRateOfReturn { get; set; }
        public List<AssetResult> AssetResults { get; set; }
    }
}