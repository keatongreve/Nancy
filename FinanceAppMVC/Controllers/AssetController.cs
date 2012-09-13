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
                db.Assets.Add(asset);
                db.SaveChanges();
            }
            return AssetList();
        }

        // GET: /Asset/Details/5?date=...
        public ActionResult Details(int id, String date = "")
        {
            Asset asset = db.Assets.Find(id);
            if (date == "")
            {
                asset.Prices = new List<AssetPrice>();
            }
            else
            {
                DateTime startDate = DateTime.Parse(date);
                asset.Prices = getQuotes(asset.Symbol, startDate, DateTime.Today);
            }
            
            return View("Details", asset);
        }

        // POST: /Asset/Delete/5
        [HttpPost]
        public ActionResult Delete(int id)
        {
            Asset asset = db.Assets.Find(id);
            db.Assets.Remove(asset);
            db.SaveChanges();
            return AssetList();
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }

        private List<AssetPrice> getQuotes(String ticker, DateTime startDate, DateTime endDate)
        {
            String url = "http://ichart.yahoo.com/table.csv?s=" + Server.UrlEncode(ticker) + "&a=" + (startDate.Month - 1)
                + "&b=" + startDate.Day + "&c=" + startDate.Year + "&d=" + (endDate.Month - 1)
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
                    double openPrice = Double.Parse(quotes[i + 1]);
                    double closePrice = Double.Parse(quotes[i + 6]);
                    assetPrices.Add(new AssetPrice
                    {
                        Date = date,
                        OpenPrice = openPrice,
                        ClosePrice = closePrice
                    });
                }

                return assetPrices;

            }
        }
    }

    public class Quote
    {
        private DateTime date;
        private double openPrice;
        private double closePrice;

        public Quote(String dateString, String openPriceString, String closePriceString)
        {
            setDate(dateString);
            setOpenPrice(openPriceString);
            setClosePrice(closePriceString);
        }

        public void setDate(String dateString)
        {
            char[] delims = {'-'};
            String[] dateInfo = dateString.Split(delims);
            int year = Int32.Parse(dateInfo[0]);
            int month = Int32.Parse(dateInfo[1]);
            int day = Int32.Parse(dateInfo[2]);
            date = new DateTime(year, month, day);
        }

        public DateTime getDate()
        {
            return date;
        }

        public void setOpenPrice(String price)
        {
            openPrice = Double.Parse(price);
        }

        public Double getOpenPrice()
        {
            return openPrice;
        }

        public void setClosePrice(String price)
        {
            closePrice = Double.Parse(price);
        }

        public Double getClosePrice()
        {
            return closePrice;
        }
    }
}