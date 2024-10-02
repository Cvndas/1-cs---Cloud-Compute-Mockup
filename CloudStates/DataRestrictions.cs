public class AuthenticationRestrictions
{
    public const int MAX_USERNAME_LENGTH = 15;  // bytes or string.Length
    public const int MAX_PASSWORD_LENGTH = 15; // bytes or string.Length
    public const int MAX_LOGIN_ATTEMPTS = 3;
    public const int MAX_REGISTRATION_ATTEMPTS = 3;
    public const int MAX_AUTHENTICATION_CHOICE_MISTAKES = 3;
}