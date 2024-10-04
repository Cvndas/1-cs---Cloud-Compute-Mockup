using System.Net.Sockets;
using System.Text;
using CloudStates;

namespace Server.src;
// Basically (ok not really but) stateless. Just pass around messages from other ChatEmployees.
class ChatEmployee
{
    // ------------ Concurrency ---------- // 
    // Accessed by ChatManager when assigned to a user
    // Accessed by self when sending the user back to the CloudManager
    private bool _CR_isWorking;
    private readonly object _isWorkingLock;
    // ---------------------------------- // 

    // -------------- Concurrency -------- //
    // private bool _CR_threadIsReady;
    private readonly object? _threadIsReadyLock;
    // ----------------------------------- // 

    public Thread _chatEmployeeThread;
    private UserResources? _userResources;
    private NetworkStream? _stream;
    private string? _debugPreamble;

    // --------- Concurrency ------------- //
    /// <summary>
    /// Threads: Accessed by self and other chatClients, for chat message exchange.<br/>
    /// Contains messages that already have the CHAT_MESSAGE flag set. 
    /// </summary>
    private Queue<byte[]> _CR_chatClientQueue;
    private object _chatClientQueueLock;
    // ---------------------------------- // 

    /// <summary>
    /// Used by the current ChatEmployee's ListenToUser thread, to shut down its SendToUserThread;
    /// </summary>
    private volatile bool _connectionWithClientIsActive;

    // Thread: Main, created upon program startup.
    public ChatEmployee()
    {
        // TODO implement constructor, set up thread, set up state machine, etc. More busy work
        // that I've already done before. Good practice for learning the language.
        _CR_isWorking = false;
        _isWorkingLock = new object();

        // _CR_threadIsReady = false;
        _threadIsReadyLock = new object();

        _chatClientQueueLock = new object();
        _CR_chatClientQueue = new Queue<byte[]>();

        _connectionWithClientIsActive = false;

        _chatEmployeeThread = new(ChatEmployeeJob);
        lock (_threadIsReadyLock) {
            _chatEmployeeThread.Start();
            // Wait for the thread to be ready.
            Monitor.Wait(_threadIsReadyLock);
            _threadIsReadyLock = null; // Micro optimization that saves the slightest amount of memory.
            // Like no more than a byte, probably. Just wanted to give it a try as practice for when
            // this sort of thing actually becomes useful.
        }

    }

    // Thread: chatEmployee-x
    private void ChatEmployeeJob()
    {
        _debugPreamble = $"DEBUG: ChatEmployee {Environment.CurrentManagedThreadId}: ";
        lock (_isWorkingLock) {
            if (_threadIsReadyLock == null) {
                throw new Exception("How the fuck could this be null??");
            }
            lock (_threadIsReadyLock) {
                // Acquired the "_isWorking" lock, therefore is ready to accept tasks. 
                // Outside while true, so this is only run on startup.
                Monitor.Pulse(_threadIsReadyLock);
            }

            while (true) {
                // Wait to be assigned a task.
                Monitor.Wait(_isWorkingLock);
                Debug.WriteLine(_debugPreamble + "has received an assignment.");
                try {
                    // ++++++++++++++ Task has been assigned ++++++++++++++ // 
                    _connectionWithClientIsActive = true;
                    lock (_chatClientQueueLock) {
                        // Start fresh, don't show new user messages that may have been queued up.
                        _CR_chatClientQueue.Clear();
                    }

                    ChatManager.Instance.AddChatEmployeeToActiveList(this);
                    _CR_isWorking = true;


                    Debug.WriteLine(_debugPreamble + "has connected with user.");

                    // The current thread is the ListenToUser thread, which fills up collegue queues.
                    // Now launching the SendToUser thread, which sends a message to the user when
                    // the queue has a message put in it by collueagues.
                    Thread sendToUserThread = new Thread(sendToUserJob);
                    sendToUserThread.Start();
                    bool userHasExitedChat = false;

                    while (!userHasExitedChat) {
                        // ProcessUserChatMessage returns false if it received "quit"
                        if (!ProcessUserChatMessage()) {
                            userHasExitedChat = true;
                        }
                    }
                    _connectionWithClientIsActive = false;

                    sendToUserThread.Join();
                }
                catch (Exception e) {
                    Error.WriteLine("Error in ChatEmployeeJob by " + _debugPreamble + e.Message);
                    Error.WriteLine("Therefore, returning the client back to the CloudManager.");
                    Error.WriteLine("Chat employee will remain available for future jobs.");
                }

                ChatManager.Instance.RemoveChatEmployeeFromActiveList(this);
                ChatManager.Instance.AddChatEmployeeToFreeQueue(this);
                // Another condition that I can't possibly see ever being triggered, but oh well. This program is getting so complicated anyway.
                if (_userResources == null) {
                    throw new Exception("ChatEmployee " + Environment.CurrentManagedThreadId + "'s _userResources was null in ChatEmployeeJob");
                }
                CloudManager.Instance.AddToUserQueue(_userResources);
                _CR_isWorking = false;
            }
            // +++++++++++++++++++++++ Task has been released +++++++++++++++++++++++ // 
        }
    }

