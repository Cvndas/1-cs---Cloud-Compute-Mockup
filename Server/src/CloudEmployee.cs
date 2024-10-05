using System.Data;
using System.Net.Sockets;
using System.Text;
using CloudStates;

namespace Server.src;

internal class CloudEmployee
{
    public int ThreadId {
        get {
            if (_employeeThread != null) {
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
    private int _registrationAttempts = 0; // Reset to 0 in AssignClient
    private int _loginAttempts = 0; // Reset to 0 in AssignClient

    private bool _transferedUserToChat;


    // Thread: Listener
    // Launches an _employeeThread on the EmployeeJob() method.
    public CloudEmployee()
    {
        _employeeState = ServerStates.NO_CONNECTION;
        _isWorking = false;
        _isWorkingLock = new object();

        _employeeThread = new Thread(EmployeeJob);
        _employeeThread.Start();
    }

    // Thread: CloudManager
    public void AssignClient(UserResources userResources)
    {
        Debug.Assert(Environment.CurrentManagedThreadId == ThreadRegistry.CloudManagerThreadId);
        Debug.Assert(userResources != null);
        _userResources = userResources;
        _stream = _userResources.stream;
        _transferedUserToChat = false;
        Debug.Assert(_stream != null);

        _registrationAttempts = 0;
        _loginAttempts = 0;

        _debug_preamble = $"DEBUG: Employee  {ThreadId} ";
        _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE;

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
        _userResources.stream.Dispose();
        _userResources.client.Dispose();
    }


    //  Thread: Employee-x == self
    private void TransferClientToChatManager()
    {
        // TODO Implent for Chat
        try {
            ChatManager.Instance.AddToUserQueue(_userResources);
        }
        catch (Exception e) {
            Error.WriteLine("Exception caught in TransferClientToChatManager: " + e.Message);
        }
        finally {
            _transferedUserToChat = true;
            _employeeState = ServerStates.NO_CONNECTION;
        }
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
                    // Wait to be assigned work by CloudManager
                    Monitor.Wait(_isWorkingLock);
                }
                Debug.Assert(_employeeState != ServerStates.NO_CONNECTION);
                // First wait on conditional variable _hasWork, and then
                try {
                    RunCloudEmployeeStateMachine();
                    WriteLine(_debug_preamble + "and the client have disconnected, mutual agreement.");
                }
                // TODO : Test that -> This exception should be triggered if at any point in the relationship 
                //between Employee and Client, the TCP connection is broken.
                catch (Exception e) {
                    Debug.WriteLine(_debug_preamble + "Exited the state machine. Reason: " + e.Message);
                }
                if (!_transferedUserToChat) {
                    DisposeOfClient();
                }
                _isWorking = false;
                CloudManager.Instance.AddToFreeQueueRemoveFromActiveList(this);

            }
        }
    }

    // Thread: Employee-x | self
    private void RunCloudEmployeeStateMachine()
    {
        Debug.Assert(Monitor.IsEntered(_isWorkingLock));
        _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE; // Set the state to the entry state
        // Can use volatile variable to cause Employee to exit.
        while (true) {
            switch (_employeeState) {
                case ServerStates.NO_CONNECTION:
                    return;

                case ServerStates.PROCESS_AUTHENTICATION_CHOICE:
                    ProcessAuthenticationChoice();
                    break;

                case ServerStates.PROCESS_REGISTRATION:
                    if (_registrationAttempts > SystemRestrictions.MAX_REGISTRATION_ATTEMPTS) {
                        SendFlag(CloudFlags.SERVER_TOO_MANY_ATTEMPTS);
                        _employeeState = ServerStates.NO_CONNECTION;
                        break;
                    }
                    ProcessRegistration();
                    break;
                case ServerStates.PROCESS_LOGIN:
                    ProcessLogin();
                    break;

                case ServerStates.IN_DASHBOARD:
                    // TODO - Manage the dashboard state machine
                    ProcessDashboard();
                    break;

                default:
                    throw new Exception(_debug_preamble + "Invalid state transition");
            }
        }
    }

    // Thread: Employee-x ==  self
    private void ProcessAuthenticationChoice()
    {
        CloudFlags flagByte = ReceiveFlag();
        if (flagByte == CloudFlags.CLIENT_REGISTER_REQUEST) {
            _employeeState = ServerStates.PROCESS_REGISTRATION;
            return;
        }
        else if (flagByte == CloudFlags.CLIENT_LOGIN_REQUEST) {
            _employeeState = ServerStates.PROCESS_LOGIN;
            return;
        }
        else if (flagByte == CloudFlags.CLIENT_QUIT) {
            _employeeState = ServerStates.NO_CONNECTION;
            return;
        }
        else {
            throw new Exception(_debug_preamble + "Received invalid flag in ProcessAuthenticationChoice");
        }
    }

