using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using FinanceAppMVC.Models;
using System.Net;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using PortfolioQuadraticOptimization;
using PortfolioQuadraticOptimization.DataContracts;
using System.IO;

namespace FinanceAppMVC.Controllers
{
    public class PortfolioController : Controller
    {
        private DataContext db = new DataContext();

        // GET: /Portfolio/
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult List()
        {
            return PartialView("List", db.Portfolios.ToList());
        }

        // GET: /Portfolio/Details/5
        public ActionResult Details(int id = 0)
        {
            Portfolio portfolio = db.Portfolios.Find(id);
            if (portfolio == null)
            {
                return RedirectToAction("Index");
            }
            return View(portfolio);
        }

        // GET: /Portfolio/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: /Portfolio/Create
        [HttpPost]
        public ActionResult Create(Portfolio portfolio)
        {
            if (ModelState.IsValid)
            {
                portfolio.DateCreated = DateTime.Now;
                portfolio.Assets = new List<Asset>();
                db.Portfolios.Add(portfolio);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(portfolio);
        }

        public ActionResult EditDefaultStartDate(int id)
        {
            var portfolio = db.Portfolios.Find(id);
            return PartialView(portfolio);
        }

        [HttpPost]
        public ActionResult EditDefaultStartDate(Portfolio portfolio)
        {
            try
            {
                var oldPortfolio = db.Portfolios.Find(portfolio.ID);
                oldPortfolio.DefaultStartDate = portfolio.DefaultStartDate;
                db.Entry(oldPortfolio).State = EntityState.Modified;
                db.SaveChanges();
                return Json(new { ReturnValue = 0, Message = "Default start date has been changed." });
            }
            catch (Exception e)
            {
                return Json(new { ReturnValue = -1, Message = e.Message });
            }
        }

        public ActionResult AddAsset(int portfolioID)
        {
            return PartialView("AddAsset", new Asset { PortfolioID = portfolioID });
        }

        [HttpPost]
        public ActionResult AddAsset(Asset asset)
        {
            Portfolio portfolio = db.Portfolios.Find(asset.PortfolioID);
            if (ModelState.IsValid && portfolio != null)
            {
                asset.Prices = getQuotes(asset.Symbol);
                asset.Portfolio = portfolio;
                db.Assets.Add(asset);
                db.SaveChanges();
            }
            return AssetList(asset.PortfolioID);
        }

        public ActionResult AssetList(int id = 0)
        {
            Portfolio portfolio = db.Portfolios.Include(p => p.Assets).Where(p => p.ID == id).FirstOrDefault();
            if (portfolio == null)
            {
                return RedirectToAction("Index");
            }

            return PartialView("AssetList", portfolio.Assets.OrderBy(a => a.ID).ToList());
        }

        [HttpPost]
        public ActionResult CalculateAssetStats(String meanRateMethod, String expectedRateMethod, String riskFreeRate, String MRP, String date ="", int id = 0)
        {
            Portfolio portfolio = db.Portfolios.Include(p => p.Assets).Where(p => p.ID == id).FirstOrDefault();
            List<Asset> assets = db.Assets.Include(a => a.Portfolio).Include(a => a.Prices).Where(a => a.PortfolioID == id).ToList();
            DateTime startDate = DateTime.Parse(date);
        
            bool meanRateMethodIsSimple;

            if (meanRateMethod.Equals("Simple"))
                meanRateMethodIsSimple = true;
            else
                meanRateMethodIsSimple = false;

            bool expectedRateMethodIsCAPM;

            if (expectedRateMethod.Equals("CAPM"))
                expectedRateMethodIsCAPM = true;
            else
                expectedRateMethodIsCAPM = false;

            portfolio.isSimple = meanRateMethodIsSimple;
            portfolio.isCAPM = expectedRateMethodIsCAPM;

            if (riskFreeRate == null || MRP == null)
            {
                riskFreeRate = "0";
                MRP = "0";
            }

            portfolio.riskFreeRate = Double.Parse(riskFreeRate);
            portfolio.MRP = Double.Parse(MRP);

            foreach (Asset asset in assets)
            {
                calculateAssetStats(asset, startDate, meanRateMethodIsSimple, expectedRateMethodIsCAPM, portfolio.riskFreeRate, portfolio.MRP);
            }
            portfolio.statsCalculated = true;
            db.SaveChanges();
            return AssetList(id);
        }

        public ActionResult Asset(int id, String meanRateMethod, String expectedRateMethod, String riskFreeRate, String MRP, String date = "", bool dateIsModified = false)
        {
            Asset asset = db.Assets.Include(a => a.Portfolio).Include(a => a.Prices).Where(a => a.ID == id).First();
            DateTime startDate;

            bool meanRateMethodIsSimple;

            if (meanRateMethod.Equals("Simple"))
                meanRateMethodIsSimple = true;
            else
                meanRateMethodIsSimple = false;

            bool expectedRateMethodIsCAPM;

            if (expectedRateMethod.Equals("CAPM"))
                expectedRateMethodIsCAPM = true;
            else
                expectedRateMethodIsCAPM = false;

            if (riskFreeRate.Equals("") || MRP.Equals(""))
            {
                riskFreeRate = "0";
                MRP = "0";
            }

            if (date == "")
                startDate = asset.Portfolio.DefaultStartDate;
            else
                startDate = DateTime.Parse(date);

            if (dateIsModified)
                calculateAssetStats(asset, startDate, meanRateMethodIsSimple, expectedRateMethodIsCAPM, Double.Parse(riskFreeRate), Double.Parse(MRP));

            ViewBag.Date = startDate;

            return View("AssetDetails", asset);
        }

        [HttpPost]
        public ActionResult DeleteAsset(int id = 0)
        {
            int portfolioID = 0;
            Asset asset = db.Assets.Find(id);
            if (asset != null)
            {
                portfolioID = asset.PortfolioID;
                db.Assets.Remove(asset);
                db.SaveChanges();
            }
            return AssetList(portfolioID);
        }

        public ActionResult RiskAnalysis(String meanRateMethod, int id = 0)
        {
            Portfolio portfolio = db.Portfolios.Include("Assets.Prices").Where(p => p.ID == id).FirstOrDefault();
            List<Asset> assets = portfolio.Assets.ToList();

            DateTime startDate;
            double[,] covarianceMatrix = new double[assets.Count, assets.Count];
            double[,] correlationMatrix = new double[assets.Count, assets.Count];

            bool meanRateMethodIsSimple;
            if (meanRateMethod.Equals("Simple"))
                meanRateMethodIsSimple = true;
            else
                meanRateMethodIsSimple = false;

            startDate = portfolio.DefaultStartDate;

            double totalInverseVolatility = 0;
            foreach (Asset asset in assets)
            {
                totalInverseVolatility += (1 / asset.AnnualizedStandardDeviation);
            }

            ViewBag.TotalInverseVolatility = totalInverseVolatility;


            for (int i = 0; i < assets.Count; i++)
            {
                for (int j = 0; j < assets.Count; j++)
                {
                    List<AssetPrice> prices_i = assets[i].Prices.Where(p => p.Date >= startDate.AddDays(1)).ToList();
                    List<AssetPrice> prices_j = assets[j].Prices.Where(p => p.Date >= startDate.AddDays(1)).ToList();
                    double covariance = calculateCovariance(prices_i, assets[i].DailyMeanRate, prices_j, assets[j].DailyMeanRate, meanRateMethodIsSimple);

                    covarianceMatrix[i, j] = covariance;
                    correlationMatrix[i, j] = calculateCorrelation(prices_i, assets[i].DailyMeanRate, prices_j, assets[j].DailyMeanRate,
                        covariance, meanRateMethodIsSimple);
                }
            }

            ViewBag.CovarianceMatrix = covarianceMatrix;
            ViewBag.CorrelationMatrix = correlationMatrix;


            return View("RiskAnalysis", assets);
        }

        // POST: /Portfolio/Delete/5
        [HttpPost]
        public ActionResult Delete(int id)
        {
            Portfolio portfolio = db.Portfolios.Find(id);
            db.Portfolios.Remove(portfolio);
            db.SaveChanges();
            return List();
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }

        [HttpPost]
        public ActionResult SetPortfolioAllocation(int portfolioId, string weightList)
        {
            Portfolio portfolio = db.Portfolios.Include("Assets.Prices").Where(p => p.ID == portfolioId).FirstOrDefault();

            if (portfolio == null)
            {
                return Json(new { Message = "Error: Portfolio with ID " + portfolioId + " not found.", StatusCode = -1 });
            }
            else
            {
                JArray weightsJSon = (JArray)JsonConvert.DeserializeObject(weightList);
                for (int i = 0; i < weightsJSon.Count; i++)
                {
                    JToken token = weightsJSon[i];
                    string symbol = token["symbol"].ToString();
                    double weight = Double.Parse(token["weight"].ToString());

                    Asset asset = portfolio.Assets.Where(a => a.Symbol == symbol).First();
                    asset.Weight = weight;
                }
                db.SaveChanges();

                return Json(new { Message = "Portfolio asset allocations were saved successfully.", StatusCode = 0 });
            }
        }

        public ActionResult PortfolioStatistics(int id, String meanRateMethod, String expectedRateMethod, String riskFreeRate, String MRP, String date = "")
        {
            Portfolio portfolio = db.Portfolios.Include("Assets.Prices").Where(p => p.ID == id).FirstOrDefault();

            bool meanRateMethodIsSimple;

            if (meanRateMethod.Equals("Simple"))
                meanRateMethodIsSimple = true;
            else
                meanRateMethodIsSimple = false;

           DateTime startDate = portfolio.DefaultStartDate;

            List<Asset> assets = portfolio.Assets.ToList();
            double val_MeanRateOfReturn = 0;
            double val_StandardDeviation = 0;
            double val_MarketCorrelation = 0;
            double val_Variance = 0;
            double val_Beta = 0;
            double[,] covarianceMatrix = new double[assets.Count + 1, assets.Count + 1];

            List<AssetPrice> riskFreeRates = getQuotes("^IRX").ToList().Where(price => price.Date >= startDate).ToList();
            riskFreeRates.RemoveAt(0);

            List<AssetPrice> marketRates = getQuotes("SPY").ToList().Where(price => price.Date >= startDate).ToList();
            marketRates.RemoveAt(0);
            double meanMarketRate = calculateDailyMeanRate(marketRates, meanRateMethodIsSimple);
            double expectedValue_Market = calculateExpectedValue(marketRates, riskFreeRates, meanRateMethodIsSimple);

            foreach (Asset a in assets)
            {

                val_StandardDeviation += a.AnnualizedStandardDeviation * a.Weight;
                val_MarketCorrelation += a.Covariance * a.Weight;
                val_MeanRateOfReturn += a.AnnualizedMeanRate * a.Weight;
                val_Beta = a.Beta * a.Weight;
            }

            portfolio.meanRateOfReturn = val_MeanRateOfReturn;
            portfolio.beta = val_Beta;

            //portfolio return variance
            val_Variance = 0;
            for (int i = 0; i < assets.Count; i++)
            {
                for (int j = 0; j < assets.Count; j++)
                {
                    List<AssetPrice> prices = assets[j].Prices.Where(p => p.Date >= startDate.AddDays(1)).ToList();
                    double meanRate = assets[j].DailyMeanRate;
                    if (i == assets.Count)
                    {
                        covarianceMatrix[i, j] = calculateCovariance(marketRates, expectedValue_Market,
                            prices, meanRate, meanRateMethodIsSimple);
                    }
                    else if (j == assets.Count)
                    {
                        covarianceMatrix[i, j] = calculateCovariance(prices, meanRate,
                            marketRates, expectedValue_Market, meanRateMethodIsSimple);
                    }
                    else if (i == assets.Count && j == assets.Count)
                    {
                        covarianceMatrix[i, j] = calculateCovariance(marketRates, expectedValue_Market, marketRates, expectedValue_Market, meanRateMethodIsSimple);
                    }
                    else
                    {
                        covarianceMatrix[i, j] = calculateCovariance(prices, meanRate, prices, meanRate, meanRateMethodIsSimple);
                        val_Variance += covarianceMatrix[i, j] * assets[i].Weight * assets[j].Weight;
                    }
                }
            }
            double stdDev = Math.Sqrt(val_Variance);
            portfolio.marketCorrelation = val_MarketCorrelation / (Math.Sqrt(calculateVariance(marketRates, meanMarketRate, meanRateMethodIsSimple)) * stdDev);
            portfolio.standardDeviation = stdDev * Math.Sqrt(252);
            portfolio.sharpeRatio = portfolio.meanRateOfReturn / portfolio.standardDeviation;

            return View("PortfolioStatistics", portfolio);
        }

        private Asset calculateAssetStats(Asset asset, DateTime startDate, bool meanRateMethodIsSimple, bool expectedRateMethodIsCAPM, double riskFreeRate, double MRP)
        {
            List<AssetPrice> queriedPrices = asset.Prices.Where(p => p.Date >= startDate).ToList();
            queriedPrices.RemoveAt(0);
            //asset.Prices = queriedPrices;

            List<AssetPrice> riskFreeRates = getQuotes("^IRX").Where(p => p.Date >= startDate).ToList();
            riskFreeRates.RemoveAt(0);
            double meanRiskFreeRate = riskFreeRates.Sum(p => p.ClosePrice) / (riskFreeRates.Count);

            List<AssetPrice> marketRates = getQuotes("SPY").Where(p => p.Date >= startDate).ToList();
            double expectedValue_Asset = calculateExpectedValue(queriedPrices, riskFreeRates, meanRateMethodIsSimple);
            double expectedValue_Market = calculateExpectedValue(marketRates, riskFreeRates, meanRateMethodIsSimple);
            double variance_Asset = calculateRiskFreeVariance(queriedPrices, riskFreeRates, expectedValue_Asset, meanRateMethodIsSimple);
            double variance_Market = calculateRiskFreeVariance(marketRates, riskFreeRates, expectedValue_Market, meanRateMethodIsSimple);
            double covariance = calculateCovariance(queriedPrices, marketRates, riskFreeRates, expectedValue_Asset, expectedValue_Market, meanRateMethodIsSimple);


            asset.DailyMeanRate = calculateDailyMeanRate(queriedPrices, meanRateMethodIsSimple);

            asset.DailyVariance = calculateVariance(queriedPrices, asset.DailyMeanRate, meanRateMethodIsSimple);
            asset.AnnualizedVariance = asset.DailyVariance * 252;

            asset.DailyStandardDeviation = Math.Sqrt(asset.DailyVariance);
            asset.AnnualizedStandardDeviation = asset.DailyStandardDeviation * Math.Sqrt(252);

            asset.Covariance = covariance;
            asset.SharpeRatio = (expectedValue_Asset * queriedPrices.Count) / Math.Sqrt((variance_Asset * Math.Pow(queriedPrices.Count, 2)) / 252);
            asset.Beta = covariance / variance_Market;
            asset.HistoricalCorrelation = covariance / (Math.Sqrt(variance_Asset) * Math.Sqrt(variance_Market));

            if (expectedRateMethodIsCAPM)
                asset.AnnualizedMeanRate = riskFreeRate + (asset.Beta * MRP);
            else
                asset.AnnualizedMeanRate = asset.DailyMeanRate * 252;

            return asset;
        }

        public ActionResult Optimize(int id, int minimumRateOfReturn, string date = "")
        {
            Portfolio portfolio = db.Portfolios.Include("Assets.Prices").Where(p => p.ID == id).FirstOrDefault();

            DateTime startDate;
            if (date == "")
                startDate = portfolio.DefaultStartDate;
            else
                startDate = DateTime.Parse(date);

            PortfolioQuadraticOptimizationService service = new PortfolioQuadraticOptimizationService();

            OptimizationData inputData = new OptimizationData();
            inputData.MinimumReturn = minimumRateOfReturn;

            List<AssetData> assetData = new List<AssetData>();

            foreach (Asset a in portfolio.Assets)
            {
                var aPrices = a.Prices.Where(x => x.Date >= startDate).ToList();

                a.AnnualizedMeanRate = calculateDailyMeanRate(aPrices, true) * 252;

                var covariances = new Dictionary<string, double>();
                foreach (Asset a2 in portfolio.Assets)
                {
                    var a2Prices = a2.Prices.Where(x => x.Date >= startDate).ToList();
                    var aExpectedVal = calculateExpectedValue(aPrices);
                    var a2ExpecteVal = calculateExpectedValue(a2Prices);
                    covariances.Add(a2.Symbol, calculateCovariance(aPrices, aExpectedVal, a2Prices, a2ExpecteVal, true));
                }

                assetData.Add(new AssetData
                {
                    Symbol = a.Symbol,
                    MeanReturnRate = a.AnnualizedMeanRate,
                    Covariances = covariances
                });
            }

            inputData.Stocks = assetData;

            //OptimizationResult result = service.OptimizePortfolioAllocation(inputData);

            var request = WebRequest.Create("http://optimization.andrewgaspar.com/api/optimize");
            request.ContentType = "application/json";
            request.Method = "POST";
            StreamWriter writer = new StreamWriter(request.GetRequestStream());
            string json = JsonConvert.SerializeObject(inputData);
            writer.Write(json);
            writer.Close();

            OptimizationResult result = new OptimizationResult();

            try
            {
                using (var response = request.GetResponse())
                {
                    request.GetRequestStream().Close();
                    if (response != null)
                    {
                        using (var answerReader = new StreamReader(response.GetResponseStream()))
                        {
                            var readString = answerReader.ReadToEnd();
                            result = JsonConvert.DeserializeObject<OptimizationResult>(readString);
                        }
                    }
                }
            }
            catch (WebException e)
            {
                return RedirectToAction("Index");
            }

            ViewBag.Results = result.Results.ToDictionary(r => r.Symbol, r => r.Allocation);
            ViewBag.Feasible = result.Feasible;
            ViewBag.Optimal = result.Optimal;
            ViewBag.ExpectedRateOfReturn = result.ExpectedReturn;

            return View();
        }

        private double calculateDailyMeanRate(List<AssetPrice> prices, bool meanRateMethodIsSimple)
        {
            double meanRate;

            if (meanRateMethodIsSimple)
                meanRate = prices.Sum(p => p.SimpleRateOfReturn) / (prices.Count);
            else
                meanRate = prices.Sum(p => p.LogRateOfReturn) / (prices.Count);
            return meanRate;
        }

        private double calculateExpectedValue(List<AssetPrice> assetPrices)
        {
            double expectedValue = 0;
            foreach (var p in assetPrices)
            {
                expectedValue += p.SimpleRateOfReturn;
            }
            expectedValue = expectedValue / assetPrices.Count;
            return expectedValue;
        }

        private double calculateVariance(List<AssetPrice> prices, double meanRate, bool meanRateMethodIsSimple)
        {
            double variance = 0;

            if (meanRateMethodIsSimple)
            {
                foreach (AssetPrice price in prices)
                {
                    variance += Math.Pow(price.SimpleRateOfReturn - meanRate, 2);
                }
            }
            else
            {
                foreach (AssetPrice price in prices)
                {
                    variance += Math.Pow(price.LogRateOfReturn - meanRate, 2);
                }
            }

            return variance / prices.Count;
        }

        private double calculateRiskFreeVariance(List<AssetPrice> queriedPrices, List<AssetPrice> riskFreeRates, double expectedValue, bool meanRateMethodIsSimple)
        {
            double variance = 0;

            if (meanRateMethodIsSimple)
            {
                foreach (AssetPrice price in queriedPrices)
                {
                    var rate = riskFreeRates.Where(x => x.Date == price.Date).FirstOrDefault();
                    if (rate != null)
                    {
                        variance += Math.Pow(price.SimpleRateOfReturn - rate.ClosePrice - expectedValue, 2);
                    }
                }
            }
            else
            {
                foreach (AssetPrice price in queriedPrices)
                {
                    var rate = riskFreeRates.Where(x => x.Date == price.Date).FirstOrDefault();
                    if (rate != null)
                    {
                        variance += Math.Pow(price.LogRateOfReturn - rate.ClosePrice - expectedValue, 2);
                    }
                }
            }

            return variance / queriedPrices.Count;
        }

        private double calculateExpectedValue(List<AssetPrice> queriedPrices, List<AssetPrice> riskFreeRates, bool meanRateMethodIsSimple)
        {
            double expectedValue = 0;

            if (meanRateMethodIsSimple)
            {
                foreach (var price in queriedPrices)
                {
                    var rate = riskFreeRates.Where(x => x.Date == price.Date).FirstOrDefault();
                    if (rate != null)
                    {
                        expectedValue += price.SimpleRateOfReturn - rate.ClosePrice;
                    }
                }
            }
            else
            {
                foreach (var price in queriedPrices)
                {
                    var rate = riskFreeRates.Where(x => x.Date == price.Date).FirstOrDefault();
                    if (rate != null)
                    {
                        expectedValue += price.LogRateOfReturn - rate.ClosePrice;
                    }
                }
            }

            return expectedValue / queriedPrices.Count;
        }

        private double calculateCovariance(List<AssetPrice> queriedPrices, List<AssetPrice> marketRates, List<AssetPrice> riskFreeRates,
            double expectedValue_Asset, double expectedValue_Market, bool meanRateMethodIsSimple)
        {
            double covariance = 0;

            if (meanRateMethodIsSimple)
            {
                foreach (var price in queriedPrices)
                {
                    var riskFreeRate = riskFreeRates.Where(x => x.Date == price.Date).FirstOrDefault();
                    var marketRate = marketRates.Where(x => x.Date == price.Date).FirstOrDefault();
                    if (riskFreeRate != null && marketRate != null)
                    {
                        covariance += (price.SimpleRateOfReturn - riskFreeRate.ClosePrice - expectedValue_Asset) *
                            (marketRate.SimpleRateOfReturn - riskFreeRate.ClosePrice - expectedValue_Market);
                    }
                }
            }
            else
            {
                foreach (var price in queriedPrices)
                {
                    var riskFreeRate = riskFreeRates.Where(x => x.Date == price.Date).FirstOrDefault();
                    var marketRate = marketRates.Where(x => x.Date == price.Date).FirstOrDefault();
                    if (riskFreeRate != null && marketRate != null)
                    {
                        covariance += (price.LogRateOfReturn - riskFreeRate.ClosePrice - expectedValue_Asset) *
                            (marketRate.LogRateOfReturn - riskFreeRate.ClosePrice - expectedValue_Market);
                    }
                }
            }

            return covariance / queriedPrices.Count;
        }

        private double calculateCovariance(List<AssetPrice> queriedPrices1, double expectedValue1, List<AssetPrice> queriedPrices2, 
            double expectedValue2, bool meanRateMethodIsSimple)
        {
            double covariance = 0;

            if (meanRateMethodIsSimple)
            {
                foreach (var p in queriedPrices1)
                {
                    var rate = queriedPrices2.Where(x => x.Date == p.Date).FirstOrDefault();
                    if (rate != null)
                    {
                        covariance += ((p.SimpleRateOfReturn - expectedValue1) * (rate.SimpleRateOfReturn - expectedValue2));
                    }
                }
            }
            else
            {
                foreach (var p in queriedPrices1)
                {
                    var rate = queriedPrices2.Where(x => x.Date == p.Date).FirstOrDefault();
                    if (rate != null)
                    {
                        covariance += ((p.LogRateOfReturn - expectedValue1) * (rate.LogRateOfReturn - expectedValue2));
                    }
                }
            }

            covariance = covariance / (queriedPrices1.Count);

            return covariance;
        }

        private double calculateCorrelation(List<AssetPrice> queriedPrices1, double meanRate1, List<AssetPrice> queriedPrices2, double meanRate2,
            double covariance, bool meanRateMethodIsSimple)
        {
            double correlation = covariance / (Math.Sqrt(calculateVariance(queriedPrices1, meanRate1, meanRateMethodIsSimple) * 
                calculateVariance(queriedPrices2, meanRate2, meanRateMethodIsSimple)));

            return correlation;
        }

        private List<AssetPrice> getQuotes(String ticker)
        {
            DateTime endDate = DateTime.Today;
            String url = "http://ichart.yahoo.com/table.csv?s=" + Server.UrlEncode(ticker) + 
                "&a=0&b=1&c=2000" +
                "&d=" + (endDate.Month - 1)
                + "&e=" + endDate.Day + "&f=" + endDate.Year + "&g=d&ignore=.csv";

            using (WebClient client = new WebClient())
            {
                String csv = client.DownloadString(url);
                char[] delims = {',', '\n'};
                String[] quotes = csv.Split(delims);
                List<AssetPrice> assetPrices = new List<AssetPrice>();
                for (int i = 7; i < quotes.Length - 1; i += 7)
                {
                    DateTime date = DateTime.Parse(quotes[i]);
                    double previousDayClosePrice;
                    if (i < quotes.Length - 8)
                        previousDayClosePrice = Double.Parse(quotes[i + 13]);
                    else
                        previousDayClosePrice = -1;
                    double openPrice = Double.Parse(quotes[i + 1]);
                    double closePrice = Double.Parse(quotes[i + 6]);
                    double simpleRateofReturn, logRateOfReturn;
                    if (ticker == "^IRX")
                    {
                        assetPrices.Add(new AssetPrice
                        {
                            Date = date,
                            OpenPrice = openPrice / (100 * 365),
                            ClosePrice = closePrice / (100 * 365),
                            SimpleRateOfReturn = closePrice / (100 * 365),
                            LogRateOfReturn = closePrice / (100 * 365)
                        });
                    }
                    else
                    {
                        if (previousDayClosePrice == -1)
                        {
                            simpleRateofReturn = 0;
                            logRateOfReturn = 0;
                        }
                        else
                        {
                            simpleRateofReturn = (closePrice - previousDayClosePrice) / previousDayClosePrice;
                            logRateOfReturn = Math.Log(closePrice / previousDayClosePrice);
                        }
                        previousDayClosePrice = closePrice;
                        assetPrices.Add(new AssetPrice
                        {
                            Date = date,
                            OpenPrice = openPrice,
                            ClosePrice = closePrice,
                            SimpleRateOfReturn = simpleRateofReturn,
                            LogRateOfReturn = logRateOfReturn
                        });
                    }
                }
                assetPrices = assetPrices.OrderBy(p => p.Date).ToList();
                return assetPrices;

            }
        }
    }
}