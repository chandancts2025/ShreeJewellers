namespace ShreeJewellers.Application.DTOs.Auth;

// ─────────────────────────────────────────────────────────────────────────────
// Registration
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Step 1 of 3 in customer registration — basic account details.</summary>
public record RegisterStep1Dto(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Password,
    string ConfirmPassword
);

/// <summary>Step 2 — KYC personal details.</summary>
public record RegisterStep2Dto(
    string CustomerId,          // Temp ID returned after Step 1
    DateOnly DateOfBirth,
    string Gender,
    string AadhaarNumber,       // Plaintext — encrypted before persistence
    string PANNumber,           // Plaintext — encrypted before persistence
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PinCode,
    string? AlternatePhone,
    string? ReferralCode
);

/// <summary>Step 3 — ID proof document upload (handled as multipart/form-data).</summary>
public record RegisterStep3Dto(
    string CustomerId,
    string? ProfilePhotoBase64,
    string? IDProofBase64,
    string? FileExtension
);

/// <summary>Full registration DTO used when Admin creates a staff/customer account.</summary>
public record AdminCreateUserDto(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string? AlternatePhone,
    string Role,               // "Admin" | "Staff" | "Customer"
    DateOnly DateOfBirth,
    string Gender,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PinCode,
    string? AadhaarNumber,
    string? PANNumber
);

// ─────────────────────────────────────────────────────────────────────────────
// Login & Token
// ─────────────────────────────────────────────────────────────────────────────

public record LoginDto(
    string Username,           // Email OR CustomerCode
    string Password,
    bool RememberMe = false
);

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string UserId,
    string FullName,
    string Email,
    string CustomerCode,
    IEnumerable<string> Roles,
    string KYCStatus
);

public record RefreshTokenDto(
    string RefreshToken
);

public record RevokeTokenDto(
    string RefreshToken
);

// ─────────────────────────────────────────────────────────────────────────────
// Password Management
// ─────────────────────────────────────────────────────────────────────────────

public record ForgotPasswordDto(
    string Email
);

public record ResetPasswordDto(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmPassword
);

public record ChangePasswordDto(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword
);

// ─────────────────────────────────────────────────────────────────────────────
// Email Verification & OTP
// ─────────────────────────────────────────────────────────────────────────────

public record VerifyEmailDto(
    string UserId,
    string Token
);

public record ResendVerificationDto(
    string Email
);

public record VerifyOtpDto(
    string UserId,
    string Otp,
    string Purpose       // "Login2FA" | "SensitiveOp"
);

public record SendOtpDto(
    string UserId,
    string Purpose
);

// ─────────────────────────────────────────────────────────────────────────────
// KYC
// ─────────────────────────────────────────────────────────────────────────────

public record KYCVerifyDto(
    string CustomerId,
    string Notes
);

public record KYCRejectDto(
    string CustomerId,
    string RejectionReason
);

public record KYCStatusResponseDto(
    string CustomerId,
    string CustomerCode,
    string FullName,
    string KYCStatus,
    DateTime? VerifiedAt,
    string? RejectionReason
);