    /// <summary>
    /// Thread: CloudEmployee-x == self
    /// </summary>
    private CloudFlags ReceiveFlag()
    {
        byte[] buffer = new byte[1];
        if (_userResources?.stream == null) {
            throw new Exception("Stream as null.");
        }
        _userResources.stream.Read(buffer);
        return (CloudFlags)buffer[0];
    }
    /// <summary>
    /// Thread: Employee-x == self
    /// </summary>
    private void SendFlag(CloudFlags serverFlag)
    {
        byte[] buffer = new byte[2];
        buffer[0] = (byte)serverFlag;
        buffer[1] = Encoding.UTF8.GetBytes("\n")[0];
        if (_stream == null) {
            throw new Exception("stream was null in SendFlag()");
        }
        _stream.Write(buffer);
        return;
    }

    /// <summary>
    /// Thread: Employee-x == self
    /// </summary>
    private void ProcessRegistration()
    {
        Debug.Assert(_employeeState == ServerStates.PROCESS_REGISTRATION);

        // Explained in ProcessLogin. Idea: least number of bytes needed to detect password that is too long.
        int bufferSize = SystemRestrictions.MAX_PASSWORD_LENGTH + SystemRestrictions.MAX_USERNAME_LENGTH + sizeof(CloudFlags) + " ".Length + sizeof(byte);
        Debug.Assert(bufferSize == 33);

        byte[] buffer = new byte[bufferSize];
        int totalBytesRead = 0;

        if (_userResources == null) {
            throw new Exception("_userResources was null");
        }
        do {
            // This is a blocking call
            int iterationBytesRead = _userResources.stream.Read(buffer, 0, bufferSize);
            totalBytesRead += iterationBytesRead;
        } while (_userResources.stream.DataAvailable);


        // Build the string out of the Message without the flag byte.
        string receivedString = Encoding.UTF8.GetString(buffer, 1, totalBytesRead - sizeof(CloudFlags));
        Debug.WriteLine(_debug_preamble + $"Received authentication string: {receivedString}");


        //  ------------------Handle the possible responses for this state. ----------------------- // 
        // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ //
        string[] usernameAndPassword = receivedString.Split(" ");
        // username: usernameAndPassword[0], password: usernameAndPassword[1], both may be null.
        if ((CloudFlags)buffer[0] != CloudFlags.CLIENT_SENDING_REGISTRATION_INFO) {
            SendFlag(CloudFlags.SERVER_UNEXPECTED_SERVER_ERROR);
            Debug.WriteLine(_debug_preamble + $"Received flag: {(CloudFlags)buffer[0]}");
            throw new Exception("Incorrect flag received in ProcessRegistration()");
        }
        // Check if there are either 2 spaces in different places or two consecutive spaces.
        if (usernameAndPassword.Length != 2 || receivedString.IndexOf(" ") != receivedString.LastIndexOf(" ")) {
            Debug.WriteLine(_debug_preamble + "Credentials were wrong.");
            SendFlag(CloudFlags.SERVER_INCORRECT_CREDENTIALS_STRUCTURE);
            _registrationAttempts += 1;
            return;
        }
        // Username and password are not null.
        string username = usernameAndPassword[0];
        string password = usernameAndPassword[1];
        Debug.Assert(username != null && password != null);

        if (username.Length > SystemRestrictions.MAX_USERNAME_LENGTH) {
            SendFlag(CloudFlags.SERVER_USERNAME_TOO_LONG);
            _registrationAttempts += 1;
            return;
        }
        else if (password.Length > SystemRestrictions.MAX_PASSWORD_LENGTH) {
            SendFlag(CloudFlags.SERVER_PASSWORD_TOO_LONG);
            _registrationAttempts += 1;
            return;
        }

        else if (CloudManager.Instance.UserIsRegistered(username)) {
            // TODO login : handle case of username already being taken.
            SendFlag(CloudFlags.SERVER_USERNAME_TAKEN);
            _registrationAttempts += 1;
            return;
        }
        // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ //
        // If you didn't get filtered, all is ok! Thank you for creating an account! 
        // Let's go back to the authentication screen so you can log in.
        try {
            CloudManager.Instance.AddUserToRegisteredUsers(username, password);
        }
        catch (Exception e) {
            WriteLine(_debug_preamble + "failed to write username + password to registeredUsers.json, when they should have.\n"
            + "Exception info: " + e.Message);
            _registrationAttempts += 1;
            _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE;
            SendFlag(CloudFlags.SERVER_UNEXPECTED_SERVER_ERROR);
            return;
        }
        SendFlag(CloudFlags.SERVER_OK);
        _userResources.username = username;
        _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE;
        return;
    }

