using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.Web.Routing;

namespace FinanceAppMVC.Extensions
{
    public static class HtmlExtensions
    {
        public static IHtmlString RawActionLink(this HtmlHelper htmlHelper, string linkText, string action, string controller, object routeValues, object htmlAttributes)
        {
            var urlHelper = new UrlHelper(htmlHelper.ViewContext.RequestContext);
            var anchor = new TagBuilder("a");
            anchor.InnerHtml = linkText;
            anchor.Attributes["href"] = urlHelper.Action(action, controller, routeValues);
            anchor.MergeAttributes(new RouteValueDictionary(htmlAttributes));
            return MvcHtmlString.Create(anchor.ToString());
        }

        public static IHtmlString RawActionLink(this HtmlHelper htmlHelper, string linkText, string action, object routeValues, object htmlAttributes)
        {
            var urlHelper = new UrlHelper(htmlHelper.ViewContext.RequestContext);
            var anchor = new TagBuilder("a");
            anchor.InnerHtml = linkText;
            anchor.Attributes["href"] = urlHelper.Action(action, routeValues);
            anchor.MergeAttributes(new RouteValueDictionary(htmlAttributes));
            return MvcHtmlString.Create(anchor.ToString());
        }

        public static MvcHtmlString RawActionLink(this AjaxHelper ajaxHelper, string linkText, string actionName, string controllerName, object routeValues, AjaxOptions ajaxOptions, object htmlAttributes)
        {
            var lnk = ajaxHelper.ActionLink("[replaceme]", actionName, controllerName, routeValues, ajaxOptions, htmlAttributes);
            return MvcHtmlString.Create(lnk.ToString().Replace("[replaceme]", linkText));
        }

        public static MvcHtmlString RawActionLink(this AjaxHelper ajaxHelper, string linkText, string actionName, object routeValues, AjaxOptions ajaxOptions, object htmlAttributes)
        {
            var lnk = ajaxHelper.ActionLink("[replaceme]", actionName, routeValues, ajaxOptions, htmlAttributes);
            return MvcHtmlString.Create(lnk.ToString().Replace("[replaceme]", linkText));
        }
    }
}