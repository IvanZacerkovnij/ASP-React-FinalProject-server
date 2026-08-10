namespace Threads.Application.DTOs.Auth;

public enum ChangePasswordStatus
{
    ConfirmationCodeSent,
    PasswordChanged,
    UserNotFound,
    InvalidCurrentPassword,
    InvalidConfirmationCode,
    NoPendingPasswordChange
}
