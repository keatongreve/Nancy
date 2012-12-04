using Microsoft.SolverFoundation.Services;
using Microsoft.SolverFoundation.Solvers;
using PortfolioQuadraticOptimization.ServiceContracts;
using PortfolioQuadraticOptimization.DataContracts;
using System.Collections.Generic;

namespace PortfolioQuadraticOptimization
{
    public class PortfolioQuadraticOptimizationService : IPortfolioQuadraticOptimizationService
    {
        public OptimizationResult OptimizePortfolioAllocation(OptimizationData data)
        {
            int assetCount = data.Stocks.Count;

            InteriorPointSolver solver = new InteriorPointSolver();
            int[] allocations = new int[assetCount];

            for (int i = 0; i < assetCount; i++)
            {
                solver.AddVariable(data.Stocks[i].Symbol, out allocations[i]);
                if (data.Stocks[i].Symbol == "SPY")
                    solver.SetBounds(allocations[i], 0, 0);
                else
                    solver.SetBounds(allocations[i], 0, 1);
            }

            int expectedRateOfReturn;
            solver.AddRow("expectedRateOfReturn", out expectedRateOfReturn);
            solver.SetBounds(expectedRateOfReturn, data.MinimumReturn, double.PositiveInfinity);

            int unity;
            solver.AddRow("Investments sum to one", out unity);
            solver.SetBounds(unity, 1, 1);

            for (int i = 0; i < assetCount; i++)
            {
                solver.SetCoefficient(expectedRateOfReturn, allocations[i], data.Stocks[i].MeanReturnRate);
                solver.SetCoefficient(unity, allocations[i], 1);
            }

            int variance;
            solver.AddRow("variance", out variance);
            for (int i = 0; i < assetCount; i++)
            {
                for (int j = 0; j < assetCount; j++)
                {
                    solver.SetCoefficient(variance, data.Stocks[i].Covariances[data.Stocks[j].Symbol], allocations[i], allocations[j]);
                }
            }

            solver.AddGoal(variance, 0, true);

            InteriorPointSolverParams lpParams = new InteriorPointSolverParams();

            solver.Solve(lpParams);

            bool optimal = false;
            bool feasible = false;
            if (solver.Result == LinearResult.Optimal)
            {
                optimal = feasible = true;
            }
            else if (solver.Result == LinearResult.Feasible)
            {
                optimal = false;
                feasible = true;
            }

            List<AssetResult> assetResults = new List<AssetResult>();
            for (int i = 0; i < assetCount; i++)
            {
                assetResults.Add(new AssetResult
                {
                    Symbol = data.Stocks[i].Symbol,
                    Allocation = (double)solver.GetValue(allocations[i])
                });
            }

            OptimizationResult result = new OptimizationResult
            {
                Optimal = optimal,
                Feasible = feasible,
                ExpectedReturn = (double)solver.GetValue(expectedRateOfReturn),
                Results = assetResults
            };

            return result;
        }
    }
}
