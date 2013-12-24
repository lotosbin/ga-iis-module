using System;
using System.Web;
using System.Net;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Threading;

public class GAModule : IHttpModule {

    private Dictionary<string, string> extensions = new Dictionary<String, String>();
    private String GA_TRACKING_ID;
    private String OECD_PROXY;
    
    private Boolean SEND_TO_GA;
    private Boolean LOG_TO_FILE;
    private String LOG_FILE_DIR;

    private GoogleAnalyticThread gaThread;
    private Queue gaRequestQueue;
    private Thread oThread;

    public GAModule()
    {
        Debug.WriteLine("GAModule Constructor... ");

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
    }

    public String ModuleName
    {
        get { return "IIS Google Analytics"; }
    }

    // In the Init function, register for HttpApplication 
    // events by adding your handlers.
    public void Init(HttpApplication application)
    {
        Debug.WriteLine("Init ");
        application.BeginRequest +=
            (new EventHandler(this.Application_BeginRequest));
        application.EndRequest +=
            (new EventHandler(this.Application_EndRequest));

        HttpApplicationState theApp = application.Application;

        if (theApp.Get("gaRequestQueue") != null)
            this.gaRequestQueue = (Queue)theApp.Get("gaRequestQueue");

        if (theApp.Get("gaThread") != null)
            this.gaThread = (GoogleAnalyticThread)theApp.Get("gaThread");

        if (this.gaRequestQueue == null)
        {
            //this.gaRequestQueue = new Queue<GARequestObject>();
            this.gaRequestQueue = new Queue();
            theApp.Add("gaRequestQueue", this.gaRequestQueue);
        }

        if (this.gaThread == null) {
            String theTime = System.DateTime.Now.ToUniversalTime().Hour  + "_" + System.DateTime.Now.ToUniversalTime().Minute + "_" + System.DateTime.Now.ToUniversalTime().Second;
            this.gaThread = new GoogleAnalyticThread(theTime, this.gaRequestQueue, OECD_PROXY, SEND_TO_GA, LOG_TO_FILE, LOG_FILE_DIR);
            this.oThread = new Thread(new ThreadStart(this.gaThread.ThreadRun));
            this.oThread.Start();
            theApp.Add("gaThread", this.gaThread);
            Debug.WriteLine("starting thread(" + theTime + ") ...");

        }
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
        String ipAddress = context.Request.UserHostAddress;
        String userAgent = context.Request.UserAgent;
        DateTime requestTime = context.Timestamp;
        if (!extensions.ContainsKey(fileExtension.ToLower()))
        {
            return;
        }

        if (context.Response.StatusCode != 200)
        {
            return;
        }

        ServicePointManager.Expect100Continue = false;

        GARequestObject gaRequestObject = new GARequestObject("1", GA_TRACKING_ID, "555", "event", extensions[fileExtension.ToLower()], "DOWNLOAD", filePath, "1", ipAddress, userAgent, requestTime);

        if (gaRequestObject != null) {
            Debug.WriteLine("filePath : " + filePath);   

            //Queue.Synchronized(this.gaRequestQueue);
            //Queue mySyncdQ = Queue.Synchronized(this.gaRequestQueue);

            lock (this.gaRequestQueue.SyncRoot)
            {
                this.gaRequestQueue.Enqueue(gaRequestObject);
            }
        }
    }

    public void Dispose() { }
}


public class GoogleAnalyticThread
{

    public String name;
    Queue threadGaRequestQueue;
    WebClient client;
    bool sendToGa = true;
    bool logToFile = true;
    string logFile = "C:\\Temp\\";
    System.IO.StreamWriter file = null;
    private DateTime date;

    public GoogleAnalyticThread(String name, Queue theQueue, String proxy,
        bool sendToGa, bool logToFile, string logFile)
    {
        this.name = name;
        this.threadGaRequestQueue = theQueue;
        this.client = new WebClient();
        WebProxy wp = new WebProxy(proxy);
        client.Proxy = wp;
        client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.63 Safari/537.36");

        this.sendToGa = sendToGa;
        this.logToFile = logToFile;
        this.logFile = logFile;

        date = System.DateTime.Now;

        if (logToFile) {
            this.file = new System.IO.StreamWriter(logFile + "gaLogFile" + date.Year + date.Month + date.Day + ".log");
        }

    }

    public void ThreadRun()
    {

        while (true)
        {
            if (threadGaRequestQueue.Count > 2)
            {

                int count = threadGaRequestQueue.Count;
                lock (this.threadGaRequestQueue.SyncRoot)
                {
                    int i = 1;
                    while (i <= count) {
                        GARequestObject requestObject = (GARequestObject)threadGaRequestQueue.Dequeue();
                        if (sendToGa)
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

                            this.client.UploadValues(
                                new Uri("http://www.google-analytics.com/collect"), 
                                collection
                            );
                        }

                        if (logToFile) {
                            writeToFile(requestObject);
                        }

                        Debug.WriteLine("threadGaRequestQueue(" + this.name + ") sending object to GA : " + requestObject.el);
                        i++;
                    }
                }
            }

            Debug.WriteLine("threadGaRequestQueue(" + this.name + ") sleeping : " + threadGaRequestQueue.Count);

            Thread.Sleep(5000);
        }
    }

    private void writeToFile(GARequestObject requestObject) {
        DateTime currDate = System.DateTime.Now;
        if (currDate.Day != date.Day) {
            date = System.DateTime.Now;
            this.file.Flush();
            this.file.Close();
            this.file = new System.IO.StreamWriter(logFile + "gaLogFile" + date.Year + date.Month + date.Day + ".log");
        }

        String theTimeStamp = "";
        if (requestObject.requestTime != null)
            theTimeStamp = requestObject.requestTime.ToShortDateString() + " " + requestObject.requestTime.ToLongTimeString() + "\t";

        this.file.WriteLine(theTimeStamp + requestObject.ipAddress + "\t" + requestObject.el + "\t" + requestObject.userAgent);
        this.file.Flush();
    }
}
