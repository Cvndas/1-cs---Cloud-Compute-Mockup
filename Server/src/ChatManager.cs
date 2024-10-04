namespace Server.src;
class ChatManager
{
    // TODO : Inactivity system:
    // I have decided to implement the inactivity system as follows.
    // 1. The server may hold 20 people at once.
    // 2. The server may have 5 users registered.
    // 3. The server only holds capacity for 5 cloud databases to be kept.
    // 4. When a new user is made, a new database is created.
    //    if there was no room, the least inactive user has their account wiped. 
    // 5. Activity is determined by a point system. Each 10 messages in chat, a user 
    // .  gets +1 to their count in a separate special json file.
    //    When single user reaches 5 points, all users have their count decremented by 5.
    //    This is to prevent integer overflow (ridiculous but sure. More C# practice.)
    // 6. When a new user registers, and the database is full, 
    // 7. When the user tries to access cloud storage via view, upload, or download, 
    //    the server responds with "Due to inactivity, your data has been deleted.
    // .  the connection with the client is broken, and the client's data is wiped from JSON.
    // .  the ChatManager does this by checking if the user has a valid entry in the database.
    //    this entry is created upon registration. If the user doesn't have one, it means it was
    // .  deleted.
    // 8. This means that kicking isn't a dynamic "it could happen at any time" process. It
    //    only happens when triggered. 
    // 9. While the database is deleted upon registration of a new user, the account info isn't.
    //    This means that when logging in, the server can check if you have account data but no database,
    //    in which case it responds with "due to inactivity, your account has been deleted. Please
    // .  register a new account.
    private Thread _chatManagerThread;
    // ------- CRITICAL OBJECT ------- // 
    private Queue<UserResources> _CR_unassignedUsersQueue;
    private readonly object _unassignedUsersQueueLock;
    // ------------------------------- // 

    // ------- CRITICAL OBJECT ------- // 
    private Queue<ChatEmployee> _CR_freeChatEmployeeQueue;
    private readonly object _freeChatEmployeeQueueLock;
    // ------------------------------- // 

    // ------- CRITICAL OBJECT ------- // 
    private List<ChatEmployee> _CR_activeChatEmployees;
    private readonly object _activeChatEmployeesLock;
    // ------------------------------- // 

    // Thread: Main thread, x number of times on bootup.
    // Thread: ChatEmployee-x, after handing the user back to the CloudManagerQueue.
    public void AddChatEmployeeToFreeQueue(ChatEmployee employee)
    {
        lock (_freeChatEmployeeQueueLock) {
            _CR_freeChatEmployeeQueue.Enqueue(employee);
            Monitor.PulseAll(_freeChatEmployeeQueueLock);
        }
    }

    // Threads: CloudEmployee-x
    public void AddToUserQueue(UserResources? user)
    {
        lock (_unassignedUsersQueueLock) {
            if (user == null) {
                throw new Exception("Tried to add an invalid user to the ChatManager's user queue.");
            }
            _CR_unassignedUsersQueue.Enqueue(user);
            Monitor.PulseAll(_unassignedUsersQueueLock);
        }
        Debug.WriteLine("CloudEmployee " + Environment.CurrentManagedThreadId + " added his user to the chat queue.");
    }

    public void AddChatEmployeeToActiveList(ChatEmployee employee)
    {
        lock (_activeChatEmployeesLock) {
            _CR_activeChatEmployees.Add(employee);
            // TODO: Check if there's a mechanism that requires a pulse here
        }
    }

    public void RemoveChatEmployeeFromActiveList(ChatEmployee employee)
    {
        lock (_activeChatEmployeesLock) {
            _CR_activeChatEmployees.Remove(employee);
        }
    }

    // Thread: Main 
    public void CreateChatEmployeePool()
    {
        for (int i = 0; i < ServerRules.CHAT_EMPLOYEE_COUNT; i++) {
            AddChatEmployeeToFreeQueue(new ChatEmployee());
        }
    }

    private ChatManager()
    {
        _CR_unassignedUsersQueue = new Queue<UserResources>();
        _unassignedUsersQueueLock = new object();

        _CR_freeChatEmployeeQueue = new Queue<ChatEmployee>();
        _freeChatEmployeeQueueLock = new object();

        _CR_activeChatEmployees = new List<ChatEmployee>();
        _activeChatEmployeesLock = new object();

        // TODO: Launch his thread.
        _chatManagerThread = new(ChatManagerJob);
        _chatManagerThread.Start();
    }
    public static ChatManager Instance {
        get {
            if (_instance == null) {
                _instance = new ChatManager();
            }
            return _instance;
        }
    }
    public bool IsUserInChat(UserResources user)
    {
        WriteLine("IsUserInChat - UNIMPLEMENTED - Always returns false ");
        return false;
    }

    private static ChatManager? _instance;

    /// <summary>
    /// Thread: ChatManager
    /// </summary>
    private void ChatManagerJob()
    {
        ThreadRegistry.ChatManagerThreadId = Environment.CurrentManagedThreadId;
        while (true) {
            ChatEmployee chosenEmployee;
            // Wait for an employee to be available.
            lock (_freeChatEmployeeQueueLock) {
                if (_CR_freeChatEmployeeQueue.Count < 1) {
                    Monitor.Wait(_freeChatEmployeeQueueLock);
                }
                chosenEmployee = _CR_freeChatEmployeeQueue.Dequeue();
            }
            lock (_unassignedUsersQueueLock) {
                if (_CR_unassignedUsersQueue.Count < 1) {
                    Monitor.Wait(_unassignedUsersQueueLock);
                }
                chosenEmployee.ConnectWithClient(_CR_unassignedUsersQueue.Dequeue());
            }
        }
    }
    /// <summary>
    /// Thread: ChatEmployee's "Listen to client" thread.
    /// </summary>
    public void FillAllChatClientQueues(byte[] messageIncludingFlag, int ThreadToIgnore)
    {
        lock (_activeChatEmployeesLock) {
            Debug.WriteLine("DEBUG: " + Environment.CurrentManagedThreadId + " has started filling queues");
            foreach (ChatEmployee employee in _CR_activeChatEmployees) {
                if (employee._chatEmployeeThread.ManagedThreadId != ThreadToIgnore) {
                    employee.EnqueueChatEmployeeQueue(messageIncludingFlag);
                    Debug.WriteLine("DEBUG: " + Environment.CurrentManagedThreadId + " has ADDED to a queue");
                }
                else {
                    Debug.WriteLine("DEBUG: " + Environment.CurrentManagedThreadId + " has IGNORED a queue");
                }
            }
            Debug.WriteLine("DEBUG: " + Environment.CurrentManagedThreadId + " has FINISHED filling queues");
        }
        return;
    }

}