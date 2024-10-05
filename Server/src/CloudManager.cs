using System.Text;
using System.Text.Json;
using CloudStates;

namespace Server.src;
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
            AddToFreeQueue(newEmployee);
        }
    }

    /// <summary>
    /// Thread: Main, only run on bootup.
    /// </summary>
    public void AddToFreeQueue(CloudEmployee cloudEmployee)
    {
        // TODO : Shouldn't this also be called by the employee somewhere? 
        lock (_freeEmployeeQueueLock) {
            _CR_freeEmployeeQueue.Enqueue(cloudEmployee);
            // Unnecessary: At this stage, the CloudManager is not yet running.
            // Keeping this here anyway, in case this isn't true in a future iteration of the program.
            Monitor.PulseAll(_freeEmployeeQueueLock);
        }
    }
    /// <summary>
    /// Threads: CloudEmployee-x
    /// </summary>
    public void AddToLoggedInList(UserResources user)
    {
        lock (_loggedInUsersResourcesLock) {
            Debug.Assert(!_CR_loggedInUsersResources.Contains(user)); // Under no circumstances should a user be logged in twice.
            _CR_loggedInUsersResources.Add(user);
        }
    }

    /// <summary>
    /// Threads: CloudEmployee-x <br/>
    /// If you, with your current TcpClient and Stream, are logged in,
    /// you may skip the registration stage and go straight into the dahsboard.
    /// </summary>
    public bool CanUserSkipRegistration(UserResources user)
    {
        // TODO Chat : Figure out where to fit this in. Probably need to modify both server and client state machines.
        bool ret = false;
        lock (_loggedInUsersResourcesLock) {
            // Username isn't enough: check the network resources.
            if (_CR_loggedInUsersResources.Contains(user)) {
                ret = true;
                // if (ChatManager.Instance.IsUserInChat(user)) {
                // throw new AttemptToLoginTwice();
                // }
            }

        }
        return ret;
    }

    public bool UserIsLoggedIn(string username)
    {
        lock (_loggedInUsersResourcesLock) {
            foreach (var user in _CR_loggedInUsersResources) {
                if (user.username == username) {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Thread: CloudEmployee, when breaking connection with client, or when passing the client to the chat manager
    /// </summary>
    public void AddToFreeQueueRemoveFromActiveList(CloudEmployee cloudEmployee)
    {
        lock (_freeEmployeeQueueLock) {
            Debug.WriteLine("DEBUG: " + cloudEmployee.ThreadId + " has been added to the _freeEmployeeQueue");
            _CR_freeEmployeeQueue.Enqueue(cloudEmployee);
            lock (_activeEmployeeListLock) {
                if (!_CR_activeEmployeeList.Remove(cloudEmployee)) {
                    throw new Exception("Failed to remove employee from Active list");
                }
                DebugPrintActiveFreeEmployees();
            }
            // Wake up the CloudManager thread, if it was waiting for a free employee to be made available.
            Monitor.PulseAll(_freeEmployeeQueueLock);
        }
    }

#if DEBUG
    private void DebugPrintActiveFreeEmployees()
    {
        Debug.Assert(Monitor.IsEntered(_activeEmployeeListLock) && Monitor.IsEntered(_freeEmployeeQueueLock));
        Debug.Write("Cloud's Active: ");
        foreach (var activeEmployee in _CR_activeEmployeeList) {
            Debug.Write(activeEmployee.ThreadId + " ");
        }
        Debug.Write(" ||| ");
        Debug.Write("Cloud's Free: ");
        foreach (var freeEmployee in _CR_freeEmployeeQueue) {
            Debug.Write(freeEmployee.ThreadId + " ");
        }
        Debug.Write("\n");
    }
#endif

    /// <summary>
    /// Thread: Listener, main - When users connect to the server. <br/>
    /// Thread: ChatEmployee, when user exits the chat. <br/>
    /// Note: Handles the locking internally.
    /// </summary>
    /// <param name="userResources"></param>
    public void AddToUserQueue(UserResources userResources)
    {
        lock (_pendingUserQueueLock) {
            if (!(_CR_pendingUserQueue.Count > SystemConstants.MAX_USERS_IN_QUEUE)) {
                _CR_pendingUserQueue.Enqueue(userResources);
                Monitor.PulseAll(_pendingUserQueueLock);
            }
            else {
                Debug.WriteLine($"Thread {Environment.CurrentManagedThreadId} didn't add user to CloudQueue, as the queue was full.");
            }
            InformUserOfQueueStatus(userResources, _CR_pendingUserQueue.Count);
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

    public bool IsPasswordCorrect(string username, string password)
    {
        bool ret;
        lock (_registeredUsersFileLock) {
            ret = JsonHelpers.ValueMatchesKey(username, password, _CR_registeredUsersFilePath);
        }
        return ret;
        ;
    }

    // Thread: CloudEmployee-x
    public void AddUserToRegisteredUsers(string username, string password)
    {
        lock (_registeredUsersFileLock) {
            JsonHelpers.AddKeyPair(_CR_registeredUsersFilePath, username, password);
        }
        Debug.WriteLine("Employee " + Environment.CurrentManagedThreadId + " has added " + username + " to registeredUsers.json");
        return;

    }

    // Thread: CloudManager == this
    public string FindLeastActiveUser()
    {
        // TODO kicking 
        Console.WriteLine("Unimplemented");
        return "";
    }

    // Thread: CloudManager == this
    private void RemoveMemberFromSystem()
    {
        // TODO kicking
        Console.WriteLine("Unimplemented.");
        // Find the thread, forcefully cancel it using C# mechanism.
    }


    // ----------------- PRIVATE ------------------ // 
    private static CloudManager? _instance;

    // ------- CRITICAL OBJECT && LOCK ------------ // 
    // Idea: CloudEmployees add users into the list if they succeed in registering to the server.
    // If the user enters the chat, then quits the chat, they end up back in the pending user queue.
    // When they are matched with a CloudEmployee, the employee checks if they're already logged in,
    // and registration is skipped.
    private List<UserResources> _CR_loggedInUsersResources;
    private readonly object _loggedInUsersResourcesLock;
    // -------------------------------------------- //


    // ------ CRITICAL OBJECT && LOCK ------------- //
    private readonly Queue<CloudEmployee> _CR_freeEmployeeQueue;
    private readonly object _freeEmployeeQueueLock;
    // -------------------------------------------- //

    // ------ CRITICAL OBJECT && LOCK ------------- //
    private readonly List<CloudEmployee> _CR_activeEmployeeList;
    private readonly object _activeEmployeeListLock;
    // -------------------------------------------- //



    // ------ CRITICAL OBJECT && LOCK ------------- //
    private Queue<UserResources> _CR_pendingUserQueue;
    private readonly object _pendingUserQueueLock;
    // -------------------------------------------- //



    // ------ Database File Directories ----------- // 

    /// <summary>
    /// CurrentWorkingDirectory is verified to be correct upon CloudManager initialization.
    /// </summary>
    private static readonly string currentWorkingDirectory = Directory.GetCurrentDirectory();
    private static readonly string userRecordsDirectoryPath = Path.Combine(currentWorkingDirectory, "database/userRecords");
    private static readonly string cloudStorageParentDirectoryPath = Path.Combine(currentWorkingDirectory, "database/cloudstorage");

    // ------ CRITICAL OBJECT && LOCK ------------- //
    // Initialized by CloudManager, later accessed by any CloudEmployee
    private static readonly string _CR_registeredUsersFilePath = Path.Combine(currentWorkingDirectory, "database/userRecords/registeredUsers.json");
    private readonly object _registeredUsersFileLock;
    // -------------------------------------------- //

    private static readonly string removedUsersFilePath = Path.Combine(currentWorkingDirectory, "database/userRecords/removedUsers.json");

    /// <summary>
    /// Thread: Listener, before any listening is performed.
    /// </summary>
    private CloudManager()
    {
        SetUpDatabaseFiles();
        _CR_freeEmployeeQueue = new Queue<CloudEmployee>();
        _freeEmployeeQueueLock = new object();

        _CR_activeEmployeeList = new List<CloudEmployee>();
        _activeEmployeeListLock = new object();

        _CR_pendingUserQueue = new Queue<UserResources>();
        _pendingUserQueueLock = new object();

        _CR_loggedInUsersResources = new List<UserResources>(ServerRules.MAX_LOGGED_IN_USERS);
        _loggedInUsersResourcesLock = new object();

        _registeredUsersFileLock = new object();

        Thread CloudManagerThread = new(CloudManagerJob);
        CloudManagerThread.Start();
        ThreadRegistry.CloudManagerThreadId = CloudManagerThread.ManagedThreadId;
    }



    /// <summary>
    /// Cloud Manager Thread's Job: Assign users to employees<br/>
    /// Thread: CloudManager
    /// </summary>
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
                UserResources user = _CR_pendingUserQueue.Dequeue();
                if (UserIsStillActive(user)) {
                    InformUserHeIsAssigned(user);
                    NotifyUsersOfQueueStatus();
                    AssignToEmployee(user);
                }
                else {
                    Debug.WriteLine("DEBUG: The user who was first in line was not connected anymore.");
                }
            }
        }
    }

    /// <summary>
    /// Thread: CloudManager
    /// </summary>
    private bool UserIsStillActive(UserResources user)
    {
        // Protocol: if a user quits 
        return user.client.Connected;
    }

    /// <summary>
    /// Thread: CloudManager - Called when an active user is popped from the queue.
    /// </summary>
    private static void InformUserHeIsAssigned(UserResources user)
    {
        user.senderReceiver.SendMessage(CloudFlags.SERVER_OK, "");
    }

    /// <summary>
    /// Thread: CloudManager - Sent to everyone in the queue after assigning the head <br/>
    /// Thread: Main/Listener - Sent to newly added user after they've been added to the queue.<br/>
    /// </summary>
    private static void InformUserOfQueueStatus(UserResources user, int queuePosition)
    {
        user.senderReceiver.SendMessage(CloudFlags.SERVER_QUEUE_POSITION, queuePosition.ToString());
    }
    /// <summary>
    /// Thread: Cloud Manager, after dequeuing to grab the first-in-line user <br/>
    /// Thread: ChatEmployee, after returning the user into the queue <br/>
    /// Thread: Main, upon adding newly connected users into the queue. <br/>
    /// </summary>
    private void NotifyUsersOfQueueStatus()
    {
        Debug.Assert(Monitor.IsEntered(_pendingUserQueueLock));
        if (!Monitor.IsEntered(_pendingUserQueueLock)) {
            throw new Exception($"Thread {Environment.CurrentManagedThreadId} didn't own _pendingUserQueueLock");
        }
        int queuePosition = 1;

        foreach (UserResources user in _CR_pendingUserQueue) {
            InformUserOfQueueStatus(user, queuePosition);
            queuePosition += 1;
        }

    }

    /// <summary>
    /// Thread: CloudManager <br/>
    /// Note: Assumes that caller has ensured that the _freeEmployeeQueue is NOT empty.
    /// </summary>
    private void AssignToEmployee(UserResources userResources)
    {
        // Pop an employee from the freelist, assign it with the user resources, 
        // then notify it to start working
        Debug.Assert(Environment.CurrentManagedThreadId == ThreadRegistry.CloudManagerThreadId);
        Debug.Assert(Monitor.IsEntered(_pendingUserQueueLock));

        CloudEmployee employee;
        lock (_freeEmployeeQueueLock) {
            employee = _CR_freeEmployeeQueue.Dequeue();
            lock (_activeEmployeeListLock) {
                _CR_activeEmployeeList.Add(employee);
                employee.AssignClient(userResources);
            }
        }
    }


    /// <summary>
    /// Called before any employees are awake, so actions to the Database folder require no locking here.
    /// </summary>
    private static void SetUpDatabaseFiles()
    {
        if (!currentWorkingDirectory.EndsWith("/Server")) {
            WriteLine("Please start the server from the Server directory.");
            throw new Exception("Start server from the Server directory");
        }

        if (!Directory.Exists(userRecordsDirectoryPath)) {
            Directory.CreateDirectory(userRecordsDirectoryPath);
        }

        if (!Directory.Exists(cloudStorageParentDirectoryPath)) {
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


public class AttemptToLoginTwice : Exception
{

}