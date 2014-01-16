using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

public class GaIAsyncResult : IAsyncResult
{
    bool _result;

    public GaIAsyncResult(bool result)
    {
        _result = result;
    }

    public bool IsCompleted
    {
        get { return true; }
    }

    public WaitHandle AsyncWaitHandle
    {
        get { throw new NotImplementedException(); }
    }

    public object AsyncState
    {
        get { return _result; }
    }

    public bool CompletedSynchronously
    {
        get { return true; }
    }
}