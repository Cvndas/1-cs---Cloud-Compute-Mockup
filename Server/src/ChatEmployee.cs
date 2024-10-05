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
    private readonly object _isWorkingLock;
    // ---------------------------------- // 

    // -------------- Concurrency -------- //
    // private bool _CR_threadIsReady;
    private readonly object? _threadIsReadyLock;
    // ----------------------------------- // 

    // ------------ Concurrency ---------- //
    // Both the main thread and the helper thread of the chat employee may send over the socket
    // at once.
    private NetworkStream? _stream;
    private readonly object _streamLock;
    // ----------------------------------- // 

    public Thread _chatEmployeeThread;
    private UserResources? _userResources;
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
    /// Set by ChatEmployee's main thread, read by ChatEmployee's helper thread.
    /// </summary>
    private volatile bool _connectionWithClientIsActive;

    // Thread: Main, created upon program startup.
    public ChatEmployee()
    {
        _isWorkingLock = new object();

        // _CR_threadIsReady = false;
        _threadIsReadyLock = new object();

        _chatClientQueueLock = new object();
        _CR_chatClientQueue = new Queue<byte[]>();

        _connectionWithClientIsActive = false;

        _streamLock = new object();

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

    /// <summary>
    /// Thread: ChatManager
    /// </summary>
    public void ConnectWithClient(UserResources userResources)
    {
        // TODO: backport this to ChatEmployee. 

        lock (_isWorkingLock) {
            _userResources = userResources;
            _stream = _userResources.stream;
            // Notify that the userResources are assigned, and the thread can start working.
            Monitor.Pulse(_isWorkingLock); // Wake up ChatEmployeeJob()
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
                Monitor.Pulse(_threadIsReadyLock); // Tell constructor that thread is ready.
            }

            while (true) { // Thread loop: Each iteration represents the lifetime of a connection.
                Monitor.Wait(_isWorkingLock); // Wait to be woken up by ConnectWithClient()
                try {
                    _connectionWithClientIsActive = true;
                    lock (_chatClientQueueLock) {
                        // Start fresh, don't show new user messages that may have been queued up from previous iterations.
                        _CR_chatClientQueue.Clear();
                    }

                    // Inform the ChatManager that this thread needs to receive chat messages
                    ChatManager.Instance.AddChatEmployeeToActiveList(this);

                    Thread sendToUserThread = new Thread(sendToUserJob);
                    sendToUserThread.Start();

                    while (_connectionWithClientIsActive) {
                        // ProcessUserChatMessage returns false if it received "quit"
                        if (ProcessUserChatMessage() == ClientFlags.TO_DASHBOARD) {
                            _connectionWithClientIsActive = false;
                            byte[] interruptHelperThread = new byte[1];
                            interruptHelperThread[0] = (byte)ServerFlags.IGNORE;
                            ChatManager.Instance.FillAllChatClientQueues(interruptHelperThread, -1);
                        }
                    }

                    // Thread should be closed by ProcessUserChatMessages() when it processes a TO_DASHBOARD flag. 
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
            }
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
    /// Thread: ChatEmployee's sendToUserThread, which is his helper thread <br/>
    /// Note: This function adds its own CHAT_MESSAGE flag, so this doesn't have to be done by the 
    /// main thread.
    /// </summary>
    private void SendChatMessageToClient(byte[] messageWithFlag)
    {
        if (_stream == null) {
            throw new Exception("_stream was null in chat employee " + _chatEmployeeThread.ManagedThreadId + "'s sendChatMessageToClient(), which runs on its own helper thread.");
        }
        lock (_streamLock) {
            _stream.Write(messageWithFlag);
        }
    }

    /// <summary>
    /// Runs on ChatEmployee's main thread. <br/>
    /// Returns true if normal message was received <br/>
    /// Returns false if received TO_DASHBOARD<br/>
    /// </summary>
    /// <returns></returns>
    private ClientFlags ProcessUserChatMessage()
    {
        // Note: This was before I realized that I had to add "\n" to mark the end of each message. This 
        // codebase is a mess of a result, and this here could break if the socket reads more than 1 message
        // at a time. Not sure how likely that is, but this is probably a security flaw or at least
        // a bug. 
        if (_stream == null) {
            throw new Exception("Chat Employee " + Environment.CurrentManagedThreadId + "'s stream was null in ProcessUserChatMessage.");
        }

        int bytesReceived = 0;
        int maxMessageSize = SharedFlags.CHAT_MESSAGE.ToString().Length + SystemRestrictions.MAX_CHAT_MESSAGE_LENGTH + 1;
        int bufferSize = maxMessageSize + 1;
        byte[] buffer = new byte[bufferSize];
        do {
            bytesReceived += _stream.Read(buffer, 0, bufferSize);
        } while (_stream.DataAvailable);

        if ((ClientFlags)buffer[0] == ClientFlags.TO_DASHBOARD) {
            // Client expects a ClientFlags.TO_DASHBOARD to be returned. Otherwise it will remain stuck in listening.            
            byte[] responseBuffer = new byte[1];
            responseBuffer[0] = (byte)ClientFlags.TO_DASHBOARD;
            lock (_streamLock) {
                _stream.Write(responseBuffer, 0, 1);
            }
            return ClientFlags.TO_DASHBOARD;
        }

        // If message is too long, just ignore it. (+1 because of the \n delimiter)
        else if (bytesReceived > maxMessageSize + 1) {
            return (ClientFlags)0;
        }

        // If client doesn't send the correct flag for some reason
        else if ((SharedFlags)buffer[0] != SharedFlags.CHAT_MESSAGE) {
            throw new Exception("Chat Employee " + Environment.CurrentManagedThreadId + "received an invalid flag.");
        }
        // Else, all correct. Go ahead and fill up all the queues.
        else {
            ChatManager.Instance.FillAllChatClientQueues(buffer, _chatEmployeeThread.ManagedThreadId);
            return (ClientFlags)0;
        }
    }

    /// <summary>
    /// Thread: ChatEmployee's sendToUserThread, which is his helper thread
    /// </summary>
    public void sendToUserJob()
    {
        while (true) {
            byte[] messageToBeSent;
            lock (_chatClientQueueLock) {
                if (_CR_chatClientQueue.Count < 1) {
                    // Wait for the queue to be filled by EnqueueChatEmployeeQueue(), in case there was nothing.
                    Monitor.Wait(_chatClientQueueLock);
                }
                // Now it's guaranteed that there's data in the queue.
                if (_connectionWithClientIsActive) {
                    messageToBeSent = _CR_chatClientQueue.Dequeue();
                }
                else {
                    return;
                }
            }
            if (messageToBeSent[0] != (byte)ServerFlags.IGNORE) {
                SendChatMessageToClient(messageToBeSent);
            }
        }
    }


}