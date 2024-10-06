namespace CloudStates;

public enum ClientStates
{
    NO_CONNECTION,
    PROGRAM_CLOSED,
    CHOOSING_AUTHENTICATE_METHOD,
    REGISTERING,
    TRY_BYPASS_LOGIN,
    UNASSIGNED,
    LOGGING_IN,
    LOGIN_INFO_SENT,
    REGISTRATION_INFO_SENT,
    LOGGED_IN,
    VIEWING_LOCAL_FILES,
    VIEWING_CLOUD_STORAGE,
    AWAITING_FILE_DOWNLOAD,
    AWAITING_FILE_UPLOAD,
    IN_CHAT,
}

public enum ServerStates
{
    NO_CONNECTION,
    CHECKING_IF_BYPASS_IS_LEGAL,
    PROCESS_AUTHENTICATION_CHOICE,
    CHECKING_LOGGED_IN_STATUS, // Entry state when Employee is assigned to a User.
    PROCESS_REGISTRATION,
    REGISTRATION_INFO_RECEIVED,
    PROCESS_LOGIN,
    IN_DASHBOARD,
    WAITING_FOR_FILE_UPLOAD, // Special state, that really shouldn't be interrupted
    SENDING_FILE_TO_CLIENT, // Special state, that really shouldn't be interrupted
}
