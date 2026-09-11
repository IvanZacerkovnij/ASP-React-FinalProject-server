namespace Threads.Application.DTOs.Auth.Responses;

public enum ChangePasswordStatus
{
    ConfirmationCodeSent,
    PasswordChanged,
    UserNotFound,
    InvalidCurrentPassword,
    InvalidConfirmationCode,
    NoPendingPasswordChange,
    InvalidNewPassword
}
