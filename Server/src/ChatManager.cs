namespace Server.src;
class ChatManager
{
    private Thread _chatManagerThread;
    // ------- CRITICAL OBJECT ------- // 
    private Queue<UserResources> _CR_unassignedUsersQueue;
    private readonly object _unassignedUsersQueueLock;
    // ------------------------------- // 

    // ------- CRITICAL OBJECT ------- // 
    private Queue<ChatEmployee> _CR_freeChatEmployeeQueue;
    private readonly object _freeChatEmployeeQueueLock;
    // ------------------------------- // 

    // Thread: Main thread, x number of times on bootup.
    // Thread: ChatEmployee-x, after handing the user back to the CloudManagerQueue.
    public void AddChatEmployeeToFreeQueue(ChatEmployee employee)
    {
        lock (_freeChatEmployeeQueueLock) {
            _CR_freeChatEmployeeQueue.Enqueue(employee);
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

        // TODO: Launch his thread.
        _chatManagerThread = new(ChatManagerJob);
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


    // Thread: ChatManager
    private void ChatManagerJob()
    {
        Console.WriteLine("ChatManagerJob: Unimplemented");
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
                ConnectChatEmployeeWithClient(chosenEmployee, _CR_unassignedUsersQueue.Dequeue());
            }
        }
    }
    private void ConnectChatEmployeeWithClient(ChatEmployee chosenEmployee, UserResources userResources)
    {
        chosenEmployee.ConnectWithClient(userResources);
    }


    // Threads: CloudEmployee-x
    public void AddToUserQueue(UserResources? user)
    {
        lock (_unassignedUsersQueueLock) {
            if (user == null) {
                throw new Exception("Tried to add an invalid user to the ChatManager's user queue.");
            }
            _CR_unassignedUsersQueue.Enqueue(user);
        }
        Debug.WriteLine("CloudEmployee " + Thread.CurrentThread.ManagedThreadId + " added his user to the chat queue.");
    }
}