

internal class CloudManager
{
    public static CloudManager Instance {
        get {
            if (_instance == null) {
                _instance = new CloudManager();
            }
            return _instance;
        }
    }

    public void CreateCloudEmployeePool()
    {
        for (int i = 0; i < ServerRules.CLOUD_EMPLOYEE_COUNT; i++) {
            AddToFreeList(new CloudEmployee());
        }
    }


    public void AddToFreeList(CloudEmployee cloudEmployee)
    {
        // TODO : Implement a proper identification system, so a thread can
        // add itself in here after closing the connection with the client,
        // or passing ownership of the client's resources to the ChatManager.
        // Maybe the "this" keyword is all that is needed?
        lock (_freeEmployeeQueueLock) {
            // Error.WriteLine("Thread " + Thread.CurrentThread.GetHashCode() + " acquired lock of _pendingUserQueue");
            _freeEmployeeQueue.Enqueue(cloudEmployee);
            Monitor.PulseAll(_freeEmployeeQueueLock);
        }
    }

    // Listener thread is the only thread that may access this
    public void AddToUserQueue(UserResources userResources)
    {
        // Assert that it is the listening thread that is trying to add to the user queue
        // TODO Chat : Remove this assert, and let chat employees fill the pending queue too
        Trace.WriteLine("DEBUG: Added a new user to _pendingUserQueue");
        Debug.Assert(Thread.CurrentThread.GetHashCode() == ThreadRegistry.ListenerThreadHash);
        lock (_pendingUserQueueLock) {
            // Error.WriteLine("Thread " + Thread.CurrentThread.GetHashCode() + " acquired lock of _pendingUserQueue");
            _pendingUserQueue.Enqueue(userResources);
            Monitor.PulseAll(_pendingUserQueueLock);
        }
        return;
    }


    // ----------------- PRIVATE ------------------ // 
    private static CloudManager? _instance;

    private Queue<CloudEmployee> _freeEmployeeQueue;
    private readonly object _freeEmployeeQueueLock;

    private Queue<UserResources> _pendingUserQueue;
    private readonly object _pendingUserQueueLock;

    private CloudManager()
    {
        _freeEmployeeQueue = new Queue<CloudEmployee>();
        _freeEmployeeQueueLock = new object();

        _pendingUserQueue = new Queue<UserResources>();
        _pendingUserQueueLock = new object();

        Thread CloudManagerThread = new Thread(CloudManagerJob);
        CloudManagerThread.Start();
        ThreadRegistry.CloudManagerThreadHash = CloudManagerThread.GetHashCode();
    }



    // Cloud Manager Thread's Job: Assign users to employees
    // Thread: CloudManager
    private void CloudManagerJob()
    {
        while (true) {
            // First wait for an employee to be ready. There's no point in checking the user queue otherwise.
            lock (_freeEmployeeQueueLock) {
                // No employee? wait for one to come in first. 
                if (_freeEmployeeQueue.Count == 0) {
                    Monitor.Wait(_freeEmployeeQueueLock);
                }
            }
            lock (_pendingUserQueueLock) {
                // No pending users? Wait for one to come in. In the meantime, employees are free to 
                // fill up the FreeEmployeeQueue.
                if (_pendingUserQueue.Count == 0) {
                    Monitor.Wait(_pendingUserQueueLock);
                }
                Debug.Assert(_pendingUserQueue.Count != 0);
                AssignToEmployee(_pendingUserQueue.Dequeue());
                Trace.WriteLine("DEBUG: Assigned a user from _pendingUserQueue to a CloudEmployee");
            }
        }
    }

    // Return false if there was no available worker.
    // Return true if it was successful.
    // Thread: CloudManager
    private bool AssignToEmployee(UserResources userResources)
    {
        // Pop an employee from the freelist, assign it with the user resources, 
        // then notify it to start working
        Debug.Assert(Thread.CurrentThread.GetHashCode() == ThreadRegistry.CloudManagerThreadHash);
        Debug.Assert(Monitor.IsEntered(_pendingUserQueueLock));

        CloudEmployee employee;
        lock (_freeEmployeeQueueLock) {
            employee = _freeEmployeeQueue.Dequeue();
        }
        employee.AssignClient(userResources);
        return false;
    }

}




