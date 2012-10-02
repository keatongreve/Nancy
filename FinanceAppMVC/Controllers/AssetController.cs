using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using FinanceAppMVC.Models;
using System.Web.Security;
using System.Net;
using System.Diagnostics;

namespace FinanceAppMVC.Controllers
{
    public class AssetController : Controller
    {
        private DataContext db = new DataContext();

        public ActionResult AssetList()
        {
            return PartialView("AssetList", db.Assets.ToList());
        }

        public ActionResult Create()
        {
            return PartialView("Create");
        }

        // POST: /Asset/Create
        [HttpPost]
        public ActionResult Create(Asset asset)
        {
            if (ModelState.IsValid)
            {
                asset.Prices = getQuotes(asset.Symbol);
                db.Assets.Add(asset);
                db.SaveChanges();
            }
            return AssetList();
        }

        // GET: /Asset/Details/5?date=...
        [OutputCache(Duration = 3600, VaryByParam = "date")]
        public ActionResult Details(int id, String date = "")
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
            asset.Prices = queriedPrices;

            asset.DailyMeanRate = queriedPrices.Sum(p => p.SimpleRateOfReturn) / (queriedPrices.Count - 1);
            asset.AnnualizedMeanRate = asset.DailyMeanRate * 252;

            double aggregateVariance = 0;
            double meanStockPrice = asset.Prices.Sum(p => p.ClosePrice) / (asset.Prices.Count);
            asset.Prices.ForEach(p => aggregateVariance += Math.Pow(p.ClosePrice - meanStockPrice, 2));
            asset.DailyVariance = aggregateVariance / (asset.Prices.Count - 1);
            asset.AnnualizedVariance = asset.DailyVariance * 252;

            asset.DailyStandardDeviation = Math.Sqrt(asset.DailyVariance);
            asset.AnnualizedStandardDeviation = asset.DailyStandardDeviation * Math.Sqrt(252);

            ViewBag.Date = startDate;

            return View("Details", asset);
        }

        public ActionResult RiskAnalysis(String date = "")
        {
            List<Asset> assets = db.Assets.Include(a => a.Prices).ToList();
            DateTime startDate;

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

            return View("RiskAnalysis", assets);
        }

        // POST: /Asset/Delete/5
        [HttpPost]
        public ActionResult Delete(int id)
        {
            Asset asset = db.Assets.Include(a => a.Prices).Where(a => a.ID == id).First();
            db.Assets.Remove(asset);
            db.SaveChanges();
            return AssetList();
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
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
                assetPrices = assetPrices.OrderBy(p => p.Date).ToList();
                return assetPrices;

            }
        }
    }
}