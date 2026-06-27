using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShreeJewellers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MetalType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProfilePhotoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IDProofUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AadhaarNumberEncrypted = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PANNumberEncrypted = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PinCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AlternatePhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    CustomerCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    KYCStatus = table.Column<int>(type: "int", nullable: false),
                    KYCVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KYCVerifiedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    KYCRejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReferredByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LoyaltyPoints = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginIP = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Users_KYCVerifiedByUserId",
                        column: x => x.KYCVerifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        //onDelete: ReferentialAction.SetNull);
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_Users_Users_ReferredByUserId",
                        column: x => x.ReferredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                         //onDelete: ReferentialAction.SetNull);
                         onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SKUCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HSNCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Purity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NetWeightGrams = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    GrossWeightGrams = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    StoneWeightGrams = table.Column<decimal>(type: "decimal(10,3)", nullable: false, defaultValue: 0m),
                    HallmarkNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MakingChargesType = table.Column<int>(type: "int", nullable: false),
                    MakingChargesValue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    WastagePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    HallmarkCharges = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    GSTRatePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 3m),
                    StoneValue = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    StockQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ReorderLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 2),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BarcodeData = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEncrypted = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppSettings_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IPAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GoldPriceHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    GoldRatePer10Gram_22K = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    GoldRatePer10Gram_24K = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    SilverRatePerKg = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsManualOverride = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldPriceHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoldPriceHistory_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InterestSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    InterestRatePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    CompoundingEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PenaltyRatePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 1m),
                    DefaultTenureMonths = table.Column<int>(type: "int", nullable: false, defaultValue: 12),
                    LoanToValuePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 75m),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterestSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterestSettings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RelatedEntityId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false, defaultValue: 0m),
                    TaxAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    OldGoldExchangeValue = table.Column<decimal>(type: "decimal(14,2)", nullable: false, defaultValue: 0m),
                    NetAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    PaymentMode = table.Column<int>(type: "int", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(14,2)", nullable: false, defaultValue: 0m),
                    AdvanceAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false, defaultValue: 0m),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InvoiceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CGSTAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false, defaultValue: 0m),
                    SGSTAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false, defaultValue: 0m),
                    IGSTAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrders_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_Users_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    WeightGrams = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    RatePerGram = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoldLoans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    InterestSettingId = table.Column<int>(type: "int", nullable: false),
                    LoanNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LoanDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MaturityDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClosedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GoldDepositedWeightGrams = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    GoldPurity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GoldCurrentValueAtDeposit = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    LoanToValuePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TotalRepaid = table.Column<decimal>(type: "decimal(14,2)", nullable: false, defaultValue: 0m),
                    LoanStatus = table.Column<int>(type: "int", nullable: false),
                    ExtensionCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldLoans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoldLoans_InterestSettings_InterestSettingId",
                        column: x => x.InterestSettingId,
                        principalTable: "InterestSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoldLoans_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoldLoans_Users_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesOrderId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    WeightGrams = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    RatePerGram = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MakingCharges = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    HallmarkCharges = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    StoneValue = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    TaxPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 3m),
                    TaxAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    LineTotal = table.Column<decimal>(type: "decimal(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrderItems_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoldLoanItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoldLoanId = table.Column<int>(type: "int", nullable: false),
                    ItemDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WeightGrams = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    Purity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EstimatedValue = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HallmarkNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldLoanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoldLoanItems_GoldLoans_GoldLoanId",
                        column: x => x.GoldLoanId,
                        principalTable: "GoldLoans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoanRepayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoldLoanId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RepaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    PrincipalComponent = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    InterestComponent = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    PenaltyAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false, defaultValue: 0m),
                    PaymentMode = table.Column<int>(type: "int", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PrincipalBalanceAfter = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanRepayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanRepayments_GoldLoans_GoldLoanId",
                        column: x => x.GoldLoanId,
                        principalTable: "GoldLoans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanRepayments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_Key",
                table: "AppSettings",
                column: "SettingKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_UpdatedByUserId",
                table: "AppSettings",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Entity",
                table: "AuditLogs",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId",
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_IsActive",
                table: "Categories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoldLoanItems_LoanId",
                table: "GoldLoanItems",
                column: "GoldLoanId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldLoans_CreatedByUserId",
                table: "GoldLoans",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldLoans_Customer_Status",
                table: "GoldLoans",
                columns: new[] { "CustomerUserId", "LoanStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldLoans_CustomerId",
                table: "GoldLoans",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldLoans_InterestSettingId",
                table: "GoldLoans",
                column: "InterestSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldLoans_LoanNumber",
                table: "GoldLoans",
                column: "LoanNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoldLoans_MaturityDate",
                table: "GoldLoans",
                column: "MaturityDate");

            migrationBuilder.CreateIndex(
                name: "IX_GoldLoans_Status",
                table: "GoldLoans",
                column: "LoanStatus");

            migrationBuilder.CreateIndex(
                name: "IX_GoldLoans_Status_MaturityDate",
                table: "GoldLoans",
                columns: new[] { "LoanStatus", "MaturityDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldPriceHistory_CreatedByUserId",
                table: "GoldPriceHistory",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldPriceHistory_Date_Source",
                table: "GoldPriceHistory",
                columns: new[] { "RecordedAt", "Source" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldPriceHistory_RecordedAt",
                table: "GoldPriceHistory",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InterestSettings_ActiveSetting",
                table: "InterestSettings",
                column: "EffectiveTo",
                filter: "[EffectiveTo] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InterestSettings_CreatedByUserId",
                table: "InterestSettings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_CreatedByUserId",
                table: "InventoryTransactions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvTrans_CreatedAt",
                table: "InventoryTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InvTrans_ProductId",
                table: "InventoryTransactions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InvTrans_ReferenceNo",
                table: "InventoryTransactions",
                column: "ReferenceNo",
                filter: "[ReferenceNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvTrans_Type",
                table: "InventoryTransactions",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_LoanRepayments_CreatedByUserId",
                table: "LoanRepayments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanRepayments_Date",
                table: "LoanRepayments",
                column: "RepaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_LoanRepayments_LoanId",
                table: "LoanRepayments",
                column: "GoldLoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanRepayments_Receipt",
                table: "LoanRepayments",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_User_Read",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Active_Stock",
                table: "Products",
                columns: new[] { "IsActive", "StockQuantity" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive",
                table: "Products",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SKUCode",
                table: "Products",
                column: "SKUCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderItems_OrderId",
                table: "SalesOrderItems",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderItems_ProductId",
                table: "SalesOrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CreatedByUserId",
                table: "SalesOrders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustomerUserId",
                table: "SalesOrders",
                column: "CustomerUserId",
                filter: "[CustomerUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Date_Status",
                table: "SalesOrders",
                columns: new[] { "OrderDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_OrderDate",
                table: "SalesOrders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_OrderNumber",
                table: "SalesOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Status",
                table: "SalesOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CustomerCode",
                table: "Users",
                column: "CustomerCode",
                unique: true,
                filter: "[CustomerCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_KYCStatus",
                table: "Users",
                column: "KYCStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Users_KYCVerifiedByUserId",
                table: "Users",
                column: "KYCVerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferredByUserId",
                table: "Users",
                column: "ReferredByUserId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "GoldLoanItems");

            migrationBuilder.DropTable(
                name: "GoldPriceHistory");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "LoanRepayments");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "SalesOrderItems");

            migrationBuilder.DropTable(
                name: "UserClaims");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "GoldLoans");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "SalesOrders");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "InterestSettings");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
