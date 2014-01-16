using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;


public sealed class Logger
{
    static Logger instance = null;
    static readonly object padlock = new Object();
    TextWriter file = null;
    DateTime date;
    String logFile;

    Logger(String logFile, DateTime date)
    {
        this.logFile = logFile;
        this.date = date;
        this.file = TextWriter.Synchronized(File.AppendText(logFile + "gaLogFile" + date.Year + date.Month + date.Day + ".log"));
    }

    public static Logger Instance(String logFile, DateTime date)
    {
        lock (padlock) {
            if (instance == null) {
                instance = new Logger(logFile, date);
            }
            return instance;
        }
    }

    public void writeToFile(String threadName, GARequestObject requestObject)
    {
        updateLoggerFile();

        String theTimeStamp = "";
        if (requestObject.requestTime != null)
            theTimeStamp = requestObject.requestTime.ToShortDateString() + " " + requestObject.requestTime.ToLongTimeString() + "\t";

        this.file.WriteLine(threadName + "(" + requestObject.requestCount + ")\t" + theTimeStamp + requestObject.ipAddress + "\t" + requestObject.el + "\t" + requestObject.userAgent + "\t" + requestObject.returnRequestCode + "\t" + requestObject.requestStatus + "\t" + requestObject.referrer);
        this.file.Flush();
    }

    public void writeToFile(String line)
    {
        updateLoggerFile();

        this.file.WriteLine(line);
        this.file.Flush();

    }

    private void updateLoggerFile() {
        DateTime currDate = System.DateTime.Now;
        if (currDate.Day != date.Day)
        {
            lock (padlock)
            {
                this.date = System.DateTime.Now;
                this.file.Flush();
                this.file.Close();
                this.file = TextWriter.Synchronized(File.AppendText(logFile + "gaLogFile" + date.Year + date.Month + date.Day + ".log"));
            }
        }
    }
}