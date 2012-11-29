using PortfolioQuadraticOptimizationService.DataContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace PortfolioQuadraticOptimizationService
{
    public class PortfolioQuadraticOptimizationService : IPortfolioQuadraticOptimizationService
    {
        public OptimizationResult OptimizePortfolioAllocation(OptimizationData data)
        {
            throw new NotImplementedException();
        }
    }
}
