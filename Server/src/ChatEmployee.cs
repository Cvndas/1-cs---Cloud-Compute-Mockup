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
    private CloudSenderReceiver? _senderReceiver;
    private readonly object _senderReceiverLock;
    // ----------------------------------- // 

    public Thread _chatEmployeeThread;
    private UserResources? _userResources;
    private string? _debugPreamble;

    // --------- Concurrency ------------- //
    /// <summary>
    /// Threads: Accessed by self and other chatClients, for chat message exchange.<br/>
    /// Contains messages that already have the CHAT_MESSAGE flag set. 
    /// </summary>
    private Queue<string> _CR_chatClientQueue;
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
        _CR_chatClientQueue = new Queue<string>();

        _connectionWithClientIsActive = false;

        _senderReceiverLock = new object();

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
            _senderReceiver = _userResources.senderReceiver;
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
                Thread sendToUserThread = new Thread(sendToUserJob);
                try {
                    _connectionWithClientIsActive = true;
                    lock (_chatClientQueueLock) {
                        // Start fresh, don't show new user messages that may have been queued up from previous iterations.
                        _CR_chatClientQueue.Clear();
                    }

                    // Inform the ChatManager that this thread needs to receive chat messages
                    ChatManager.Instance.AddChatEmployeeToActiveList(this);
                    sendToUserThread.Start();

                    while (_connectionWithClientIsActive) {
                        // ProcessUserChatMessage returns false if it received "quit"
                        if (ProcessUserChatMessage() == CloudFlags.CLIENT_TO_DASHBOARD) {
                            sendToUserThread.Interrupt();
                            break;
                        }
                    }

                    // TODO : like I said, interrupt the helper so it doesn't get stuck here.
                }
                // If the user closed the connection
                catch (IOException e) {
                    Debug.WriteLine(e.Message);
                    if (sendToUserThread.IsAlive) {
                        sendToUserThread.Interrupt();
                    }
                }
                catch (Exception e) {
                    Error.WriteLine("Error in ChatEmployeeJob by " + _debugPreamble + e.Message);
                    Error.WriteLine("Therefore, returning the client back to the CloudManager.");
                    Error.WriteLine("Chat employee will remain available for future jobs.");
                }

                Debug.WriteLine("Trying to join the sendToUserThread");
                sendToUserThread.Join();
                Debug.WriteLine("Success! Joined the sendToUserThread");

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
    public void EnqueueChatEmployeeQueue(string formattedChatMessage)
    {
        lock (_chatClientQueueLock) {
            _CR_chatClientQueue.Enqueue(formattedChatMessage);
            Monitor.Pulse(_chatClientQueueLock);
        }
    }

    /// <summary>
    /// Runs on ChatEmployee's main thread. <br/>
    /// Returns true if normal message was received <br/>
    /// Returns false if received TO_DASHBOARD<br/>
    /// </summary>
    /// <returns></returns>
    private CloudFlags ProcessUserChatMessage()
    {

        if (_senderReceiver == null) {
            throw new Exception("Chat Employee " + Environment.CurrentManagedThreadId + "'s stream was null in ProcessUserChatMessage.");
        }

        List<(CloudFlags flagFromClient, string chatMessage)> chatMessages;
        lock (_senderReceiverLock) {
            chatMessages = _senderReceiver.ReceiveMessages();
        }
        if (chatMessages.Count > 1) {
            throw new Exception("Received more than 1 chat message from the client.");
        }

        (CloudFlags flag, string formattedChatMessageBody) = chatMessages[0];

        if (flag == CloudFlags.CLIENT_TO_DASHBOARD) {
            // TODO NEXT: Interrupt helper thread, etc. 
            Debug.Assert(false);
            return CloudFlags.CLIENT_TO_DASHBOARD;
        }

        else if (formattedChatMessageBody.Length > SystemConstants.MAX_FORMATTED_CHAT_MESSAGE_BODY_LEN) {
            WriteLine("Received a message that was too long: " + formattedChatMessageBody.Length + " characters.");
            return (CloudFlags)0;
        }

        // If client doesn't send the correct flag for some reason
        else if (flag != CloudFlags.CLIENT_CHAT_MESSAGE) {
            throw new Exception("Chat Employee " + Environment.CurrentManagedThreadId + "received an invalid flag.");
        }
        // Else, all correct. Go ahead and fill up all the queues.
        else {
            ChatManager.Instance.FillAllChatClientQueues(formattedChatMessageBody, _chatEmployeeThread.ManagedThreadId);
            return (CloudFlags)0;
        }
    }



    /// <summary>
    /// Thread: ChatEmployee's sendToUserThread, which is his helper thread
    /// </summary>
    public void sendToUserJob()
    {
        try {
            while (true) {
                string chatMessageToBeSent;
                lock (_chatClientQueueLock) {
                    if (_CR_chatClientQueue.Count < 1) {
                        // Wait for the queue to be filled by EnqueueChatEmployeeQueue(), in case there was nothing.
                        Monitor.Wait(_chatClientQueueLock);
                    }
                    // Now it's guaranteed that there's data in the queue.
                    chatMessageToBeSent = _CR_chatClientQueue.Dequeue();
                }
                lock (_senderReceiverLock) {
                    _senderReceiver!.SendMessage(CloudFlags.SERVER_CHAT_MESSAGE, chatMessageToBeSent);
                }
            }
        }
        catch (ThreadInterruptedException) {
            Debug.WriteLine("Client is no longer in chat. Closing the sendToUserJob.");
            return;
        }
    }


}