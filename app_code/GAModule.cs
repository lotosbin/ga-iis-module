using System;
using System.Web;
using System.Net;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Configuration;
//using System.Diagnostics;
using System.Threading;
using System.IO;

public class GAModule : IHttpModule {

    private Dictionary<string, string> extensions = new Dictionary<String, String>();
    private String GA_TRACKING_ID;
    private String OECD_PROXY;
    
    private Boolean SEND_TO_GA;
    private Boolean LOG_TO_FILE;
    private String LOG_FILE_DIR;
    private ArrayList filtredIps = new ArrayList();
    private ArrayList filtredBots = new ArrayList();
    private int THREAD_NUMBER = 1;

    private GaQueue gaRequestQueue;

    private Logger logger = null;

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
        SEND_TO_GA = true;
        LOG_TO_FILE = true;
        LOG_FILE_DIR = ConfigurationManager.AppSettings["LOG_FILE_DIR"];

        String value = ConfigurationManager.AppSettings["SEND_TO_GA"];
        if (value!=null) {
            bool flag;
            if (Boolean.TryParse("t", out flag))
                SEND_TO_GA = flag;
        }

        value = ConfigurationManager.AppSettings["LOG_TO_FILE"];
        if (value!=null) {
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
                this.filtredIps.Add(ipsPattern);
            }
        }

        value = ConfigurationManager.AppSettings["GA_THREAD_NUMBER"];
        if (value != null)
        {
            THREAD_NUMBER = Convert.ToInt32(value);
        }

        String botsSetting = ConfigurationManager.AppSettings["GA_FILTER_BOTS"];
        String[] bots = botsSetting.Split(',');
        foreach (String bot in bots)
        {
            this.filtredBots.Add(bot.Trim().ToUpper());
        }

        logger = Logger.Instance(LOG_FILE_DIR, System.DateTime.Now);
        logger.writeToFile("Finishing creating GAModule :: " + Thread.CurrentThread.ManagedThreadId);
    }

    public String ModuleName
    {
        get { return "IIS Google Analytics"; }
    }

    // In the Init function, register for HttpApplication 
    // events by adding your handlers.
    public void Init(HttpApplication application)
    {
        //Debug.WriteLine("Init ");
        //application.BeginRequest += (new EventHandler(this.Application_BeginRequest));
        application.EndRequest -=
            (new EventHandler(this.Application_EndRequest));
        application.EndRequest +=
            (new EventHandler(this.Application_EndRequest));

        application.Error +=
            (new EventHandler(this.Application_Error));

        HttpApplicationState theApp = application.Application;

        //if (theApp.Get("gaRequestQueue") != null)
        //    this.gaRequestQueue = (Queue)theApp.Get("gaRequestQueue");

        /*if (this.gaRequestQueue == null)
        {
            //this.gaRequestQueue = new Queue<GARequestObject>();
            this.gaRequestQueue = new Queue();
            theApp.Add("gaRequestQueue", this.gaRequestQueue);
        }*/
        this.gaRequestQueue = GaQueue.Instance();


        Object lGaThread = theApp.Get("gaThread");
        //logger.writeToFile("lGaThread :" + lGaThread);
        if (lGaThread == null)
        {
            logger.writeToFile("lGaThread is null");
            //THREAD_NUMBER
            for (int i = 1; i <= this.THREAD_NUMBER; i++)
            {
                String theTime = System.DateTime.Now.ToUniversalTime().Hour + "_" + System.DateTime.Now.ToUniversalTime().Minute + "_" + System.DateTime.Now.ToUniversalTime().Second;
                //GoogleAnalyticThread gaThread = new GoogleAnalyticThread(theTime + "_" + i, this.gaRequestQueue, OECD_PROXY, SEND_TO_GA, LOG_TO_FILE, LOG_FILE_DIR);
                GoogleAnalyticThread gaThread = new GoogleAnalyticThread(theTime + "_" + i, OECD_PROXY, SEND_TO_GA, LOG_TO_FILE, LOG_FILE_DIR);
                Thread oThread = new Thread(new ThreadStart(gaThread.ThreadRun));
                oThread.Start();
                //Debug.WriteLine("starting thread(" + theTime + ") ...");
            }
            theApp.Add("gaThread", this.THREAD_NUMBER);
        }
        else if (lGaThread != null)
        {
            int threadNumber = this.THREAD_NUMBER;
            threadNumber = Convert.ToInt32(lGaThread);
            if (threadNumber > this.THREAD_NUMBER) {
                int count = this.THREAD_NUMBER - threadNumber;
                logger.writeToFile("count : " + count);
                for (int i = 1; i <= count; i++)
                {
                    String theTime = System.DateTime.Now.ToUniversalTime().Hour + "_" + System.DateTime.Now.ToUniversalTime().Minute + "_" + System.DateTime.Now.ToUniversalTime().Second;
                    int lCount = i + this.THREAD_NUMBER;
                    logger.writeToFile("lCount : " + lCount);
                    //GoogleAnalyticThread gaThread = new GoogleAnalyticThread(theTime + "_" + lCount, this.gaRequestQueue, OECD_PROXY, SEND_TO_GA, LOG_TO_FILE, LOG_FILE_DIR);
                    GoogleAnalyticThread gaThread = new GoogleAnalyticThread(theTime + "_" + lCount, OECD_PROXY, SEND_TO_GA, LOG_TO_FILE, LOG_FILE_DIR);
                    Thread oThread = new Thread(new ThreadStart(gaThread.ThreadRun));
                    oThread.Start();
                    //Debug.WriteLine("starting thread(" + theTime + ") ...");
                }                
                this.THREAD_NUMBER = threadNumber;
            }
            theApp.Add("gaThread", this.THREAD_NUMBER);
        }
        logger.writeToFile("Finishing Init : " + Thread.CurrentThread.ManagedThreadId);
    }

