# UnityTask
UnityTask


    void Awake()
    {
        _task = Task.Run(Do());
        _task.Complete += TestTask_Complete;
        _task.Aborted += _task_Aborted;
    }
    
    private void _task_Aborted(Task obj)
    {
        Debug.Log("ABORTED");
    }
    
    private void TestTask_Complete(Task obj)
    {
        Debug.Log("TASK COMPLETED");
    }
    
    private IEnumerator Do()
    {
        yield return null;
        Debug.Log(Environment.TickCount + ":" + "MainThread:" + Thread.CurrentThread.ManagedThreadId + ":" + Thread.CurrentThread.IsBackground);
        yield return new WaitForSeconds(1000);
        Debug.Log(Environment.TickCount + ":" + "MainThread:" + Thread.CurrentThread.ManagedThreadId + ":" + Thread.CurrentThread.IsBackground);
        var msg = "";
        yield return new TaskBackgroundThread();
        msg += Environment.TickCount + ":" + "BackThread:" + Thread.CurrentThread.ManagedThreadId + ":" + Thread.CurrentThread.IsBackground + '\n';
        
        yield return new TaskWait(10000);
        msg += Environment.TickCount + ":" + "BackThread:" + Thread.CurrentThread.ManagedThreadId + ":" + Thread.CurrentThread.IsBackground;
        yield return new TaskCoroutineThread();
        Debug.Log(msg);
        Debug.Log(Environment.TickCount + ":" + "MainThread:" + Thread.CurrentThread.ManagedThreadId + ":" + Thread.CurrentThread.IsBackground);
    }