    /// <summary>
    /// Thread: ChatManager
    /// </summary>
    public void ConnectWithClient(UserResources userResources)
    {
        // TODO: backport this to ChatEmployee. 
        // Fixes a very improbable, probably impossible on all hardware that C#
        // runs on, race condition... 
        // ... where the thread is not actively waiting for tasks to be given 
        // when a task is "Monitor.Pulse()"d to the thread.

        // Requires an additional "Thread is ready" lock to be created, which
        // could theoretically be destroyed? Maybe by setting it to null?

        lock (_isWorkingLock) {
            _userResources = userResources;
            _stream = _userResources.stream;
            // Notify that the userResources are assigned, and the thread can start working.
            Monitor.Pulse(_isWorkingLock);
        }
    }

    /// <summary>
    /// Thread: Another chat employee. Pulses that there is a message to be sent to the client. <br/>
    /// If the chat employee was waiting for a message, he will be woken up. 
    /// </summary>
    public void EnqueueChatEmployeeQueue(byte[] message)
    {
        lock (_chatClientQueueLock) {
            _CR_chatClientQueue.Enqueue(message);
            Monitor.Pulse(_chatClientQueueLock);
        }
    }

    /// <summary>
    /// Thread: ChatEmployee's sendToUserThread, which is his helper thread
    /// </summary>
    public void sendToUserJob()
    {
        while (_connectionWithClientIsActive) {
            byte[] messageToBeSent;
            lock (_chatClientQueueLock) {
                if (_CR_chatClientQueue.Count < 1) {
                    // Wait for the queue to be filled by EnqueueChatEmployeeQueue(), in case there was nothing.
                    Debug.WriteLine("DEBUG: waiting for my queue to be filled before I can send back to my client");
                    Monitor.Wait(_chatClientQueueLock);
                }
                // Now it's guaranteed that there's data in the queue.
                messageToBeSent = _CR_chatClientQueue.Dequeue();
            }
            Debug.WriteLine("DEBUG: Queue filled! Going to send back to my client now.");
            // Don't need the lock until next iteration. 
            sendChatMessageToClient(messageToBeSent);
        }
    }

    /// <summary>
    /// Thread: ChatEmployee's sendToUserThread, which is his helper thread <br/>
    /// Note: This function adds its own CHAT_MESSAGE flag, so this doesn't have to be done by the 
    /// main thread.
    /// </summary>
    private void sendChatMessageToClient(byte[] messageWithFlag)
    {
        if (_stream == null) {
            throw new Exception("_stream was null in chat employee " + _chatEmployeeThread.ManagedThreadId + "'s sendChatMessageToClient(), which runs on its own helper thread.");
        }
        _stream.Write(messageWithFlag);
    }

    /// <summary>
    /// Runs on ChatEmployee's main thread. <br/>
    /// Returns true if normal message was received <br/>
    /// Returns false if user wrote quit, which sends the TO_DASHBOARD flag internally <br/>
    /// </summary>
    /// <returns></returns>
    private bool ProcessUserChatMessage()
    {
        // Note about reading and writing on the same NetworkStream without synchronization, 
        // Source: https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.networkstream?view=net-8.0

        // "Read and write operations can be performed simultaneously on 
        // an instance of the NetworkStream class without the need for 
        // synchronization. As long as there is one unique thread for the 
        // write operations and one unique thread for the read operations, 
        // there will be no cross-interference between read and write threads 
        // and no synchronization is required."

        // This function needs to verify that the user has stayed within the bounds. 
        // Doesn't have to send a response to the user, as unmodified client
        // prevents this, but shouldn't fill the queue with such a garbage message
        // if the client is modified and sending unintended data.

        if (_stream == null) {
            throw new Exception("Chat Employee " + Environment.CurrentManagedThreadId + "'s stream was null in ProcessUserChatMessage.");
        }

        int bytesReceived = 0;
        int maxMessageSize = SharedFlags.CHAT_MESSAGE.ToString().Length + SystemRestrictions.MAX_CHAT_MESSAGE_LENGTH;
        int bufferSize = maxMessageSize + 1;
        byte[] buffer = new byte[bufferSize];
        do {
            bytesReceived += _stream.Read(buffer, 0, bufferSize);
        } while (_stream.DataAvailable);

        if ((ClientFlags)buffer[0] == ClientFlags.TO_DASHBOARD) {
            return false;
        }

        // If message is too long, just ignore it.
        else if (bytesReceived > maxMessageSize) {
            return true;
        }

        // If client doesn't send the correct flag for some reason
        else if ((SharedFlags)buffer[0] != SharedFlags.CHAT_MESSAGE) {
            throw new Exception("Chat Employee " + Environment.CurrentManagedThreadId + "received an invalid flag.");
        }
        // Else, all correct. Go ahead and fill up all the queues.
        else {
            ChatManager.Instance.FillAllChatClientQueues(buffer, _chatEmployeeThread.ManagedThreadId);
            return true;
        }
    }


}