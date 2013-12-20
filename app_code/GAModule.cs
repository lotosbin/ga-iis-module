using System;
using System.Web;
using System.Net;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Configuration;

public class GAModule : IHttpModule {

    private Dictionary<string, string> extensions = new Dictionary<String, String>();
    private String GA_TRACKING_ID;
    private String OECD_PROXY;

    public GAModule()
    {

        String categoriesSetting = ConfigurationManager.AppSettings["GA_CATEGORIES"];
        String[] categories = categoriesSetting.Split(',');

        foreach (String category in categories) {
            String extensionsSetting = ConfigurationManager.AppSettings["GA_" + category];
            if ( !String.IsNullOrEmpty(extensionsSetting) )
            {
                String[] exts = extensionsSetting.Split(',');
                foreach (String extension in exts)
                {
                    extensions.Add(extension, category);
                }
            }
        }

        GA_TRACKING_ID = ConfigurationManager.AppSettings["GA_TRACKING_ID"];
        OECD_PROXY = ConfigurationManager.AppSettings["OECD_PROXY"];
    }

    public String ModuleName
    {
        get { return "IIS Google Analytics"; }
    }

    // In the Init function, register for HttpApplication 
    // events by adding your handlers.
    public void Init(HttpApplication application)
    {
        application.BeginRequest +=
            (new EventHandler(this.Application_BeginRequest));
        application.EndRequest +=
            (new EventHandler(this.Application_EndRequest));
    }

    private void Application_BeginRequest(Object source, EventArgs e)
    {
        // Create HttpApplication and HttpContext objects to access
        // request and response properties.
        HttpApplication application = (HttpApplication)source;
        
        HttpContext context = application.Context;
        string filePath = context.Request.FilePath;
        string fileExtension = VirtualPathUtility.GetExtension(filePath);
    }

    private void Application_EndRequest(Object source, EventArgs e) {

        HttpApplication application = (HttpApplication)source;
        HttpContext context = application.Context;
        string filePath = context.Request.FilePath;
        string fileExtension =
            VirtualPathUtility.GetExtension(filePath);

        if (!extensions.ContainsKey(fileExtension.ToLower()))
        {
            return;
        }

        if (context.Response.StatusCode != 200)
        {
            return;
        }

        ServicePointManager.Expect100Continue = false;

        WebClient client = new WebClient();
        WebProxy wp = new WebProxy(OECD_PROXY);
        client.Proxy = wp;

        client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.63 Safari/537.36");

        NameValueCollection collection = new NameValueCollection();
        collection.Add("v", "1");
        collection.Add("tid", GA_TRACKING_ID);
        collection.Add("cid", "555");
        collection.Add("t", "event");
        collection.Add("ec", extensions[fileExtension.ToLower()]);
        collection.Add("ea", "DOWNLOAD");
        collection.Add("el", filePath);
        collection.Add("ev", "1");

        client.UploadValuesAsync(
            new Uri("http://www.google-analytics.com/collect"), 
            collection
        );
       
        context.Response.Write("COUCOU");

    }

    public void Dispose() { }
}