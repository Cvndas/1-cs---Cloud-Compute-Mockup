namespace CloudStates;

public enum ClientStates
{
    NO_CONNECTION,
    PROGRAM_CLOSED,
    CHOOSING_AUTHENTICATE_METHOD,
    REGISTERING,
    LOGGING_IN,
    LOGIN_INFO_SENT,
    REGISTRATION_INFO_SENT,
    LOGGED_IN
}

public enum ServerStates
{
    NO_CONNECTION,
    PROCESS_AUTHENTICATION_CHOICE, // Entry state when Employee is assigned to a User.
    PROCESS_REGISTRATION,
    REGISTRATION_INFO_RECEIVED,
    PROCESS_LOGIN,
    IN_DASHBOARD,
}
