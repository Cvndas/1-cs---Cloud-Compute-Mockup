

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
            CloudEmployee newEmployee = new CloudEmployee();
            AddToFreeEmployeeQueue(newEmployee);
        }
    }


    // Thread: Listener, on bootup
    // Thread, Unimplemented: ChatEmployee, when returning a user from the chat back to the dashboard.
    public void AddToFreeEmployeeQueue(CloudEmployee cloudEmployee)
    {
        lock (_freeEmployeeQueueLock) {
            Debug.WriteLine("DEBUG: " + cloudEmployee.ThreadId + " has been added to the _freeEmployeeQueue");
            _freeEmployeeQueue.Enqueue(cloudEmployee);
            Monitor.PulseAll(_freeEmployeeQueueLock);
        }
    }

    // Listener thread is the only thread that may access this
    public void AddToUserQueue(UserResources userResources)
    {
        // Assert that it is the listening thread that is trying to add to the user queue
        // TODO Chat : Remove this assert, and let chat employees fill the pending queue too
        Debug.WriteLine("DEBUG: Added a new user to _pendingUserQueue");
        Debug.Assert(Thread.CurrentThread.ManagedThreadId == ThreadRegistry.ListenerThreadId);
        lock (_pendingUserQueueLock) {
            // Error.WriteLine("Thread " + Thread.CurrentThread.ManagedThreadId + " acquired lock of _pendingUserQueue");
            _pendingUserQueue.Enqueue(userResources);
            Monitor.PulseAll(_pendingUserQueueLock);
        }
        return;
    }

    // Thread: CloudEmployee-x
    public bool UserIsRegistered(string username)
    {
        // TODO login 1
        return false;
    }

    public void AddUserToRegisteredUsers(string username, string password){
        // TODO login 2
        return;
    }

    // Thread: CloudManager == this
    public string FindLeastActiveUser()
    {
        // TODO kicking 
        return "";
    }

    // Thread: CloudManager == this
    private void RemoveMemberFromSystem()
    {
        // TODO kicking
        // Find the thread, forcefully cancel it using C# mechanism.
    }


    // ----------------- PRIVATE ------------------ // 
    private static CloudManager? _instance;

    private Queue<CloudEmployee> _freeEmployeeQueue;
    private readonly object _freeEmployeeQueueLock;

    private Queue<UserResources> _pendingUserQueue;
    private readonly object _pendingUserQueueLock;


    // Thread: Listener: Only to fill upon startup, before any listening is performed.
    // Thread: CloudManager: Searching through the array to find the Employee who has the least active user.
    private Queue<CloudEmployee> _activeEmployeesSortedByClientActivity;

    private CloudManager()
    {
        SetUpDatabaseFiles();
        _freeEmployeeQueue = new Queue<CloudEmployee>();
        _freeEmployeeQueueLock = new object();

        _pendingUserQueue = new Queue<UserResources>();
        _pendingUserQueueLock = new object();

        // TODO : Think of how to handle kicking inactive users and removing their accounts.
        // There are two types of kicking:
        // 1. A 6th user has logged in, you were inactive, you are kicked from the server. You have to re-start the client.
        // 2. A new user has logged in and made their own database, creating the 11th database. You were the last to touch
        //    the database, so your acount has been removed from registeredUsers.json, added to removedUsers.json, and 
        //    your cloudstorage/username folder has been deleted. You are only kicked if you happen to have been online at that point, but
        //    you receive the "your account has been deleted due to inactivity" message regardless the first time you submit your
        //    username again. 

        // But first comes simply writing data to the registeredUsers.json file, which is done by a CloudEmployee upon registration.
        // This employee obviously needs to lock the registeredUsers.json file.

        Thread CloudManagerThread = new Thread(CloudManagerJob);
        CloudManagerThread.Start();
        ThreadRegistry.CloudManagerThreadId = CloudManagerThread.ManagedThreadId;
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
                Debug.WriteLine("DEBUG: Assigned a user from _pendingUserQueue to a CloudEmployee");
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
        Debug.Assert(Thread.CurrentThread.ManagedThreadId == ThreadRegistry.CloudManagerThreadId);
        Debug.Assert(Monitor.IsEntered(_pendingUserQueueLock));

        CloudEmployee employee;
        lock (_freeEmployeeQueueLock) {
            employee = _freeEmployeeQueue.Dequeue();
        }
        employee.AssignClient(userResources);
        return false;
    }


    // Called before any employees are awake, so actions to the Database folder require no locking here.
    private void SetUpDatabaseFiles()
    {
        string cwd = Directory.GetCurrentDirectory();
        if (!cwd.EndsWith("/Server")) {
            Console.WriteLine("Please start the server from the Server directory.");
            throw new Exception("Start server from the Server directory");
        }

        string userRecordsDirectoryPath = Path.Combine(cwd, "database/userRecords");
        if (!Directory.Exists(userRecordsDirectoryPath)){
            Directory.CreateDirectory(userRecordsDirectoryPath);
        }

        // 3. CloudStorage parent directory
        string cloudStorageParentDirectoryPath = Path.Combine(cwd, "database/cloudstorage");
        if (!Directory.Exists(cloudStorageParentDirectoryPath)){
            Directory.CreateDirectory(cloudStorageParentDirectoryPath);
        }

        // 1. Registered users
        string registeredUsersPath = Path.Combine(cwd, "database/userRecords/registeredUsers.json");
        if (!File.Exists(registeredUsersPath)) {
            File.Create(registeredUsersPath);
        }

        // 2. Users whose entire account has been removed due to Database inactivity
        string removedUsersPath = Path.Combine(cwd, "database/userRecords/removedUsers.json");
        if (!File.Exists(removedUsersPath)) {
            File.Create(removedUsersPath);
        }

    }
}




