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

        public ActionResult Asset(int id, String meanRateMethod, String date = "")
        {
            Asset asset = db.Assets.Include(a => a.Portfolio).Include(a => a.Prices).Where(a => a.ID == id).First();
            DateTime startDate;

            if (date == "")
                startDate = asset.Portfolio.DefaultStartDate;
            else
                startDate = DateTime.Parse(date);

            bool meanRateMethodIsSimple;

            if (meanRateMethod.Equals("Simple"))
                meanRateMethodIsSimple = true;
            else
                meanRateMethodIsSimple = false;

            /* Temporarily replace the asset's prices (all prices) with a list of prices starting 
             * from the requested date. This does not affect anything in the database. */
            List<AssetPrice> queriedPrices = asset.Prices.Where(p => p.Date >= startDate).ToList();
            queriedPrices.RemoveAt(0);
            asset.Prices = queriedPrices;

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
            asset.AnnualizedMeanRate = asset.DailyMeanRate * 252;

            asset.DailyVariance = calculateVariance(queriedPrices, asset.DailyMeanRate, meanRateMethodIsSimple);
            asset.AnnualizedVariance = asset.DailyVariance * 252;

            asset.DailyStandardDeviation = Math.Sqrt(asset.DailyVariance);
            asset.AnnualizedStandardDeviation = asset.DailyStandardDeviation * Math.Sqrt(252);

            asset.SharpeRatio = (expectedValue_Asset * queriedPrices.Count) / Math.Sqrt((variance_Asset * Math.Pow(queriedPrices.Count, 2)) / 252);
            asset.Beta = covariance / variance_Market;
            asset.HistoricalCorrelation = covariance / (Math.Sqrt(variance_Asset) * Math.Sqrt(variance_Market));

            ViewBag.Date = startDate;

            return View("AssetDetails", asset);
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

        public ActionResult RiskAnalysis(int id = 0, String date = "")
        {
            Portfolio portfolio = db.Portfolios.Include("Assets.Prices").Where(p => p.ID == id).FirstOrDefault();
            List<Asset> assets = portfolio.Assets.ToList();

            DateTime startDate;
            double[,] covarianceMatrix = new double[assets.Count, assets.Count];
            double[,] correlationMatrix = new double[assets.Count, assets.Count];

            if (date == "")
                startDate = portfolio.DefaultStartDate;
            else
                startDate = DateTime.Parse(date);

            List<AssetPrice> marketRates = getQuotes("SPY").Where(p => p.Date >= startDate).ToList();

            double totalInverseVolatility = 0;
            foreach (Asset asset in assets)
            {
                List<AssetPrice> prices = asset.Prices.Where(p => p.Date >= startDate).ToList();

                prices[0].SimpleRateOfReturn = 0;
                prices[0].LogRateOfReturn = 0;

                asset.Prices = prices;

                asset.DailyMeanRate = asset.Prices.Sum(p => p.SimpleRateOfReturn) / (asset.Prices.Count - 1);
                asset.AnnualizedMeanRate = asset.DailyMeanRate * 252;

                double aggregateVariance = 0;
                double meanStockPrice = asset.Prices.Sum(p => p.ClosePrice) / (asset.Prices.Count - 1);
                foreach (AssetPrice p in prices)
                {
                    aggregateVariance += Math.Pow(p.SimpleRateOfReturn - asset.DailyMeanRate, 2);
                }

                asset.DailyVariance = aggregateVariance / (asset.Prices.Count - 1);
                asset.AnnualizedVariance = asset.DailyVariance * 252;

                asset.DailyStandardDeviation = Math.Sqrt(asset.DailyVariance);
                asset.AnnualizedStandardDeviation = asset.DailyStandardDeviation * Math.Sqrt(252);

                totalInverseVolatility += (1 / asset.AnnualizedStandardDeviation);
            }

            ViewBag.Date = startDate;
            ViewBag.TotalInverseVolatility = totalInverseVolatility;


            for (int i = 0; i < assets.Count; i++)
            {
                for (int j = 0; j < assets.Count; j++)
                {
                    covarianceMatrix[i, j] = calculateCovariance(assets[i].Prices.Where(p => p.Date >= startDate.AddDays(1)).ToList(),
                        assets[j].Prices.Where(p => p.Date >= startDate.AddDays(1)).ToList());
                }
            }

            ViewBag.CovarianceMatrix = covarianceMatrix;

            for (int i = 0; i < assets.Count; i++)
            {
                for (int j = 0; j < assets.Count; j++)
                {
                    var prices_i = assets[i].Prices.Where(p => p.Date >= startDate.AddDays(1)).ToList();
                    var prices_j =  assets[j].Prices.Where(p => p.Date >= startDate.AddDays(1)).ToList();
                    correlationMatrix[i, j] = calculateCorrelation(prices_i, prices_j, marketRates);
                }
            }

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

        private double calculateCovariance(List<AssetPrice> queriedPrices1, List<AssetPrice> queriedPrices2)
        {
            double covariance = 0;

            double expectedValue_queriedPrices1 = calculateExpectedValue(queriedPrices1);
            double expectedValue_queriedPrices2 = calculateExpectedValue(queriedPrices2);

            foreach (var p in queriedPrices1)
            {
                var rate = queriedPrices2.Where(x => x.Date == p.Date).FirstOrDefault();
                if (rate != null)
                {
                    covariance += ((p.SimpleRateOfReturn - expectedValue_queriedPrices1) * (rate.SimpleRateOfReturn - expectedValue_queriedPrices2));
                }
            }

            covariance = covariance / (queriedPrices1.Count);

            return covariance;
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

        private double calculateStandardDev(List<AssetPrice> assetPrices)
        {
            double standardDev = 0;
            double expectedValue = calculateExpectedValue(assetPrices);

            foreach (var p in assetPrices)
            {
                standardDev += Math.Pow(p.SimpleRateOfReturn - expectedValue, 2);
            }
            standardDev = Math.Sqrt(standardDev / assetPrices.Count);

            return standardDev;


        }

        private double calculateStandardDevWithMarket(List<AssetPrice> assetPrices, List<AssetPrice> marketRates)
        {
            double standardDev = 0;

            foreach (var p in assetPrices)
            {
                var rate = marketRates.Where(x => x.Date == p.Date).FirstOrDefault();
                if (rate != null)
                {
                    standardDev += Math.Pow(p.SimpleRateOfReturn - rate.SimpleRateOfReturn, 2);
                }
            }

            standardDev = Math.Sqrt(standardDev / assetPrices.Count);

            return standardDev;
        }

        private double calculateCorrelation(List<AssetPrice> queriedPrices1, List<AssetPrice> queriedPrices2, List<AssetPrice> marketRates)
        {
            double correlation = 0;

            correlation = calculateCovariance(queriedPrices1, queriedPrices2) / (calculateStandardDev(queriedPrices1) 
                * calculateStandardDev(queriedPrices2));
            double x = calculateCovariance(queriedPrices1, queriedPrices2);
            double y = (calculateStandardDev(queriedPrices1)
                * calculateStandardDev(queriedPrices2));

            return correlation;
        }

        private double calculateCorrelationWithMarket(List<AssetPrice> queriedPrices, List<AssetPrice> marketRates, List<AssetPrice> treasuryRates)
        {
            double correlation = 0;

            correlation = calculateCovarianceWithMarketRates(queriedPrices, marketRates, treasuryRates) / (Math.Sqrt(calculateVariance(treasuryRates, queriedPrices, queriedPrices.Count)) *
                Math.Sqrt(calculateVariance(treasuryRates, marketRates, queriedPrices.Count)));

            return correlation;
        }

        private double calculateVariance(List<AssetPrice> queriedPrices, List<AssetPrice> marketRates, int count)
        {
            double variance = 0;
            double expectedValue_MarketWithTreasury = calculateExpectedValueWithTreasury(marketRates, queriedPrices, count);

            foreach (var p in marketRates)
            {
                var treasuryRate = queriedPrices.Where(x => x.Date == p.Date).FirstOrDefault();
                if (treasuryRate != null)
                {
                    variance += Math.Pow((p.LogRateOfReturn - treasuryRate.ClosePrice) - expectedValue_MarketWithTreasury, 2);
                }
            }

            variance = variance / count;

            return variance;
        }

        private double calculateCovarianceWithMarketRates(List<AssetPrice> queriedPrices, List<AssetPrice> marketRates, List<AssetPrice> treasuryRates)
        {
            double covariance = 0;

            double expectedValue_AssetWithTreasury = calculateExpectedValueWithTreasury(queriedPrices, treasuryRates, queriedPrices.Count);
            double expectedValue_MarketWithTreasury = calculateExpectedValueWithTreasury(marketRates, treasuryRates, queriedPrices.Count);

            foreach (var p in queriedPrices)
            {
                var marketRate = marketRates.Where(x => x.Date == p.Date).FirstOrDefault();
                var treasuryRate = treasuryRates.Where(x => x.Date == p.Date).FirstOrDefault();
                if (marketRate != null && treasuryRate != null)
                {
                    covariance += ((p.SimpleRateOfReturn - treasuryRate.ClosePrice) - expectedValue_AssetWithTreasury) *
                        ((marketRate.LogRateOfReturn - treasuryRate.ClosePrice) - expectedValue_MarketWithTreasury);
                }
            }

            covariance = covariance / (queriedPrices.Count);

            return covariance;
        }

        private double calculateExpectedValueWithTreasury(List<AssetPrice> assetPrices, List<AssetPrice> treasuryRates, int count)
        {
            double expectedValue = 0;
            foreach (var p in assetPrices)
            {
                var rate = treasuryRates.Where(x => x.Date == p.Date).FirstOrDefault();
                if (rate != null)
                {
                    expectedValue += (p.LogRateOfReturn - rate.ClosePrice);
                }
            }
            expectedValue = expectedValue / count;
            return expectedValue;
        }

        private double calculateExpectedValueWithMarket(List<AssetPrice> assetPrices, List<AssetPrice> marketPrices)
        {
            double expectedValue = 0;
            foreach (var p in assetPrices)
            {
                var rate = marketPrices.Where(x => x.Date == p.Date).FirstOrDefault();
                if (rate != null)
                {
                    expectedValue += (p.SimpleRateOfReturn - rate.SimpleRateOfReturn);
                }
            }
            expectedValue = expectedValue / assetPrices.Count;
            return expectedValue;
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

        public ActionResult PortfolioStatistics(int id, String date = "")
        {
            Portfolio portfolio = db.Portfolios.Include("Assets.Prices").Where(p => p.ID == id).FirstOrDefault();

            DateTime startDate;
            if (date == "")
                startDate = portfolio.DefaultStartDate;
            else
                startDate = DateTime.Parse(date);

            List<Asset> assets = portfolio.Assets.ToList();
            double val_MeanRateOfReturn = 0;
            double val_StandardDeviation = 0;
            double val_MarketCorrelation = 0;
            double val_Covariance = 0;
            double val_Variance = 0;
            double val_Sharpe = 0;
            double val_Beta = 0;
            double[,] covarianceMatrix = new double[assets.Count + 1, assets.Count + 1];

            List<AssetPrice> marketRates = getQuotes("SPY").ToList().Where(price => price.Date >= startDate).ToList();
            marketRates.RemoveAt(0);
            List<AssetPrice> treasuryRates = getQuotes("^IRX").ToList().Where(price => price.Date >= startDate).ToList();
            treasuryRates.RemoveAt(0);

            foreach (Asset a in assets)
            {
                List<AssetPrice> queriedPrices = a.Prices.Where(price => price.Date >= startDate).ToList();
                queriedPrices.RemoveAt(0);

                a.AnnualizedMeanRate = calculateExpectedValue(queriedPrices) * 252;
                a.AnnualizedStandardDeviation = calculateStandardDev(queriedPrices) * Math.Sqrt(252);

                val_MeanRateOfReturn += a.AnnualizedMeanRate * a.Weight;
                val_StandardDeviation += a.AnnualizedStandardDeviation * a.Weight;
                val_MarketCorrelation += calculateCovariance(queriedPrices, marketRates) * a.Weight;
                val_Variance = calculateVariance(treasuryRates, marketRates, queriedPrices.Count);
                val_Covariance = calculateCovarianceWithMarketRates(queriedPrices, marketRates, treasuryRates);
                val_Beta += (val_Covariance / val_Variance) * a.Weight;

                double val_SummedSharpe = 0;
                foreach (AssetPrice price in a.Prices)
                {
                    var rate = treasuryRates.Where(x => x.Date == price.Date).FirstOrDefault();
                    if (rate != null)
                    {
                        val_SummedSharpe += price.LogRateOfReturn - rate.ClosePrice;
                    }
                }
                val_Sharpe += val_SummedSharpe / Math.Sqrt(val_Variance * queriedPrices.Count / 252) * a.Weight;
            }

            portfolio.meanRateOfReturn = val_MeanRateOfReturn;
            portfolio.beta = val_Beta;
            ViewBag.Date = startDate;

            //portfolio return variance
            val_Variance = 0;
            for (int i = 0; i < assets.Count; i++)
            {
                for (int j = 0; j < assets.Count; j++)
                {
                    if (i == assets.Count)
                    {
                        covarianceMatrix[i, j] = calculateCovariance(marketRates,
                            assets[j].Prices.Where(p => p.Date >= startDate.AddDays(1)).ToList());
                    }
                    else if (j == assets.Count)
                    {
                        covarianceMatrix[i, j] = calculateCovariance(assets[i].Prices.Where(p => p.Date >= startDate.AddDays(1)).ToList(),
                            marketRates);
                    }
                    else if (i == assets.Count && j == assets.Count)
                    {
                        covarianceMatrix[i, j] = calculateCovariance(marketRates, marketRates);
                    }
                    else
                    {
                        covarianceMatrix[i, j] = calculateCovariance(assets[i].Prices.Where(p => p.Date >= startDate.AddDays(1)).ToList(),
                            assets[j].Prices.Where(p => p.Date >= startDate.AddDays(1)).ToList());
                        val_Variance += covarianceMatrix[i, j] * assets[i].Weight * assets[j].Weight;
                    }
                }
            }
            double stdDev = Math.Sqrt(val_Variance);
            portfolio.marketCorrelation = val_MarketCorrelation / (calculateStandardDev(marketRates) * stdDev);
            portfolio.standardDeviation = stdDev * Math.Sqrt(252);
            portfolio.sharpeRatio = portfolio.meanRateOfReturn / portfolio.standardDeviation;

            return View("PortfolioStatistics", portfolio);
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