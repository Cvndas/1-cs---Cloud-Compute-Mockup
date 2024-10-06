using System.Linq.Expressions;

public class SystemConstants
{
    public const int MAX_USERNAME_LENGTH = 15;  // bytes or string.Length
    public const int MAX_PASSWORD_LENGTH = 15; // bytes or string.Length
    public const int MAX_LOGIN_ATTEMPTS = 3;
    public const int MAX_REGISTRATION_ATTEMPTS = 3;
    public const int MAX_AUTHENTICATION_CHOICE_MISTAKES = 3;

    public const int MAX_FILENAME_LENGTH = 15;
    public const int MAX_FILESIZE_MB = 20;
    public const int MAX_FILES_IN_CLOUD = 5;
    public const int MAX_USERS_IN_QUEUE = 100;
    public const int MAX_BODY_BYTE_LEN = 50;
    public static readonly string CHAT_MESSAGE_PREAMBLE = ": ";
    /// <summary>
    /// Max length of a chat message's body, which is the body excluding "Username: "
    /// </summary>
    public static readonly int MAX_CHAT_MESSAGE_LEN = MAX_BODY_BYTE_LEN - MAX_USERNAME_LENGTH - CHAT_MESSAGE_PREAMBLE.Length;

    /// <summary>
    /// Max length of a chat message in the format of "Username: hey there."
    /// </summary>
    public static readonly int MAX_FORMATTED_CHAT_MESSAGE_BODY_LEN = MAX_BODY_BYTE_LEN;
}