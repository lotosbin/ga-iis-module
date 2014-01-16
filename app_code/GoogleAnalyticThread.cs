using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Collections;
using System.Collections.Specialized;
using System.Threading;
//using System.Diagnostics;

public class GoogleAnalyticThread
{

    public String name;
    //Queue threadGaRequestQueue;
    GaQueue threadGaRequestQueue;

    WebClient client;
    bool sendToGa = true;
    bool logToFile = true;
    string logFile = "C:\\Temp\\";
    //System.IO.StreamWriter file = null;
    //TextWriter file = null;

    private Logger logger = null;

    private DateTime date;

    public GoogleAnalyticThread(String name, String proxy,
        bool sendToGa, bool logToFile, string logFile)
    /*public GoogleAnalyticThread(String name, Queue theQueue, String proxy,
        bool sendToGa, bool logToFile, string logFile)*/
    {
        this.name = name;
        //this.threadGaRequestQueue = theQueue;
        this.threadGaRequestQueue = GaQueue.Instance();
        this.client = new WebClient();
        WebProxy wp = new WebProxy(proxy);
        client.Proxy = wp;
        client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.63 Safari/537.36");

        this.sendToGa = sendToGa;
        this.logToFile = logToFile;
        this.logFile = logFile;

        date = System.DateTime.Now;

        /*if (logToFile)
        {
            lock (this.threadGaRequestQueue.SyncRoot)
            {
                //this.file = new System.IO.StreamWriter(logFile + "gaLogFile" + date.Year + date.Month + date.Day + ".log", true);
                this.file = TextWriter.Synchronized(File.AppendText(logFile + "gaLogFile" + date.Year + date.Month + date.Day + ".log"));
            }
        }*/
        if (logToFile)
        {
            logger = Logger.Instance(logFile, date);
            logger.writeToFile("creating : " + name);
        }
    }

    public void ThreadRun()
    {
        //ArrayList requests = new ArrayList();
        while (true)
        {
            if (this.threadGaRequestQueue.Count() > 0)
            {
                try
                {
                    GARequestObject requestObject = this.threadGaRequestQueue.Dequeue();
                    //logger.writeToFile("ThreadRun ");
                    /*lock (this.threadGaRequestQueue.SyncRoot)
                    {
                        if (threadGaRequestQueue.Count > 0)
                        {
                            requestObject = (GARequestObject)this.threadGaRequestQueue.Dequeue();
                            requestObject.requestCount = this.threadGaRequestQueue.Count;
                        }
                    }*/
                    
                    if (requestObject != null)
                    {
                        //logger.writeToFile("ThreadRun " + requestObject);
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
                            if (requestObject.referrer != null && (requestObject.referrer.Trim().Length > 0))
                                collection.Add("dr", requestObject.referrer);
                            
                            //requestObject.userAgent = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";
                            try
                            {
                                this.client.Headers.Add(HttpRequestHeader.UserAgent, requestObject.userAgent);
                                this.client.UploadValues(
                                    new Uri("http://www.google-analytics.com/collect"),
                                    collection
                                );
                                this.client.Dispose();
                            }
                            catch (System.Net.WebException e)
                            {
                                //Debug.WriteLine(e.Message);
                                this.logger.writeToFile(e.Message);
                            }
                            collection.Clear();
                            collection = null;
                        }

                        if (logToFile)
                        {
                            //writeToFile(requestObject);
                            this.logger.writeToFile(this.name, requestObject);
                        }

                        //Debug.WriteLine("threadGaRequestQueue(" + this.name + ") sending object to GA : " + requestObject.el);
                        //requestObject = null;
                    }
                }
                catch (Exception e) { }
            }
            else
            {
                Thread.Sleep(500);
            }

            //Debug.WriteLine("threadGaRequestQueue(" + this.name + ") sleeping : " + threadGaRequestQueue.Count);
        }
    }

}