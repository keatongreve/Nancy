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
            return PartialView(db.Portfolios.ToList());
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

        public ActionResult Asset(int id, String date = "")
        {

            Asset asset = db.Assets.Include(a => a.Prices).Where(a => a.ID == id).First();
            DateTime startDate;

            if (date == "")
                startDate = DateTime.Today.Subtract(System.TimeSpan.FromDays(7));
            else
                startDate = DateTime.Parse(date);

            /* Temporarily replace the asset's prices (all prices) with a list of prices starting 
             * from the requested date. This does not affect anything in the database. */
            List<AssetPrice> queriedPrices = asset.Prices.Where(p => p.Date >= startDate).ToList();
            queriedPrices.RemoveAt(0);
            asset.Prices = queriedPrices;


            asset.DailyMeanRate = asset.Prices.Sum(p => p.LogRateOfReturn) / (asset.Prices.Count - 1);
            asset.AnnualizedMeanRate = asset.DailyMeanRate * 252;

            double aggregateVariance = 0;
            double meanStockPrice = asset.Prices.Sum(p => p.ClosePrice) / (asset.Prices.Count - 1);
            foreach (AssetPrice p in queriedPrices)
            {
                aggregateVariance += Math.Pow(p.LogRateOfReturn - asset.DailyMeanRate, 2);
            }

            asset.DailyVariance = aggregateVariance / (asset.Prices.Count - 1);
            asset.AnnualizedVariance = asset.DailyVariance * 252;

            asset.DailyStandardDeviation = Math.Sqrt(asset.DailyVariance);
            asset.AnnualizedStandardDeviation = asset.DailyStandardDeviation * Math.Sqrt(252);


            List<AssetPrice> treasuryRates = getQuotes("^IRX").Where(p => p.Date >= startDate).ToList();
            treasuryRates.RemoveAt(0);
            double meanTreasuryRate = treasuryRates.Sum(p => p.ClosePrice) / (treasuryRates.Count);
            double covariance = 0;

            double sumDiffReturnIRX = 0;
            foreach (var p in queriedPrices)
            {
                var irxRate = treasuryRates.Where(x => x.Date == p.Date).FirstOrDefault();
                if (irxRate != null)
                    sumDiffReturnIRX += p.LogRateOfReturn - irxRate.ClosePrice;
            }
            double expectedValueRateIRX = sumDiffReturnIRX / queriedPrices.Count;

            List<AssetPrice> StandardAndPoor = getQuotes("SPY").Where(p => p.Date >= startDate).ToList();
            double sumDiffReturnMF = 0;
            foreach (var p in treasuryRates)
            {
                var spPrice = StandardAndPoor.Where(x => x.Date == p.Date).FirstOrDefault();
                if (spPrice != null)
                    sumDiffReturnMF += spPrice.LogRateOfReturn - p.ClosePrice;
            }
            double expectedValueRateMF = sumDiffReturnMF / queriedPrices.Count;

            double sumVariance = 0;
            foreach (var p in queriedPrices)
            {
                var spPrice = StandardAndPoor.Where(x => x.Date == p.Date).FirstOrDefault();
                if (spPrice != null)
                    sumVariance += Math.Pow((p.LogRateOfReturn - spPrice.LogRateOfReturn) - expectedValueRateMF, 2);
            }

            double aggregrateMFDiffVariance = 0;
            foreach (var p in queriedPrices)
            {
                var treasuryRate = treasuryRates.Where(x => x.Date == p.Date).FirstOrDefault();
                var spPrice = StandardAndPoor.Where(y => y.Date == p.Date).FirstOrDefault();
                if (treasuryRate != null && spPrice != null)
                {
                    covariance += ((p.SimpleRateOfReturn - treasuryRate.ClosePrice - expectedValueRateIRX) * (spPrice.LogRateOfReturn - treasuryRate.ClosePrice - expectedValueRateMF));
                    aggregrateMFDiffVariance += Math.Pow(spPrice.LogRateOfReturn - treasuryRate.ClosePrice - expectedValueRateMF, 2);
                }
            }

            covariance = covariance / (queriedPrices.Count);
            aggregateVariance = 0;
            foreach (var p in treasuryRates)
            {
                var treasuryRate = treasuryRates.Where(x => x.Date == p.Date).FirstOrDefault();
                var spPrice = StandardAndPoor.Where(y => y.Date == p.Date).FirstOrDefault();
                if (treasuryRate != null && spPrice != null)
                    aggregateVariance += Math.Pow((spPrice.LogRateOfReturn - treasuryRate.ClosePrice - expectedValueRateMF), 2);
            }
            double variance = aggregateVariance / (treasuryRates.Count);

            double sharpeDiffReturn = 0;
            double aggregateRateDiffVariance = 0;
            foreach (var p in queriedPrices)
            {
                var irxRate = treasuryRates.Where(x => x.Date == p.Date).FirstOrDefault();
                if (irxRate != null)
                {
                    sharpeDiffReturn += (p.LogRateOfReturn - irxRate.ClosePrice);
                    aggregateRateDiffVariance += Math.Pow((p.LogRateOfReturn - irxRate.ClosePrice) - expectedValueRateIRX, 2);
                }
            }

            asset.SharpeRatio = sharpeDiffReturn / Math.Sqrt(queriedPrices.Count * aggregateRateDiffVariance / 252);
            asset.Beta = covariance / variance;
            asset.HistoricalCorrelation = covariance / (Math.Sqrt(aggregateRateDiffVariance / (queriedPrices.Count)) * Math.Sqrt(aggregrateMFDiffVariance / (queriedPrices.Count)));

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

        public ActionResult RiskAnalysis(int id = 0, String date = "")
        {

            List<Asset> assets = db.Assets.Include(a => a.Prices).Where(a => a.PortfolioID == id).ToList();
            DateTime startDate;
            double[,] covarianceMatrix = new double[assets.Count, assets.Count];
            double[,] correlationMatrix = new double[assets.Count, assets.Count];

            if (date == "")
                startDate = DateTime.Today.Subtract(System.TimeSpan.FromDays(7));
            else
                startDate = DateTime.Parse(date);

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
                    correlationMatrix[i, j] = calculateCorrelation(assets[i].Prices.Where(p => p.Date >= startDate.AddDays(1)).ToList(),
                        assets[j].Prices.Where(p => p.Date >= startDate.AddDays(1)).ToList());
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

        private double calculateCorrelation(List<AssetPrice> queriedPrices1, List<AssetPrice> queriedPrices2)
        {
            double correlation = 0;

            correlation = calculateCovariance(queriedPrices1, queriedPrices2) / (calculateStandardDev(queriedPrices1) * calculateStandardDev(queriedPrices2));

            return correlation;
        }

        public ActionResult PortfolioStatistics(String weightList, int portfolioID = 0)
        {
            JArray weightsJSon = (JArray)JsonConvert.DeserializeObject(weightList);
            Portfolio portfolio = db.Portfolios.Include(p => p.Assets).Where(p => p.ID == portfolioID).FirstOrDefault();
            for (int i = 0; i < weightsJSon.Count; i++)
            {
                JToken token = weightsJSon[i];
                string symbol = token["symbol"].ToString();
                double weight = Double.Parse(token["weight"].ToString());

                Asset asset = portfolio.Assets.Where(a => a.Symbol == symbol).First();
                asset.Weight = weight;
                db.SaveChanges();
            }
            return View("PortfolioStatistics", null);
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