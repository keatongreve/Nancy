using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using Microsoft.SolverFoundation.Common;
using Microsoft.SolverFoundation.Services;
using Microsoft.SolverFoundation.Solvers;
using PortfolioQuadraticOptimization.DataContracts;

namespace PortfolioQuadraticOptimization.ServiceContracts
{
    [ServiceContract]
    public interface IPortfolioQuadraticOptimizationService
    {
        [OperationContract]
        OptimizationResult OptimizePortfolioAllocation(OptimizationData data);
    }
}