/*    private void Application_BeginRequest(Object source, EventArgs e)
    {
        // Create HttpApplication and HttpContext objects to access
        // request and response properties.
        /*HttpApplication application = (HttpApplication)source;
        
        HttpContext context = application.Context;
        string filePath = context.Request.FilePath;
        string fileExtension = VirtualPathUtility.GetExtension(filePath);* /
    }*/


    private void Application_EndRequest(Object source, EventArgs e) {
        try
        {
            HttpApplication application = (HttpApplication)source;
            HttpContext context = application.Context;

            if (context.Response.StatusCode != 200)
            {
                return;
            }

            String range = context.Request.Headers.Get("Range");
            if (range != null && range.Trim().Length > 0)
            {
                return;
            }

            string filePath = context.Request.FilePath;
            if (filePath == null || filePath.StartsWith("/redirect/"))
            {
                return;
            }

            string fileExtension =
                VirtualPathUtility.GetExtension(filePath);
            String ipAddress = context.Request.UserHostAddress;
            String userAgent = context.Request.UserAgent;
            DateTime requestTime = context.Timestamp;

            Boolean filter = false;
            foreach (String pattern in filtredIps)
            {
                if (ipAddress.StartsWith(pattern))
                {
                    filter = true;
                    break;
                }
            }
            if (filter)
            {
                return;
            }

            if (!extensions.ContainsKey(fileExtension.ToLower()))
            {
                return;
            }

            if (userAgent != null)
            {
                String lUserAgent = userAgent.ToUpper();
                filter = false;
                foreach (String bot in filtredBots)
                {
                    if (lUserAgent.Contains(bot))
                    {
                        filter = true;
                        break;
                    }
                }
                if (filter)
                {
                    return;
                }
                //if (this.filtredBots.co lUserAgent)
                /*if (lUserAgent.Contains("GOOGLEBOT") || lUserAgent.Contains("YANDEXBOT") || lUserAgent.Contains("BINGBOT")
                    || lUserAgent.Contains("BAIDUSPIDER") || lUserAgent.Contains("TWITTERBOT") || lUserAgent.Contains("YOUDAOBOT")
                        || lUserAgent.Contains("YOLINKBOT") || lUserAgent.Contains("PAPERLIBOT") || lUserAgent.Contains("VOILABOT")
                            || lUserAgent.Contains("SHOWYOUBOT") || lUserAgent.Contains("EXABOT") || lUserAgent.Contains("MAIL.RU_BOT"))
                {
                    return;
                }*/
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

            GARequestObject gaRequestObject = new GARequestObject("1", GA_TRACKING_ID, "555", "event", extensions[fileExtension.ToLower()], "DOWNLOAD", filePath, "1", ipAddress, userAgent, requestTime, context.Response.StatusCode, range, urlReferrer, 0);


            if (gaRequestObject != null)
            {
                //Debug.WriteLine("filePath : " + filePath);   

                //Queue.Synchronized(this.gaRequestQueue);
                //Queue mySyncdQ = Queue.Synchronized(this.gaRequestQueue);

                /*lock (this.gaRequestQueue.SyncRoot)
                {
                    this.gaRequestQueue.Enqueue(gaRequestObject);
                }*/
                this.gaRequestQueue.Enqueue(gaRequestObject);
            }
        }
        catch (Exception ee) {
            logger.writeToFile(ee.Message);
        }
    }


    private void Application_Error(Object source, EventArgs e)
    {
        try
        {
            logger.writeToFile(HttpContext.Current.Server.GetLastError().Message);
        }
        catch (Exception ee) { }
        finally
        { 
            //todo:retirer l'evènement 
        }
    }

    public void Dispose() { }
}


