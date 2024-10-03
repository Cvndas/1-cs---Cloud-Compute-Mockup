using System.Text.Json;

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
            _CR_freeEmployeeQueue.Enqueue(cloudEmployee);
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
            _CR_pendingUserQueue.Enqueue(userResources);
            Monitor.PulseAll(_pendingUserQueueLock);
        }
        return;
    }

    // Thread: CloudEmployee-x
    public bool UserIsRegistered(string username)
    {
        bool ret;
        lock (_registeredUsersFileLock) {
            ret = JsonHelpers.KeyExists(_CR_registeredUsersFilePath, username);
        }
        return ret;
    }

    // Thread: CloudEmployee-x
    public void AddUserToRegisteredUsers(string username, string password){
        lock (_registeredUsersFileLock){
            JsonHelpers.AddKeyPair(_CR_registeredUsersFilePath, username, password);
        }
        Debug.WriteLine("Employee " + Thread.CurrentThread.ManagedThreadId + " has added " + username + " to registeredUsers.json");
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

    // ------ CRITICAL OBJECT && LOCK ------------- //
    private Queue<CloudEmployee> _CR_freeEmployeeQueue;
    private readonly object _freeEmployeeQueueLock;
    // -------------------------------------------- //


    // ------ CRITICAL OBJECT && LOCK ------------- //
    private Queue<UserResources> _CR_pendingUserQueue;
    private readonly object _pendingUserQueueLock;
    // -------------------------------------------- //

    

    // ------ Database File Directories ----------- // 
    
    // CurrentWorkingDirectory is verified to be correct upon CloudManager initialization.
    private static readonly string currentWorkingDirectory = Directory.GetCurrentDirectory();
    private static readonly string userRecordsDirectoryPath = Path.Combine(currentWorkingDirectory, "database/userRecords");
    private static readonly string cloudStorageParentDirectoryPath = Path.Combine(currentWorkingDirectory, "database/cloudstorage");

    // ------ CRITICAL OBJECT && LOCK ------------- //
    // Initialized by CloudManager, later accessed by any CloudEmployee
    private static readonly string _CR_registeredUsersFilePath = Path.Combine(currentWorkingDirectory, "database/userRecords/registeredUsers.json");
    private readonly object _registeredUsersFileLock;
    // -------------------------------------------- //

    private static readonly string removedUsersFilePath = Path.Combine(currentWorkingDirectory, "database/userRecords/removedUsers.json");


    // Thread: Listener: Only to fill upon startup, before any listening is performed.
    // Thread: CloudManager: Searching through the array to find the Employee who has the least active user.
    // Idea: after every 10 messages that an employee has sent, it erases itself from this queue, and apends itself to the back.
    // private Queue<CloudEmployee> _activeEmployeesSortedByClientActivity;

    private CloudManager()
    {
        SetUpDatabaseFiles();
        _CR_freeEmployeeQueue = new Queue<CloudEmployee>();
        _freeEmployeeQueueLock = new object();

        _CR_pendingUserQueue = new Queue<UserResources>();
        _pendingUserQueueLock = new object();

        _registeredUsersFileLock = new object();

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
                if (_CR_freeEmployeeQueue.Count == 0) {
                    Monitor.Wait(_freeEmployeeQueueLock);
                }
            }
            lock (_pendingUserQueueLock) {
                // No pending users? Wait for one to come in. In the meantime, employees are free to 
                // fill up the FreeEmployeeQueue.
                if (_CR_pendingUserQueue.Count == 0) {
                    Monitor.Wait(_pendingUserQueueLock);
                }
                Debug.Assert(_CR_pendingUserQueue.Count != 0);
                AssignToEmployee(_CR_pendingUserQueue.Dequeue());
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
            employee = _CR_freeEmployeeQueue.Dequeue();
        }
        employee.AssignClient(userResources);
        return false;
    }


    // Called before any employees are awake, so actions to the Database folder require no locking here.
    private void SetUpDatabaseFiles()
    {
        if (!currentWorkingDirectory.EndsWith("/Server")) {
            WriteLine("Please start the server from the Server directory.");
            throw new Exception("Start server from the Server directory");
        }

        if (!Directory.Exists(userRecordsDirectoryPath)){
            Directory.CreateDirectory(userRecordsDirectoryPath);
        }

        if (!Directory.Exists(cloudStorageParentDirectoryPath)){
            Directory.CreateDirectory(cloudStorageParentDirectoryPath);
        }

        if (!File.Exists(_CR_registeredUsersFilePath)) {
            File.Create(_CR_registeredUsersFilePath);
        }

        if (!File.Exists(removedUsersFilePath)) {
            File.Create(removedUsersFilePath);
        }
    }
}




