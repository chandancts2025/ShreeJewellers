using FluentValidation;
using ShreeJewellers.Application.DTOs.Auth;

namespace ShreeJewellers.Application.Validators;

// ─────────────────────────────────────────────────────────────────────────────
// Registration Validators
// ─────────────────────────────────────────────────────────────────────────────

public class RegisterStep1Validator : AbstractValidator<RegisterStep1Dto>
{
    public RegisterStep1Validator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters.")
            .Matches(@"^[a-zA-Z\s\-']+$").WithMessage("First name can only contain letters, spaces, hyphens, and apostrophes.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters.")
            .Matches(@"^[a-zA-Z\s\-']+$").WithMessage("Last name can only contain letters, spaces, hyphens, and apostrophes.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Mobile number is required.")
            .Matches(@"^[6-9]\d{9}$").WithMessage("Enter a valid 10-digit Indian mobile number.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(100).WithMessage("Password cannot exceed 100 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"\d").WithMessage("Password must contain at least one digit.")
            .Matches(@"[^a-zA-Z\d]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Please confirm your password.")
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}

public class RegisterStep2Validator : AbstractValidator<RegisterStep2Dto>
{
    public RegisterStep2Validator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Invalid registration session.");

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required.")
            .Must(dob => dob <= DateOnly.FromDateTime(DateTime.Today.AddYears(-18)))
                .WithMessage("You must be at least 18 years old.")
            .Must(dob => dob >= DateOnly.FromDateTime(DateTime.Today.AddYears(-120)))
                .WithMessage("Please enter a valid date of birth.");

        RuleFor(x => x.Gender)
            .NotEmpty().WithMessage("Gender is required.")
            .Must(g => new[] { "Male", "Female", "Other" }.Contains(g))
                .WithMessage("Gender must be Male, Female, or Other.");

        RuleFor(x => x.AadhaarNumber)
            .NotEmpty().WithMessage("Aadhaar number is required.")
            .Matches(@"^\d{12}$").WithMessage("Aadhaar number must be exactly 12 digits.");

        RuleFor(x => x.PANNumber)
            .NotEmpty().WithMessage("PAN number is required.")
            .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$").WithMessage("Enter a valid PAN number (e.g., ABCDE1234F).");

        RuleFor(x => x.AddressLine1)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(200).WithMessage("Address cannot exceed 200 characters.");

        RuleFor(x => x.AddressLine2)
            .MaximumLength(200).WithMessage("Address line 2 cannot exceed 200 characters.")
            .When(x => !string.IsNullOrEmpty(x.AddressLine2));

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(100).WithMessage("City cannot exceed 100 characters.");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("State is required.")
            .MaximumLength(100).WithMessage("State cannot exceed 100 characters.");

        RuleFor(x => x.PinCode)
            .NotEmpty().WithMessage("PIN code is required.")
            .Matches(@"^\d{6}$").WithMessage("PIN code must be exactly 6 digits.");

        RuleFor(x => x.AlternatePhone)
            .Matches(@"^[6-9]\d{9}$").WithMessage("Enter a valid 10-digit alternate mobile number.")
            .When(x => !string.IsNullOrEmpty(x.AlternatePhone));

        RuleFor(x => x.ReferralCode)
            .Matches(@"^CUST-\d{4}-\d+$").WithMessage("Invalid referral code format.")
            .When(x => !string.IsNullOrEmpty(x.ReferralCode));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Login Validator
// ─────────────────────────────────────────────────────────────────────────────

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Email or Customer Code is required.")
            .MaximumLength(256).WithMessage("Username too long.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(100).WithMessage("Password too long.");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Password Validators
// ─────────────────────────────────────────────────────────────────────────────

public class ForgotPasswordValidator : AbstractValidator<ForgotPasswordDto>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}

public class ResetPasswordValidator : AbstractValidator<ResetPasswordDto>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Valid email required.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches(@"[A-Z]").WithMessage("Must contain an uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Must contain a lowercase letter.")
            .Matches(@"\d").WithMessage("Must contain a digit.")
            .Matches(@"[^a-zA-Z\d]").WithMessage("Must contain a special character.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

public class ChangePasswordValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches(@"[A-Z]").WithMessage("Must contain an uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Must contain a lowercase letter.")
            .Matches(@"\d").WithMessage("Must contain a digit.")
            .Matches(@"[^a-zA-Z\d]").WithMessage("Must contain a special character.")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must differ from current password.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Admin Create User Validator
// ─────────────────────────────────────────────────────────────────────────────

public class AdminCreateUserValidator : AbstractValidator<AdminCreateUserDto>
{
    private static readonly string[] ValidRoles = { "Admin", "Staff", "Customer" };

    public AdminCreateUserValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(@"^[6-9]\d{9}$").WithMessage("Valid 10-digit mobile required.");
        RuleFor(x => x.AlternatePhone).Matches(@"^[6-9]\d{9}$").When(x => !string.IsNullOrEmpty(x.AlternatePhone));
        RuleFor(x => x.Role).NotEmpty().Must(r => ValidRoles.Contains(r)).WithMessage("Role must be Admin, Staff, or Customer.");
        RuleFor(x => x.DateOfBirth)
            .Must(dob => dob <= DateOnly.FromDateTime(DateTime.Today.AddYears(-18)))
            .WithMessage("User must be at least 18 years old.");
        RuleFor(x => x.AddressLine1).NotEmpty().MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.State).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PinCode).NotEmpty().Matches(@"^\d{6}$");
        RuleFor(x => x.AadhaarNumber).Matches(@"^\d{12}$").When(x => !string.IsNullOrEmpty(x.AadhaarNumber));
        RuleFor(x => x.PANNumber).Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$").When(x => !string.IsNullOrEmpty(x.PANNumber));
    }
}
