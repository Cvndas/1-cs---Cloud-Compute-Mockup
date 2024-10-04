namespace CloudStates;


/* 
    Well, it's fun to write out bytes bit by bit, but I'm pretty sure enums support this out of the box lol
    // TODO: Test later. 
*/
[Flags]
public enum ClientFlags : byte
{
    CLIENT_QUIT,
    REGISTER_REQUEST,
    LOGIN_REQUEST,
    SENDING_LOGIN_INFO,
    SENDING_REGISTRATION_INFO,
    VIEW_CLOUD_STORAGE,
    TO_DASHBOARD, // Later, when returning from chat.
    UPLOAD_REQUEST,
    DOWNLOAD_REQUEST,
    TO_CHAT,
}

public enum ServerFlags : byte
{
    OK,
    USERNAME_TAKEN,
    USERNAME_DOESNT_EXIST,
    PASSWORD_INCORRECT,
    INCORRECT_CREDENTIALS_STRUCTURE,
    QUIT_CONNECTION,
    PASSWORD_TOO_LONG,
    USERNAME_TOO_LONG,
    TOO_MANY_ATTEMPTS,
    CLOUD_STORAGE_FILE_LIST,
    KICKED_DUE_TO_INACTIVITY,
    UNEXPECTED_SERVER_ERROR,
    FILE_TOO_BIG,
    FILENAME_TOO_LONG,
    DISALLOWED_FILE_TYPE,
    DOWNLOAD_ERROR,
    NO_TOKENS,
    UPLOAD_ACCEPTED,
    UPLOAD_FAILED,
    UPOAD_SUCCESSFUL,
    FILE_DOESNT_EXIST_ON_SERVER,
    DOWNLOAD_COMPLETE,
    DOWNLOAD_COMING,
    REQUEST_TOO_LONG,
    QUEUE_POSITION,
    ALREADY_LOGGED_IN,
}

public enum SharedFlags : byte
{
    CHAT_MESSAGE,
}