    private void ProcessLogin()
    {
        Debug.Assert(_employeeState == ServerStates.PROCESS_LOGIN);
        if (_userResources == null) {
            throw new Exception(_debug_preamble + "userResources was null in ProcessLogin. this should not never happen");
        }

        int bufferSize = SystemRestrictions.MAX_PASSWORD_LENGTH + SystemRestrictions.MAX_USERNAME_LENGTH + sizeof(CloudFlags) + " ".Length + sizeof(byte) + 1;
        Debug.Assert(bufferSize == 34);
        // Assert that this buffer is large enough to check if the user sent a username+password combo that is too long. +1 should be enough, 
        byte[] buffer = new byte[bufferSize];
        int totalBytesReceived = 0;
        if (_stream == null) {
            throw new Exception(_debug_preamble + "_stream was null in ProcessLogin");
        }

        do {
            totalBytesReceived += _stream.Read(buffer, 0, bufferSize);
        } while (_stream.DataAvailable);

        CloudFlags clientFlag = (CloudFlags)buffer[0];

        if (clientFlag != CloudFlags.CLIENT_SENDING_LOGIN_INFO) {
            SendFlag(CloudFlags.SERVER_UNEXPECTED_SERVER_ERROR);
            _loginAttempts += 1;
            _employeeState = ServerStates.NO_CONNECTION;
            throw new Exception(_debug_preamble + "received incorrect flag from client in ProcessLogin()");
        }
        // Get the ammount of read bytes, minus the flag byte, starting from the byte after the flag byte.
        string usernamePassword = Encoding.UTF8.GetString(buffer, 1, totalBytesReceived - 1);
        string[] usernamePasswordArray = usernamePassword.Split(" ");

        // Handling all possible cases.
        if (usernamePasswordArray.Length != 2 || usernamePassword.IndexOf(" ") != usernamePassword.LastIndexOf(" ")) {
            SendFlag(CloudFlags.SERVER_INCORRECT_CREDENTIALS_STRUCTURE);
            _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE;
            _loginAttempts += 1;
            return;
        }
        string username = usernamePasswordArray[0];
        string password = usernamePasswordArray[1];

        if (CloudManager.Instance.UserIsLoggedIn(username)) {
            SendFlag(CloudFlags.SERVER_ALREADY_LOGGED_IN);
            _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE;
            _loginAttempts += 1;
            return;
        }

        // If username is too long, it means it doesn't exist. 
        if (username.Length > SystemRestrictions.MAX_USERNAME_LENGTH || !CloudManager.Instance.UserIsRegistered(username)) {
            SendFlag(CloudFlags.SERVER_USERNAME_DOESNT_EXIST);
            _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE;
            _loginAttempts += 1;
            return;
        }

        if (!CloudManager.Instance.IsPasswordCorrect(username, password)) {
            SendFlag(CloudFlags.SERVER_PASSWORD_INCORRECT);
            _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE;
            _loginAttempts += 1;
            return;
        }

        SendFlag(CloudFlags.SERVER_OK);
        CloudManager.Instance.AddToLoggedInList(_userResources);
        _employeeState = ServerStates.IN_DASHBOARD;
        return;
    }

    // Thread: CloudEmployee-x
    private void ProcessDashboard()
    {
        // The longest possible request is UPLOAD_REQUEST " " FileName " " FileSize
        // Added 10 bytes just to be safe.
        int maximumRequestLength = sizeof(CloudFlags) + " ".Length + SystemRestrictions.MAX_FILENAME_LENGTH + " ".Length + sizeof(int) + 10;
        int bufferSize = maximumRequestLength + 1;
        byte[] buffer = new byte[bufferSize];
        int receivedBytes = 0;

        do {
            receivedBytes += _stream!.Read(buffer, 0, bufferSize);
        } while (_stream.DataAvailable);

        CloudFlags receivedFlag = (CloudFlags)buffer[0];

        // Process flag-only cases first
        if (receivedFlag == CloudFlags.CLIENT_TO_CHAT) {
            TransferClientToChatManager();
            return;
        }

        Console.WriteLine("ProcessDashboard: Only TO_CHAT is (partially) implemented.");
        string receivedString = Encoding.UTF8.GetString(buffer);
        string[] receivedStringComponents = receivedString.Split(" ");

        // if (true) {

        // }

        // // TODO: check if there are two spaces, if so, make sure they're not next to one another. 
        // else if (receivedBytes > maximumRequestLength) {
        //     SendFlag(Flags.REQUEST_TOO_LONG);
        //     return;
        // }
    }
}