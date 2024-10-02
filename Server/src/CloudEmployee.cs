internal class CloudEmployee
{
    private Thread _employeeThread;

    // --------- Concurrency --------- // 

    // Need _isWorkingLock to access.
    // Accessed by Self, and by CloudManager.
    private bool _isWorking;
    private readonly object _isWorkingLock;

    // ------------------------------- //

    private CloudStates.ServerStates _employeeState;
    private UserResources? _userResources;


    // Thread: Listener
    // Launches an _employeeThread on the EmployeeJob() method.
    public CloudEmployee()
    {
        _employeeState = CloudStates.ServerStates.NO_CONNECTION;
        _isWorking = false;
        _isWorkingLock = new object();

        _employeeThread = new Thread(EmployeeJob);
        _employeeThread.Start();
    }

    // Thread: CloudManager
    public void AssignClient(UserResources userResources)
    {
        Debug.Assert(Thread.CurrentThread.GetHashCode() == ThreadRegistry.CloudManagerThreadHash);
        Debug.Assert(userResources != null);
        _userResources = userResources;

        // Now notify the thread to start working.
        // TODO
        lock (_isWorkingLock) {
            _isWorking = true;
            Monitor.Pulse(_isWorkingLock); // Tell the thread to start working.... ... 
            // ...
        }
        // ... Now!!!
        return;
    }

    // Thread: Employee-x
    private void DisposeOfClient()
    {
        Debug.Assert(_userResources != null);
        Debug.Assert(_userResources.stream != null);
        Debug.Assert(_userResources.client != null);

        _userResources.stream.Close();
        _userResources.client.Close();
    }


    //  Thread: Employee-x
    private void transferResourcesToChatManager(ChatEmployee chatEmployee)
    {
        // TODO Implent for Chat
        return;
    }

    // Thread: Employee-x
    private void EmployeeJob()
    {
        // TODO: Implement for Login
        lock (_isWorkingLock) {
            if (_isWorking == false) {
                // Wait to be assigned work.
                Monitor.Wait(_isWorkingLock);
            }
            Debug.WriteLine("Employee hashcode " + Thread.CurrentThread.GetHashCode() + " has started working.");
            // First wait on conditional variable _hasWork, and then
            RunCloudEmployeeStateMachine();
            DisposeOfClient();
            _isWorking = false;
            Debug.WriteLine("Employee hashcode " + Thread.CurrentThread.GetHashCode() + " has stopped working.");
        }
    }

    // Thread: Employee-x
    private void RunCloudEmployeeStateMachine()
    {
        _employeeState = CloudStates.ServerStates.PROCESSING_CHOICE; // Set the state to the entry state
        // Can use volatile variable to cause Employee to exit.
        while (true) {
            switch (_employeeState) {
                case CloudStates.ServerStates.NO_CONNECTION:
                    Debug.WriteLine($"Employee hash {Thread.CurrentThread.GetHashCode()}: State - NO_CONNECTION");
                    break;

                case CloudStates.ServerStates.PROCESSING_CHOICE:
                    Debug.WriteLine($"Employee hash {Thread.CurrentThread.GetHashCode()}: State - PROCESSING_CHOICE");
                    break;

                default:
                    throw new Exception($"Employee hash {Thread.CurrentThread.GetHashCode()}: Invalid state transition");
            }
        }
    }

}