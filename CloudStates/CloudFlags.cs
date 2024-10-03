namespace CloudStates;


/* 
    Well, it's fun to write out bytes bit by bit, but I'm pretty sure enums support this out of the box lol
    // TODO: Test later. 
*/
[Flags]
public enum ClientFlags : byte
{
    CLIENT_QUIT = 0b00_00_00_00,
    REGISTER_REQUEST = 0b00_00_00_01,
    LOGIN_REQUEST = 0b00_00_00_10,
    SENDING_LOGIN_INFO = 0b00_00_00_11,
    SENDING_REGISTRATION_INFO = 0b00_00_01_00,
    VIEW_CLOUD_STORAGE,

}

public enum ServerFlags : byte
{
    OK = 0b00_00_00_00,
    USERNAME_TAKEN = 0b00_00_00_01,
    USERNAME_DOESNT_EXIST = 0b_00_00_00_10,
    PASSWORD_INCORRECT = 0b_00_00_00_11,
    INCORRECT_CREDENTIALS_STRUCTURE = 0b00_00_01_00,
    QUIT_CONNECTION = 0b00__00_01_01,
    PASSWORD_TOO_LONG = 0b00_00_01_10,
    USERNAME_TOO_LONG = 0b00_00_01_11,
    TOO_MANY_ATTEMPTS = 0b00_00_10_00,
    KICKED_DUE_TO_INACTIVITY = 0b00_00_10_01,
    UNEXPECTED_SERVER_ERROR = 0b00_00_10_10,

}
