﻿namespace CloudStates;

public enum ClientStates
{
    NO_CONNECTION, 
    PROGRAM_CLOSED,
    CHOOSING_AUTHENTICATE_METHOD,
    REGISTERING,
    LOGGING_IN,
    LOGIN_INFO_SENT,
    REGISTRATION_INFO_SENT,
    LOGGED_IN, // TODO State
    VIEWING_LOCAL_FILES, // TODO State
    VIEWING_CLOUD_STORAGE, // TODO State
    AWAITING_FILE_DOWNLOAD, 
    AWAITING_FILE_UPLOAD 
}

public enum ServerStates
{
    NO_CONNECTION,
    PROCESS_AUTHENTICATION_CHOICE, // Entry state when Employee is assigned to a User.
    PROCESS_REGISTRATION,
    REGISTRATION_INFO_RECEIVED,
    PROCESS_LOGIN,
    IN_DASHBOARD,
    WAITING_FOR_FILE_UPLOAD, // Special state, that really shouldn't be interrupted
    SENDING_FILE_TO_CLIENT, // Special state, that really shouldn't be interrupted
}
