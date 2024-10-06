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
    private CloudSenderReceiver? _senderReceiver;
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
        _senderReceiver = userResources.senderReceiver;
        _transferedUserToChat = false;

        _registrationAttempts = 0;
        _loginAttempts = 0;

        _debug_preamble = $"DEBUG: Employee  {ThreadId} ";
        _employeeState = ServerStates.CHECKING_IF_BYPASS_IS_LEGAL;

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
        CloudManager.Instance.RemoveUserFromLoggedInList(_userResources!);
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
        // TODO - Catch "TcpClient is no longer active exception" and re-throw it in all functions until it is caught here. 
        // Do the same thing for the ChatEmployees. 
        // Also verify that "isActive" actualy does something in the pendinguserqueue popping system. 
        // Debug.Assert(false);
        while (true) {
            lock (_isWorkingLock) {
                if (_isWorking == false) {
                    // Wait to be assigned work by CloudManager
                    Monitor.Wait(_isWorkingLock);
                }
                try {

                    Debug.Assert(_employeeState == ServerStates.CHECKING_IF_BYPASS_IS_LEGAL);
                    // First wait on conditional variable _hasWork, and then
                    RunCloudEmployeeStateMachine();
                    WriteLine(_debug_preamble + "and the client have disconnected, mutual agreement.");
                }
                catch (IOException) {

                }
                catch (Exception e) {
                    Debug.WriteLine(_debug_preamble + "Exited the state machine. Reason: " + e.Message);
                }
                finally {
                    if (!_transferedUserToChat) {
                        DisposeOfClient();
                    }
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
        // Can use volatile variable to cause Employee to exit.
        while (true) {
            switch (_employeeState) {
                case ServerStates.NO_CONNECTION:
                    return;
                case ServerStates.CHECKING_IF_BYPASS_IS_LEGAL:
                    CheckIfBypassIsLegal();
                    break;
                case ServerStates.PROCESS_AUTHENTICATION_CHOICE:
                    ProcessAuthenticationChoice();
                    break;

                case ServerStates.PROCESS_REGISTRATION:
                    if (_registrationAttempts > SystemConstants.MAX_REGISTRATION_ATTEMPTS) {
                        _senderReceiver!.SendMessage(CloudFlags.SERVER_TOO_MANY_ATTEMPTS, "");
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

    private void CheckIfBypassIsLegal()
    {
        if (_senderReceiver!.ReceiveMessages()[0].flagtype == CloudFlags.CLIENT_BYPASS_LOGIN_REQUEST) {
            if (CloudManager.Instance.UserIsLoggedIn(_userResources!)) {
                _senderReceiver.SendMessage(CloudFlags.SERVER_OK, "");
                _employeeState = ServerStates.IN_DASHBOARD;
            }
            else {
                _senderReceiver.SendMessage(CloudFlags.SERVER_REJECTED, "");
                _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE;
            }
        }
        else {
            throw new IOException("User sent the wrong flags in CheckIfBypassIsLegal.");
        }
        return;
    }

    // Thread: Employee-x ==  self
    private void ProcessAuthenticationChoice()
    {
        List<(CloudFlags flags, string body)> serverResponse = _senderReceiver!.ReceiveMessages();
        if (serverResponse.Count > 1 || serverResponse[0].body != "") {
            throw new Exception("Received incorrect responses in ProcessAuthenticationChoice.");
        }
        CloudFlags flagByte = serverResponse[0].flags;
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
    /// Thread: Employee-x == self
    /// </summary>
    private void ProcessRegistration()
    {
        Debug.Assert(_employeeState == ServerStates.PROCESS_REGISTRATION);

        // Build the string out of the Message without the flag byte.
        List<(CloudFlags flags, string body)> serverResponses = _senderReceiver!.ReceiveMessages();
        if (serverResponses.Count > 1) {
            throw new Exception("Received too many responses in ProcessRegistration.");
        }
        (CloudFlags flags, string body) response = serverResponses[0];
        Debug.WriteLine(_debug_preamble + $"Received authentication string: {response.body}");


        //  ------------------Handle the possible responses for this state. ----------------------- // 
        // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ //
        string[] usernameAndPassword = response.body.Split(" ");
        // username: usernameAndPassword[0], password: usernameAndPassword[1], both may be null.
        if (response.flags != CloudFlags.CLIENT_SENDING_REGISTRATION_INFO) {
            _senderReceiver.SendMessage(CloudFlags.SERVER_UNEXPECTED_SERVER_ERROR, "");
            Debug.WriteLine(_debug_preamble + $"Received flag: {response.flags}");
            throw new Exception("Incorrect flag received in ProcessRegistration()");
        }
        // Check if there are either 2 spaces in different places or two consecutive spaces.
        if (usernameAndPassword.Length != 2 || response.body.IndexOf(" ") != response.body.LastIndexOf(" ")) {
            Debug.WriteLine(_debug_preamble + "Credentials were wrong.");
            _senderReceiver.SendMessage(CloudFlags.SERVER_INCORRECT_CREDENTIALS_STRUCTURE, "");
            _registrationAttempts += 1;
            return;
        }
        // Username and password are not null.
        string username = usernameAndPassword[0];
        string password = usernameAndPassword[1];
        Debug.Assert(username != null && password != null);

        if (username.Length > SystemConstants.MAX_USERNAME_LENGTH) {
            _senderReceiver.SendMessage(CloudFlags.SERVER_USERNAME_TOO_LONG, "");
            _registrationAttempts += 1;
            return;
        }
        else if (password.Length > SystemConstants.MAX_PASSWORD_LENGTH) {
            _senderReceiver.SendMessage(CloudFlags.SERVER_PASSWORD_TOO_LONG, "");
            _registrationAttempts += 1;
            return;
        }

        else if (CloudManager.Instance.UserIsRegistered(username)) {
            _senderReceiver.SendMessage(CloudFlags.SERVER_USERNAME_TAKEN, "");
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
            _senderReceiver.SendMessage(CloudFlags.SERVER_UNEXPECTED_SERVER_ERROR, "");
            return;
        }
        _senderReceiver.SendMessage(CloudFlags.SERVER_OK, "");
        _userResources!.username = username;
        _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE;
        return;
    }

    private void ProcessLogin()
    {
        Debug.Assert(_employeeState == ServerStates.PROCESS_LOGIN);

        List<(CloudFlags flag, string body)> serverResponses = _senderReceiver!.ReceiveMessages();
        if (serverResponses.Count > 1) {
            throw new Exception("Received too many responses in ProcessLogin.");
        }
        CloudFlags flag = serverResponses[0].flag;
        string body = serverResponses[0].body;

        if (flag != CloudFlags.CLIENT_SENDING_LOGIN_INFO) {
            _senderReceiver.SendMessage(CloudFlags.SERVER_UNEXPECTED_SERVER_ERROR, "");
            _loginAttempts += 1;
            _employeeState = ServerStates.NO_CONNECTION;
            throw new Exception(_debug_preamble + "received incorrect flag from client in ProcessLogin()");
        }
        // Get the ammount of read bytes, minus the flag byte, starting from the byte after the flag byte.
        string usernamePassword = body;
        string[] usernamePasswordArray = usernamePassword.Split(" ");

        // Handling all possible cases.
        if (usernamePasswordArray.Length != 2 || usernamePassword.IndexOf(" ") != usernamePassword.LastIndexOf(" ")) {
            _senderReceiver.SendMessage(CloudFlags.SERVER_INCORRECT_CREDENTIALS_STRUCTURE, "");
            _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE;
            _loginAttempts += 1;
            return;
        }
        string username = usernamePasswordArray[0];
        string password = usernamePasswordArray[1];

        // If username is too long, it means it doesn't exist. 
        if (username.Length > SystemConstants.MAX_USERNAME_LENGTH || !CloudManager.Instance.UserIsRegistered(username)) {
            _senderReceiver.SendMessage(CloudFlags.SERVER_USERNAME_DOESNT_EXIST, "");
            _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE;
            _loginAttempts += 1;
            return;
        }

        if (!CloudManager.Instance.IsPasswordCorrect(username, password)) {
            _senderReceiver.SendMessage(CloudFlags.SERVER_PASSWORD_INCORRECT, "");
            _employeeState = ServerStates.PROCESS_AUTHENTICATION_CHOICE;
            _loginAttempts += 1;
            return;
        }

        _senderReceiver.SendMessage(CloudFlags.SERVER_OK, "");
        if (_userResources == null) {
            throw new Exception("_userResources was null in ProcessLogin()");
        }
        CloudManager.Instance.AddToLoggedInList(_userResources);
        _employeeState = ServerStates.IN_DASHBOARD;
        return;
    }

    // Thread: CloudEmployee-x
    private void ProcessDashboard()
    {
        var response = _senderReceiver!.ReceiveMessages();
        if (response.Count > 1) {
            throw new Exception("Received too many server responses in ProcessDashboard().");
        }
        var receivedFlag = response[0].flagtype;
        var body = response[0].body;
        // Process flag-only cases first
        if (receivedFlag == CloudFlags.CLIENT_TO_CHAT) {
            TransferClientToChatManager();
            return;
        }

        Console.WriteLine("ProcessDashboard: Only TO_CHAT is (partially) implemented.");
        string[] receivedStringComponents = body.Split(" ");

        // if (true) {

        // }

        // // TODO: check if there are two spaces, if so, make sure they're not next to one another. 
        // else if (receivedBytes > maximumRequestLength) {
        //     SendFlag(Flags.REQUEST_TOO_LONG);
        //     return;
        // }
    }
}