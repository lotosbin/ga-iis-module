using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
//using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Threading;

/// <summary>
/// Summary description for AsuncGAModule
/// </summary>
public class AsyncGAModule  : IHttpModule
{

    private Dictionary<string, string> extensions = new Dictionary<String, String>();
    private String GA_TRACKING_ID;
    private String OECD_PROXY;

    private Boolean SEND_TO_GA;
    private Boolean LOG_TO_FILE;
    private String LOG_FILE_DIR;
    private ArrayList filtredIps = new ArrayList();

    TextWriter file = null;
    private DateTime date;

	public AsyncGAModule()
	{
        //Debug.WriteLine("AsyncGAModule Constructor... ");

        String categoriesSetting = ConfigurationManager.AppSettings["GA_CATEGORIES"];
        String[] categories = categoriesSetting.Split(',');

        foreach (String category in categories)
        {
            String extensionsSetting = ConfigurationManager.AppSettings["GA_" + category];
            if (!String.IsNullOrEmpty(extensionsSetting))
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
        SEND_TO_GA = true;
        LOG_TO_FILE = true;
        LOG_FILE_DIR = ConfigurationManager.AppSettings["LOG_FILE_DIR"];

        String value = ConfigurationManager.AppSettings["SEND_TO_GA"];
        if (value != null)
        {
            bool flag;
            if (Boolean.TryParse("t", out flag))
                SEND_TO_GA = flag;
        }

        value = ConfigurationManager.AppSettings["LOG_TO_FILE"];
        if (value != null)
        {
            bool flag;
            if (Boolean.TryParse("t", out flag))
                LOG_TO_FILE = flag;
        }

        String filtredIpsSetting = ConfigurationManager.AppSettings["GA_FILTRED_IPS"];
        if (!String.IsNullOrEmpty(filtredIpsSetting))
        {
            String[] ips = filtredIpsSetting.Split(',');
            foreach (String ipsPattern in ips)
            {
                filtredIps.Add(ipsPattern);
            }
        }

    }

    public String ModuleName
    {
        get { return "Async IIS Google Analytics"; }
    }


    // In the Init function, register for HttpApplication 
    // events by adding your handlers.
    public void Init(HttpApplication application)
    {
        //Debug.WriteLine("AsyncGAModule Init ");
        //application.BeginRequest += (new EventHandler(this.Application_BeginRequest));
        application.AddOnEndRequestAsync(new BeginEventHandler(Application_EndRequest), new EndEventHandler(OnEndAsync));

        HttpApplicationState theApp = application.Application;
        if (theApp.Get("gaLogFile") != null)
            this.file = (TextWriter)theApp.Get("gaLogFile");

        if (theApp.Get("gaLogFileDate") != null)
            this.date = (DateTime)theApp.Get("gaLogFileDate");

        if (this.file == null)
        {
            date = System.DateTime.Now;
            this.file = TextWriter.Synchronized(new System.IO.StreamWriter(LOG_FILE_DIR + "gaLogFile" + date.Year + date.Month + date.Day + ".log", true));
            theApp.Add("gaLogFile", this.file);
            theApp.Add("gaLogFileDate", this.date);
        }
    }

    IAsyncResult Application_EndRequest(Object source, EventArgs e, AsyncCallback cb, Object state)
    {

        HttpApplication application = (HttpApplication)source;
        HttpContext context = application.Context;

        if (context.Response.StatusCode != 200)
        {
            return new GaIAsyncResult(false);
        }
        
        string filePath = context.Request.FilePath;
        string fileExtension =
            VirtualPathUtility.GetExtension(filePath);
        String ipAddress = context.Request.UserHostAddress;
        String userAgent = context.Request.UserAgent;
        DateTime requestTime = context.Timestamp;

        Boolean filter = false;
        foreach (String pattern in filtredIps)
        {
            if (ipAddress.StartsWith(pattern))
                filter = true;
        }
        if (filter)
        {
            return new GaIAsyncResult(false);
        }

        if (!extensions.ContainsKey(fileExtension.ToLower()))
        {
            return new GaIAsyncResult(false);
        }

        Uri uri = context.Request.UrlReferrer;
        String urlReferrer = "";
        if (uri != null)
        {
            urlReferrer = uri.ToString();
        }

        String[] languages = context.Request.UserLanguages;
        String userLanguage = "";
        if (languages != null && languages.Length > 0)
        {
            userLanguage = languages[0];
        }


        ServicePointManager.Expect100Continue = false;

        GARequestObject requestObject = new GARequestObject("1", GA_TRACKING_ID, "555", "event", extensions[fileExtension.ToLower()], "DOWNLOAD", filePath, "1", ipAddress, userAgent, requestTime, context.Response.StatusCode, context.Response.Status, urlReferrer, 0);

        if (requestObject != null)
        {
            //Debug.WriteLine("filePath : " + filePath);

            WebClient client = new WebClient();
            WebProxy wp = new WebProxy(OECD_PROXY);
            client.Proxy = wp;
            client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.63 Safari/537.36");

            if (SEND_TO_GA)
            {
                NameValueCollection collection = new NameValueCollection();
                collection.Add("v", requestObject.v);
                collection.Add("tid", requestObject.tid);
                collection.Add("cid", requestObject.cid);
                collection.Add("t", requestObject.t);
                collection.Add("ec", requestObject.ec);
                collection.Add("ea", requestObject.ea);
                collection.Add("el", requestObject.el);
                collection.Add("ev", requestObject.ev);
                if (requestObject.referrer != null && (requestObject.referrer.Trim().Length > 0))
                    collection.Add("dr", requestObject.referrer);

                try
                {
                    client.UploadValuesAsync(
                        new Uri("http://www.google-analytics.com/collect"),
                        collection
                    );
                }
                catch (System.Net.WebException webE) {
                    //Debug.WriteLine(webE.Message);
                }
            }

            if (LOG_TO_FILE)
            {
                writeToFile(requestObject);
            }

            //Debug.WriteLine("Application_EndRequest  sending object to GA : " + requestObject.el);
        }

        return new GaIAsyncResult(true);
    }

    public void OnEndAsync(IAsyncResult result)
    {

    }

    public void Dispose() {
        //Debug.WriteLine("****** Application_End Dispose called 222 : ");
        if (this.file != null) {
            this.file.WriteLine("****** Application_End Dispose called 111 : ");
            
            /*this.file.WriteLine("****** Application_End Dispose called application : " + System.Web.HttpContext.Current.ApplicationInstance);
            HttpApplication application = System.Web.HttpContext.Current.ApplicationInstance;            
            if (application != null)
            {
                HttpApplicationState theApp = application.Application;
                this.file.WriteLine("****** Application_End Dispose called theApp : " + theApp);
                this.file.WriteLine("****** Application_End Dispose called theApp.get : " + theApp.Get("gaLogFile"));
                theApp.Remove("gaLogFile");
            }*/
            this.file.Flush();
            //this.file.Close();

        }
    }

    private void writeToFile(GARequestObject requestObject)
    {
        DateTime currDate = System.DateTime.Now;
        if (currDate.Day != date.Day)
        {
            date = System.DateTime.Now;
            this.file.Flush();
            this.file.Close();
            this.file = TextWriter.Synchronized(new System.IO.StreamWriter(LOG_FILE_DIR + "gaLogFile" + date.Year + date.Month + date.Day + ".log", true));
        }

        String theTimeStamp = "";
        if (requestObject.requestTime != null)
            theTimeStamp = requestObject.requestTime.ToShortDateString() + " " + requestObject.requestTime.ToLongTimeString() + "\t";

        this.file.WriteLine(theTimeStamp + requestObject.ipAddress + "\t" + requestObject.el + "\t" + requestObject.userAgent);
        this.file.Flush();
    }

}
