using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

/// <summary>
/// Summary description for GARequestObject
/// </summary>
public class GARequestObject
{
    public String v = "1";
    public String tid = "GA_TRACKING_ID";
    public String cid = "555";
    public String t = "event";
    public String ec = "ext";
    public String ea = "DOWNLOAD";
    public String el = "filePath";
    public String ev = "1";

    public DateTime requestTime;
    public int returnRequestCode = 200;
    public String requestStatus = "";
 
    public String ipAddress = "";
    public String userAgent = "";
    public String referrer = "";
    public int requestCount = 0;

    public GARequestObject(String v, String tid, String cid, String t, String ec, String ea, String el, String ev, String ipAddress, String userAgent, DateTime requestTime, int returnRequestCode, String requestStatus, String referrer, int requestCount)
    {
        this.v = v;
        this.tid = tid;
        this.cid = cid;
        this.t = t;
        this.ec = ec;
        this.ea = ea;
        this.el = el;
        this.ev = ev;
        this.ipAddress = ipAddress;
        this.userAgent = userAgent;
        this.requestTime = requestTime;
        this.returnRequestCode = returnRequestCode;
        this.referrer = referrer;
        this.requestCount = requestCount;
        this.requestStatus = requestStatus;
    }
}