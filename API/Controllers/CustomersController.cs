using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShreeJewellers.Application.DTOs.Portal;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Infrastructure.Data;

namespace ShreeJewellers.API.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = "SuperAdmin,Admin,Staff")]
[Produces("application/json")]
public class CustomersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public CustomersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] string? search = null,
        [FromQuery] string? kycStatus = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var customerIds = await GetCustomerIdsAsync();
        var query = _db.Users.Where(u => customerIds.Contains(u.Id));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s) ||
                (u.CustomerCode ?? string.Empty).ToLower().Contains(s) ||
                (u.PhoneNumber ?? string.Empty).Contains(s) ||
                (u.Email ?? string.Empty).ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(kycStatus) &&
            Enum.TryParse<ShreeJewellers.Domain.Enums.KYCStatus>(kycStatus, true, out var parsedStatus))
        {
            query = query.Where(u => u.KYCStatus == parsedStatus);
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        var total = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var data = new List<CustomerListItemDto>();
        foreach (var user in users)
        {
            data.Add(await MapToListItemAsync(user));
        }

        return Ok(new CustomerListResponseDto(total, page, pageSize, data));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCustomer(string id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null || !await IsCustomerAsync(user.Id)) return NotFound();

        return Ok(await MapToDetailAsync(user));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> UpdateCustomer(string id, [FromBody] UpdateCustomerDto dto)
    {
        if (!HasRequiredCustomerFields(dto))
        {
            return BadRequest(new { message = "First name, last name, phone, email, and address are required." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null || !await IsCustomerAsync(user.Id)) return NotFound();

        var email = dto.Email.Trim().ToLowerInvariant();
        var duplicateEmail = await _userManager.FindByEmailAsync(email);
        if (duplicateEmail is not null && duplicateEmail.Id != id)
        {
            return Conflict(new { message = "Another user already uses this email address." });
        }

        var phone = dto.PhoneNumber.Trim();
        var phoneExists = await _db.Users.AnyAsync(u => u.Id != id && u.PhoneNumber == phone);
        if (phoneExists)
        {
            return Conflict(new { message = "Another user already uses this phone number." });
        }

        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.PhoneNumber = phone;
        user.AlternatePhone = dto.AlternatePhone?.Trim();
        user.Email = email;
        user.UserName = email;
        user.NormalizedEmail = email.ToUpperInvariant();
        user.NormalizedUserName = email.ToUpperInvariant();
        if (dto.DateOfBirth.HasValue) user.DateOfBirth = dto.DateOfBirth.Value;
        if (!string.IsNullOrWhiteSpace(dto.Gender)) user.Gender = dto.Gender.Trim();
        user.AddressLine1 = dto.AddressLine1.Trim();
        user.AddressLine2 = dto.AddressLine2?.Trim();
        user.City = dto.City.Trim();
        user.State = dto.State.Trim();
        user.PinCode = dto.PinCode.Trim();
        user.IsActive = dto.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(await MapToDetailAsync(user));
    }

    [HttpPost("{id}/activate")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> ActivateCustomer(string id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null || !await IsCustomerAsync(user.Id)) return NotFound();

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Customer activated successfully." });
    }

    [HttpPost("{id}/deactivate")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> DeactivateCustomer(string id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null || !await IsCustomerAsync(user.Id)) return NotFound();

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Customer deactivated successfully." });
    }

    private async Task<CustomerListItemDto> MapToListItemAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new CustomerListItemDto(
            user.Id,
            user.FullName,
            user.Email ?? string.Empty,
            user.PhoneNumber ?? string.Empty,
            user.CustomerCode,
            user.KYCStatus.ToString(),
            user.IsActive,
            user.CreatedAt,
            roles.FirstOrDefault() ?? "Customer");
    }

    private async Task<CustomerDetailDto> MapToDetailAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new CustomerDetailDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.Email ?? string.Empty,
            user.PhoneNumber ?? string.Empty,
            user.AlternatePhone,
            user.DateOfBirth,
            user.Gender,
            user.AddressLine1,
            user.AddressLine2,
            user.City,
            user.State,
            user.PinCode,
            user.CustomerCode,
            user.KYCStatus.ToString(),
            user.IsActive,
            user.CreatedAt,
            roles.FirstOrDefault() ?? "Customer");
    }

    private async Task<bool> IsCustomerAsync(string userId)
    {
        var roleId = await _db.Roles
            .Where(r => r.Name == "Customer")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        return roleId is not null &&
            await _db.UserRoles.AnyAsync(ur => ur.RoleId == roleId && ur.UserId == userId);
    }

    private async Task<List<string>> GetCustomerIdsAsync()
    {
        var roleId = await _db.Roles.Where(r => r.Name == "Customer").Select(r => r.Id).FirstOrDefaultAsync();
        return roleId is null
            ? new List<string>()
            : await _db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId).ToListAsync();
    }

    private static bool HasRequiredCustomerFields(UpdateCustomerDto dto)
        => !string.IsNullOrWhiteSpace(dto.FirstName)
           && !string.IsNullOrWhiteSpace(dto.LastName)
           && !string.IsNullOrWhiteSpace(dto.PhoneNumber)
           && !string.IsNullOrWhiteSpace(dto.Email)
           && !string.IsNullOrWhiteSpace(dto.AddressLine1)
           && !string.IsNullOrWhiteSpace(dto.City)
           && !string.IsNullOrWhiteSpace(dto.State)
           && !string.IsNullOrWhiteSpace(dto.PinCode);
}
