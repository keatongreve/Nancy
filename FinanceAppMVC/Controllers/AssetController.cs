using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using FinanceAppMVC.Models;
using System.Web.Security;

namespace FinanceAppMVC.Controllers
{
    public class AssetController : Controller
    {
        private DataContext db = new DataContext();

        public ActionResult AssetList()
        {
            if (User.Identity.IsAuthenticated)
            {
                int userID = (int) Membership.GetUser().ProviderUserKey;
                return PartialView("AssetList", db.Assets.Where(a => a.UserID == userID).ToList());
            }
            else
            {
                return PartialView("AssetList");
            }
        }

        public ActionResult Create()
        {
            return PartialView("Create");
        }

        // POST: /Asset/Create
        [HttpPost]
        public ActionResult Create(Asset asset)
        {
            if (ModelState.IsValid && User.Identity.IsAuthenticated)
            {
                asset.UserID = (int) Membership.GetUser().ProviderUserKey;
                db.Assets.Add(asset);
                db.SaveChanges();
            }
            return AssetList();
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
    }
}