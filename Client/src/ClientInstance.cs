using System.Net;
using System.Net.Sockets;
using CloudStates;

class ClientInstance
{
    // ------------------- PUBLIC ------------------------ //
    public static ClientInstance Instance {
        get {
            if (_instance == null) {
                _instance = new ClientInstance();
            }
            else {
                throw new Exception("Attempted to make another ClientInstance. There should only be one.");
            }
            return _instance;
        }
    }

    public ClientStates ClientState {
        get {
            return _clientState;
        }
        set {
            _clientState = value;
        }
    }

    public void RunClient()
    {
        // Initialize State Machine
        WriteLine("Welcome to the Castle in the Cloud.\nConnecting you to the server...");
        _clientState = ClientStates.NO_CONNECTION;

        try {
            RunStateMachine();
        }
        catch (Exception e) {
            Error.WriteLine("Unexpected Exception: " + e.Message);
        }
        finally {
            if (_tcpClient != null) {
                _tcpClient.Close();
            }
            else {
                Error.WriteLine("_tcpClient was null.");
            }
            if (_stream != null) {
                _stream.Close();
            }
            else {
                Error.WriteLine("_stream was null.");
            }

        }
    }
    // ------------------- PRIVATE ------------------------ // 


    private static ClientInstance? _instance;
    private ClientInstance()
    {
        _serverIpEndPoint = new IPEndPoint(ServerAddress.SERVER_IP, ServerAddress.SERVER_PORT);
    }

    private IPEndPoint _serverIpEndPoint;

    private ClientStates _clientState;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;








    // ------------------- Big State Machine --------------- // 
    private void RunStateMachine()
    {
        bool isTerminated = false;
        ;
        while (!isTerminated) {
            switch (ClientState) {
                case ClientStates.NO_CONNECTION:
                    try {
                        ConnectToServer();
                    }
                    catch (Exception e) {
                        Error.WriteLine("Failed to connect to the server: " + e.Message);
                        return;
                    }
                    break;

                case ClientStates.CHOOSING_AUTHENTICATE_METHOD:
                    ChooseAuthenticateMethod();
                    break;

                case ClientStates.REGISTERING:
                    SendRegistrationInfo();
                    break;

                case ClientStates.REGISTRATION_INFO_SENT:
                    HandleRegisterResponse();
                    break;

                case ClientStates.LOGGING_IN:
                    SendLoginInfo();
                    break;

                case ClientStates.LOGIN_INFO_SENT:
                    HandleLoginResponse();
                    break;

                default:
                    throw new Exception("Invalid State Transition");
            }
        }

    }
    private void ConnectToServer()
    {
        _tcpClient = new TcpClient();
        _tcpClient.Connect(_serverIpEndPoint);
        _stream = _tcpClient.GetStream();
        ClientState = ClientStates.CHOOSING_AUTHENTICATE_METHOD;
    }

    private void SendFlag(ClientFlags clientFlag)
    {
        if (clientFlag == ClientFlags.REGISTER_REQUEST) {
            WriteLine("It was a Register request");
        }
    }

    private void SendMessage(ClientFlags clientFlag, string Message)
    {
        // TODO : implement this
        // Don't forget to convert the Message to UTF8 bytes via Encoding.UTF8.GetBytes();
        return;
    }

    private void ChooseAuthenticateMethod()
    {
        while (true) {
            Write("[Login: l | Register: r] ");
            string? choice = ReadLine() ?? throw new Exception("Failed to read [userchoice] in ChooseAuthenticateMethod()");
            WriteLine();

            if (choice == "r") {
                SendFlag(ClientFlags.REGISTER_REQUEST);
                ClientState = ClientStates.REGISTERING;
                return;
            }
            else if (choice == "l") {
                SendFlag(ClientFlags.LOGIN_REQUEST);
                ClientState = ClientStates.LOGGING_IN;
                return;
            }
            else {
                Error.WriteLine("Invalid choice.");
            }
        }
    }

    private void SendRegistrationInfo()
    {
        WriteLine("Please provide a username, followed by a password. Note: Password is not encrypted.");
        string? credentials = ReadLine() ?? throw new Exception("Failed to read [username_password] in SendRegistrationInfo()");
        SendMessage(ClientFlags.SENDING_REGISTRATION_INFO, credentials);
        ClientState = ClientStates.REGISTRATION_INFO_SENT;
        return;
    }

    private void SendLoginInfo()
    {
        WriteLine("Please provide a username, followed by a password. Note: Password is not encrypted.");
        string? credentials = ReadLine() ?? throw new Exception("Failed to read [credentials] in SendLoginInfo");
        SendMessage(ClientFlags.SENDING_LOGIN_INFO, credentials);
        ClientState = ClientStates.LOGIN_INFO_SENT;
        return;
    }

    private void HandleRegisterResponse()
    {
        byte[] buffer = new byte[1];
        if (_stream == null) {
            throw new Exception("_stream was null in HandleRegisterResponse()");
        }
        _stream.Read(buffer);
        ServerFlags serverFlag = (ServerFlags)buffer[0];

        if (serverFlag == ServerFlags.OK) {
            WriteLine("Account created! Please proceed to login.");
            ClientState = ClientStates.CHOOSING_AUTHENTICATE_METHOD;
            return;
        }
        else if (serverFlag == ServerFlags.USERNAME_TAKEN) {
            WriteLine("Username was taken. Please choose another");
            ClientState = ClientStates.REGISTERING;
            return;
        }
        else {
            throw new Exception($"invalid server response received in HandleRegisterResponse():\n{serverFlag}\n");
        }
    }

    private void HandleLoginResponse()
    {
        byte[] buffer = new byte[1];
        if (_stream == null) {
            throw new Exception("_stream was null in HandleRegisterResponse()");
        }
        _stream.Read(buffer);
        ServerFlags serverFlag = (ServerFlags)buffer[0];

        if (serverFlag == ServerFlags.OK){
            WriteLine("Logged in successfully.");
            ClientState = ClientStates.LOGGED_IN;
            return;
        }
        else if (serverFlag == ServerFlags.PASSWORD_INCORRECT){
            // TODO: Implement server-side attempt limiting.
            WriteLine("Incorrect password. You have 3 more tries."); 
            ClientState = ClientStates.LOGGING_IN;
        }
        else {
            throw new Exception($"Invalid server response received in HandleLoginResponse():\n{serverFlag}\n");
        }
    }

}




































