using System.Net.Sockets;
using System.Text;

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
    private bool _CR_threadIsReady;
    private readonly object _threadIsReadyLock;
    // ----------------------------------- // 

    private Thread _chatEmployeeThread;
    private UserResources? _userResources;
    private NetworkStream? _stream;
    private string? _debugPreamble;

    public ChatEmployee()
    {
        // TODO implement constructor, set up thread, set up state machine, etc. More busy work
        // that I've already done before. Good practice for learning the language.
        _CR_isWorking = false;
        _isWorkingLock = new object();

        _CR_threadIsReady = false;
        _threadIsReadyLock = new object();

        _chatEmployeeThread = new(ChatEmployeeJob);
        lock (_threadIsReadyLock) {
            _chatEmployeeThread.Start();
            // Wait for the thread to be ready.
            Monitor.Wait(_threadIsReadyLock);
        }

    }

    private void ChatEmployeeJob()
    {
        _debugPreamble = $"DEBUG: ChatEmployee {Thread.CurrentThread.ManagedThreadId}: ";
        lock (_isWorkingLock) {
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
                    ChatManager.Instance.AddChatEmployeeToActiveList(this);
                    _CR_isWorking = true;

                    WriteLine(_debugPreamble + "has connected with user.");
                    byte[] buffer = new byte[1024];
                    _stream!.Read(buffer);
                    // Should print hello world.
                    Console.WriteLine("Incoming message: " + Encoding.UTF8.GetString(buffer));

                    Thread.Sleep(100000);
                }
                catch (Exception e) {
                    Error.WriteLine("Error in ChatEmployeeJob by " + _debugPreamble + e.Message);
                    Error.WriteLine("Chat employee will remain available for future jobs.");
                }

                ChatManager.Instance.RemoveChatEmployeeFromActiveList(this);
                // TODO: Send user back to the CloudManager userQueue.
                ChatManager.Instance.AddChatEmployeeToFreeQueue(this);
                _CR_isWorking = false;
            }
        }
    }


    // Thread: ChatManager
    public void ConnectWithClient(UserResources userResources)
    {
        // TODO: backport this to ChatEmployee, if this works. 
        // Should fix the race condition of the thread not being active 
        // yet by the time that the connection
        // is made. 

        lock (_isWorkingLock) {
            _userResources = userResources;
            _stream = _userResources.stream;
            // Notify that the userResources are assigned, and the thread can start working.
            Monitor.Pulse(_isWorkingLock);
        }

    }
}