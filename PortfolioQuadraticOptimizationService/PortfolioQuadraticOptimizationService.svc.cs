using Microsoft.SolverFoundation.Services;
using Microsoft.SolverFoundation.Solvers;
using PortfolioQuadraticOptimizationService.DataContracts;
using System.Collections.Generic;

namespace PortfolioQuadraticOptimizationService
{
    public class PortfolioQuadraticOptimizationService : IPortfolioQuadraticOptimizationService
    {
        public OptimizationResult OptimizePortfolioAllocation(OptimizationData data)
        {
            int assetCount = data.Assets.Count;

            InteriorPointSolver solver = new InteriorPointSolver();
            int[] allocations = new int[assetCount];

            for (int i = 0; i < assetCount; i++)
            {
                solver.AddVariable(data.Assets[i].Symbol, out allocations[i]);
                solver.SetBounds(allocations[i], 0, 1);
            }

            int expectedRateOfReturn;
            solver.AddRow("expectedRateOfReturn", out expectedRateOfReturn);
            solver.SetBounds(expectedRateOfReturn, data.MinimumRateOfReturn, double.PositiveInfinity);

            int unity;
            solver.AddRow("Investments sum to one", out unity);
            solver.SetBounds(unity, 1, 1);

            for (int i = 0; i < assetCount; i++)
            {
                solver.SetCoefficient(expectedRateOfReturn, allocations[i], data.Assets[i].MeanRateOfReturn);
                solver.SetCoefficient(unity, allocations[i], 1);
            }

            int variance;
            solver.AddRow("variance", out variance);
            for (int i = 0; i < assetCount; i++)
            {
                for (int j = 0; j < assetCount; j++)
                {
                    solver.SetCoefficient(variance, data.Assets[i].Covariances[data.Assets[j].Symbol], allocations[i], allocations[j]);
                }
            }

            solver.AddGoal(variance, 0, true);

            InteriorPointSolverParams lpParams = new InteriorPointSolverParams();

            solver.Solve(lpParams);

            bool optimal = (solver.Result == LinearResult.Optimal);
            bool feasible = (solver.Result == LinearResult.Feasible);

            List<AssetResult> assetResults = new List<AssetResult>();
            for (int i = 0; i < assetCount; i++)
            {
                assetResults.Add(new AssetResult
                {
                    Symbol = data.Assets[i].Symbol,
                    Allocation = (double)solver.GetValue(allocations[i])
                });
            }

            OptimizationResult result;

            if (solver.Result != LinearResult.Optimal)
            {
                result = new OptimizationResult
                {
                    Optimal = false,
                    Feasible = false,
                    ExpectedRateOfReturn = 0,
                    AssetResults = null
                };
            }
            else
            {
                result = new OptimizationResult
                {
                    Optimal = optimal,
                    Feasible = feasible,
                    ExpectedRateOfReturn = (double)solver.GetValue(expectedRateOfReturn),
                    AssetResults = assetResults
                };
            }

            return result;
        }
    }
}
