using System;
using System.Collections;
using System.Linq;
using System.Text;


public sealed class GaQueue
{
    static GaQueue instance = null;
    static readonly object padlock = new Object();
    private Queue gaRequestQueue;
    //ArrayList uniqueFileList = null;
    //ArrayList toBeRemoveFileList = null;

    GaQueue()
    {
        this.gaRequestQueue = new Queue();
        //this.uniqueFileList = new ArrayList();
        //this.toBeRemoveFileList = new ArrayList();        
    }

    public static GaQueue Instance()
    {
        lock (padlock)
        {
            if (instance == null)
            {
                instance = new GaQueue();
            }
            return instance;
        }
    }

    public int Count()
    {
        return this.gaRequestQueue.Count;
    }

    public void Enqueue(GARequestObject requestObject)
    {
        lock (this.gaRequestQueue.SyncRoot)
        {
            lock (padlock)
            {
                /*String key = requestObject.requestTime.ToShortDateString() + "_" + requestObject.requestTime.ToLongTimeString() + "_" + requestObject.ipAddress + "_" + requestObject.el;
                if (!this.uniqueFileList.Contains(key))
                {
                    this.gaRequestQueue.Enqueue(requestObject);
                    this.uniqueFileList.Add(key);
                }*/
                this.gaRequestQueue.Enqueue(requestObject);
            }
        }
    }

    public GARequestObject Dequeue() {
        GARequestObject requestObject = null;
        lock (this.gaRequestQueue.SyncRoot)
        {
            lock (padlock)
            {
                if (this.gaRequestQueue.Count > 0)
                {
                    requestObject = (GARequestObject)this.gaRequestQueue.Dequeue();
                    requestObject.requestCount = this.gaRequestQueue.Count;
                    /*String key = requestObject.requestTime.ToShortDateString() + "_" + requestObject.requestTime.ToLongTimeString() + "_" + requestObject.ipAddress + "_" + requestObject.el;
                    this.toBeRemoveFileList.Add(key);
                    if (toBeRemoveFileList.Count > 20)
                    {
                        foreach (String lKey in this.toBeRemoveFileList)
                        {
                            this.uniqueFileList.Remove(lKey);
                        }
                        this.toBeRemoveFileList.Clear();
                    }*/
                }
            }
        }
        return requestObject;
    }
}

