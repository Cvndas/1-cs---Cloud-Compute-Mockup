using System.Net.Sockets;
using System.Text;
using CloudStates;

internal class CloudEmployee
{
    public int ThreadId {
        get {
            if (_employeeThread != null){
                return _employeeThread.ManagedThreadId;
            }
            return -1;
        }
    }
    private Thread _employeeThread;

    // --------- Concurrency --------- // 

    // Need _isWorkingLock to access.
    // Accessed by Self, and by CloudManager.
    private bool _isWorking;
    private readonly object _isWorkingLock;

    // ------------------------------- //

    private ServerStates _employeeState;
    private UserResources? _userResources;
    private NetworkStream? _stream;
    private string? _debug_preamble;
    private int _registrationAttempts = 0;
    private int _loginAttempts = 0; // Reset to 0 in AssignClient



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
        Debug.Assert(Thread.CurrentThread.ManagedThreadId == ThreadRegistry.CloudManagerThreadId);
        Debug.Assert(userResources != null);
        _userResources = userResources;
        _stream = _userResources.stream;
        Debug.Assert(_stream != null);

        _registrationAttempts = 0;
        _loginAttempts = 0;

        _debug_preamble = $"DEBUG: Employee  {this.ThreadId} ";

        // Now notify the thread to start working.
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
        Debug.WriteLine(_debug_preamble + "TERMINATED THE CONNECTION WITH CLIENT");
        _userResources.stream.Dispose();
        _userResources.client.Dispose();
    }


    //  Thread: Employee-x == self
    private void transferResourcesToChatManager(ChatEmployee chatEmployee)
    {
        // TODO Implent for Chat
        // Without closing the user's resources, transfer them over into the ChatQueue.
        return;
    }

    // Thread: Employee == self
    // Persists across different client connections, and when waiting in the _freeEmployeeQueue.
    private void EmployeeJob()
    {
        while (true) {
            lock (_isWorkingLock) {
                if (_isWorking == false) {
                    // Wait to be assigned work.
                    Monitor.Wait(_isWorkingLock);
                }
                Debug.WriteLine(_debug_preamble + "has started working.");
                // First wait on conditional variable _hasWork, and then
                try {
                    RunCloudEmployeeStateMachine();
                    WriteLine(_debug_preamble + "and the client have disconnected, mutual agreement.");
                }
                catch (Exception e) {
                    Debug.WriteLine(_debug_preamble + "Exited the state machine. Reason: " + e.Message);
                }
                DisposeOfClient();
                _isWorking = false;
                Debug.WriteLine(_debug_preamble + "has stopped working.");
                CloudManager.Instance.AddToFreeEmployeeQueue(this);
            }
        }
    }

    // Thread: Employee-x | self
    private void RunCloudEmployeeStateMachine()
    {
        Debug.Assert(Monitor.IsEntered(_isWorkingLock));
        _employeeState = CloudStates.ServerStates.PROCESS_AUTHENTICATION_CHOICE; // Set the state to the entry state
        // Can use volatile variable to cause Employee to exit.
        while (true) {
            switch (_employeeState) {
                case ServerStates.NO_CONNECTION:
                    Debug.WriteLine(_debug_preamble + "State - NO_CONNECTION");
                    break;

                case ServerStates.PROCESS_AUTHENTICATION_CHOICE:
                    Debug.WriteLine(_debug_preamble + "State - PROCESSING_CHOICE");
                    ProcessAuthenticationChoice();
                    break;

                case ServerStates.PROCESS_REGISTRATION:
                    Debug.WriteLine(_debug_preamble + "State - PROCESS_REGISTRATION");
                    if (_registrationAttempts > AuthenticationRestrictions.MAX_REGISTRATION_ATTEMPTS) {
                        SendFlag(ServerFlags.TOO_MANY_ATTEMPTS);
                        _employeeState = ServerStates.NO_CONNECTION;
                        return;
                    }
                    ProcessRegistration();
                    break;
                default:
                    throw new Exception(_debug_preamble + "Invalid state transition");
            }
        }
    }

    // Thread: Employee-x ==  self
    private void ProcessAuthenticationChoice()
    {
        ClientFlags flagByte = ReceiveFlag();
        Debug.WriteLine("DEBUG: Received flagByte: " + flagByte);
        if (flagByte == ClientFlags.REGISTER_REQUEST) {
            this._employeeState = ServerStates.PROCESS_REGISTRATION;
            return;
        }
        else if (flagByte == ClientFlags.LOGIN_REQUEST) {
            this._employeeState = ServerStates.PROCESS_LOGIN;
            return;
        }
        else if (flagByte == ClientFlags.CLIENT_QUIT){
            this._employeeState = ServerStates.NO_CONNECTION;
            return;
        }
        else {
            throw new Exception(_debug_preamble + "Received invalid flag in ProcessAuthenticationChoice");
        }
    }

    // Thread: Employee-x == self
    private ClientFlags ReceiveFlag()
    {
        Debug.WriteLine(_debug_preamble + " Entered ReceiveFlag");
        byte[] buffer = new byte[1];
        if (_userResources?.stream == null) {
            throw new Exception("Stream as null.");
        }
        _userResources.stream.Read(buffer);
        return (ClientFlags)buffer[0];
    }

    // Thread: Employee-x == self
    private void SendFlag(ServerFlags serverFlag)
    {
        byte[] buffer = new byte[1];
        buffer[0] = (byte)serverFlag;
        if (_stream == null) {
            throw new Exception("stream was null in SendFlag()");
        }
        _stream.Write(buffer);
        return;
    }

    // Thread: Employee-x == self
    private void ProcessRegistration()
    {
        Debug.WriteLine(_debug_preamble + "Entered ProcessRegistration");
        byte[] buffer = new byte[1024];
        int totalBytesRead = 0;

        if (_userResources == null) {
            throw new Exception("_userResources was null");
        }
        do {
            // This is a blocking call
            int iterationBytesRead = _userResources.stream.Read(buffer, 0, 1024);
            totalBytesRead += iterationBytesRead;
            Debug.WriteLine($"Read {iterationBytesRead} bytes.");
        } while (_userResources.stream.DataAvailable);


        // Build the string out of the Message without the flag byte.
        string receivedString = Encoding.UTF8.GetString(buffer, 1, totalBytesRead - sizeof(ServerFlags));
        Debug.WriteLine(_debug_preamble + $"Received authentication string: {receivedString}");


        //  ------------------Handle the possible responses for this state. ----------------------- // 
        // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ //
        string[] usernameAndPassword = receivedString.Split(" ");
        // username: usernameAndPassword[0], password: usernameAndPassword[1], both may be null.
        if ((ClientFlags)buffer[0] != ClientFlags.SENDING_REGISTRATION_INFO) {
            Debug.WriteLine(_debug_preamble + $"Received flag: {(ClientFlags)buffer[0]}");
            throw new Exception("Incorrect flag received in ProcessRegistration()");
        }

        if (usernameAndPassword.Length != 2) {
            Debug.WriteLine(_debug_preamble + "Credentials were wrong: too little or too many arguments");
            SendFlag(ServerFlags.INCORRECT_CREDENTIALS_STRUCTURE);
            _registrationAttempts += 1;
            return;
        }
        // Username and password are not null.

        // TODO misc: This check shouldn't be necessary. Test extensively later.
        else if (usernameAndPassword[0] == null || usernameAndPassword[1] == null) {
            SendFlag(ServerFlags.INCORRECT_CREDENTIALS_STRUCTURE);
            _registrationAttempts += 1;
            return;
        }
        // If Username is too long
        else if (usernameAndPassword[0].Length > AuthenticationRestrictions.MAX_USERNAME_LENGTH) {
            SendFlag(ServerFlags.USERNAME_TOO_LONG);
            _registrationAttempts += 1;
            return;
        }
        else if (usernameAndPassword[1].Length > AuthenticationRestrictions.MAX_PASSWORD_LENGTH) {
            SendFlag(ServerFlags.PASSWORD_TOO_LONG);
            _registrationAttempts += 1;
            return;
        }
        
        else if (CloudManager.Instance.UserIsRegistered(usernameAndPassword[0])){
        // TODO login : handle case of username already being taken.
            _registrationAttempts += 1;
            return;
        }
        

        // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ //
        // If you didn't get filtered, all is ok! Thank you for creating an account! 
        // Let's go back to the authentication screen so you can log in.
        SendFlag(ServerFlags.OK);
        _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE;
        return;
    }

}