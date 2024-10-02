namespace CloudStates;

[Flags]
public enum ClientFlags : byte
{
    CLIENT_QUIT = 0b00_00_00_00,
    REGISTER_REQUEST = 0b00_00_00_01,
    LOGIN_REQUEST = 0b00_00_00_10,
    SENDING_LOGIN_INFO = 0b00_00_00_11,
    SENDING_REGISTRATION_INFO = 0b00_00_01_00
}

public enum ServerFlags : byte
{
    OK = 0b00_00_00_00,
    USERNAME_TAKEN = 0b00_00_00_01,
    USERNAME_DOESNT_EXIST = 0b_00_00_00_10,
    PASSWORD_INCORRECT = 0b_00_00_00_11,
}