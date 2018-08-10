using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace AspNetHostFileChangeMonitorBug {
    public class Global : HttpApplication {
        protected void Application_Start (Object sender, EventArgs e) {
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }
    }
}
