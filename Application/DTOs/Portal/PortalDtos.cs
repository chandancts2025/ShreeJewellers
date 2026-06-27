using ShreeJewellers.Domain.Enums;

namespace ShreeJewellers.Application.DTOs.Portal;

public record PublicHomeDto(
    string ShopName,
    int TrustedSinceYear,
    string HeroHeadline,
    string HeroSubheadline,
    string AboutSummary,
    IEnumerable<PublicFeatureDto> Highlights,
    IEnumerable<PublicCategoryCardDto> FeaturedCategories,
    IEnumerable<TestimonialDto> Testimonials,
    IEnumerable<QuickLinkDto> QuickLinks
);

public record PublicAboutDto(
    string ShopName,
    int TrustedSinceYear,
    string Story,
    string OwnerName,
    string OwnerTitle,
    string? OwnerPhotoUrl,
    IEnumerable<string> Certifications,
    string HallmarkNote
);

public record PublicContactDto(
    string ShopName,
    string Address,
    string Phone,
    string Email,
    string WhatsAppNumber,
    string MapEmbedUrl,
    string BusinessHours
);

public record PublicGoldLoanInfoDto(
    decimal InterestRatePercent,
    string InterestCalculationType,
    int DefaultTenureMonths,
    decimal LoanToValuePercent,
    IEnumerable<string> Steps,
    IEnumerable<string> RequiredDocuments
);

public record PublicFeatureDto(string Title, string Description, string Icon);
public record PublicCategoryCardDto(int CategoryId, string Name, string MetalType, int ProductCount, string Description);
public record TestimonialDto(string CustomerName, string Quote, string Location);
public record QuickLinkDto(string Title, string Route);

public record ContactInquiryDto(
    string Name,
    string Phone,
    string? Email,
    string Message
);

public record CustomerDashboardDto(
    string UserId,
    string FullName,
    string CustomerCode,
    string KycStatus,
    int ActiveLoanCount,
    decimal ActiveLoanOutstanding,
    decimal InterestAccrued,
    int RecentOrderCount,
    decimal RecentOrderValue,
    IEnumerable<LoanDashboardCardDto> ActiveLoans,
    IEnumerable<OrderDashboardCardDto> RecentOrders
);

public record LoanDashboardCardDto(
    int LoanId,
    string LoanNumber,
    decimal PrincipalAmount,
    int DaysRemaining,
    decimal InterestAccrued,
    decimal OutstandingBalance,
    string Status
);

public record OrderDashboardCardDto(
    int OrderId,
    string OrderNumber,
    DateOnly OrderDate,
    decimal NetAmount,
    string Status,
    string PaymentStatus
);

public record ProfileDto(
    string UserId,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string PhoneNumber,
    string? AlternatePhone,
    DateOnly? DateOfBirth,
    string? Gender,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PinCode,
    string? CustomerCode,
    string KycStatus,
    string? ProfilePhotoUrl,
    string? IdProofUrl,
    string? KycRejectionReason
);

public record UpdateProfileDto(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? AlternatePhone,
    DateOnly? DateOfBirth,
    string? Gender,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PinCode
);

public record CustomerListItemDto(
    string UserId,
    string FullName,
    string Email,
    string PhoneNumber,
    string? CustomerCode,
    string KycStatus,
    bool IsActive,
    DateTime CreatedAt,
    string Role
);

public record CustomerListResponseDto(
    int Total,
    int Page,
    int PageSize,
    IEnumerable<CustomerListItemDto> Data
);

public record CustomerDetailDto(
    string UserId,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string PhoneNumber,
    string? AlternatePhone,
    DateOnly? DateOfBirth,
    string? Gender,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PinCode,
    string? CustomerCode,
    string KycStatus,
    bool IsActive,
    DateTime CreatedAt,
    string Role
);

public record UpdateCustomerDto(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? AlternatePhone,
    string Email,
    DateOnly? DateOfBirth,
    string? Gender,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PinCode,
    bool IsActive
);

public record ShopSettingsDto(
    string ShopName,
    string ShopAddress,
    string GSTNumber,
    string ShopPhone,
    string ShopEmail,
    string LogoUrl,
    string WhatsAppNumber,
    string MapEmbedUrl,
    string TrustedSinceYear,
    string BusinessHours,
    string AboutSummary,
    string AboutStory,
    string OwnerName,
    string OwnerTitle,
    string OwnerPhotoUrl
);

public record UpdateShopSettingsDto(
    string ShopName,
    string ShopAddress,
    string GSTNumber,
    string ShopPhone,
    string ShopEmail,
    string LogoUrl,
    string WhatsAppNumber,
    string MapEmbedUrl,
    string TrustedSinceYear,
    string BusinessHours,
    string AboutSummary,
    string AboutStory,
    string OwnerName,
    string OwnerTitle,
    string OwnerPhotoUrl
);

public record InterestSettingListItemDto(
    int Id,
    decimal InterestRatePercent,
    InterestCalculationType CalculationType,
    bool CompoundingEnabled,
    decimal PenaltyRatePercent,
    int DefaultTenureMonths,
    decimal LoanToValuePercent,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo
);

public record UpsertInterestSettingDto(
    decimal InterestRatePercent,
    InterestCalculationType CalculationType,
    bool CompoundingEnabled,
    decimal PenaltyRatePercent,
    int DefaultTenureMonths,
    decimal LoanToValuePercent,
    DateOnly EffectiveFrom
);

public record GoldPriceSettingsDto(
    string ApiKeyHint,
    string RefreshIntervalMinutes,
    string ManualOverrideReasonHint
);

public record UpdateGoldPriceSettingsDto(
    string ApiKey,
    string RefreshIntervalMinutes
);
