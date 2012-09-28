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

            asset.dailyMeanRate = queriedPrices.Sum(p => p.SimpleRateOfReturn) / (queriedPrices.Count - 1);
            asset.annualizedMeanRate = asset.dailyMeanRate * 252;

            double aggregateVariance = 0;
            asset.Prices.ForEach(p => aggregateVariance += Math.Pow(p.SimpleRateOfReturn - asset.dailyMeanRate, 2));
            asset.dailyVariance = aggregateVariance / (queriedPrices.Count - 1);
            asset.annualizedVariance = asset.dailyVariance * 252;

            asset.dailyStdDev = Math.Sqrt(asset.dailyVariance);
            asset.annualizedStdDev = asset.dailyStdDev * 252;


            ViewBag.Date = startDate.ToString("yyyy-MM-dd");
            return View("Details", asset);
        }

        // POST: /Asset/Delete/5
        [HttpPost]
        public ActionResult Delete(int id)
        {
            Asset asset = db.Assets.Include(a => a.Prices).Where(a => a.ID == id).First();
            asset.Prices.Clear();
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