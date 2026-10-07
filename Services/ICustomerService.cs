using System.Security.Claims;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Services;

public interface ICustomerService
{
    Task<PagedList<Customer>> GetCustomersAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? sortBy = null,
        string? sortOrder = null,
        int page = 1,
        int pageSize = 10);

    Task<Customer?> GetCustomerByIdAsync(ClaimsPrincipal user, int id);
    Task<(bool Success, string? Error, Customer? Customer)> CreateCustomerAsync(ClaimsPrincipal user, Customer customer);
    Task<(bool Success, string? Error, Customer? Customer)> UpdateCustomerAsync(ClaimsPrincipal user, Customer customer);
    Task<(bool Success, string? Error)> DeactivateCustomerAsync(ClaimsPrincipal user, int id);
    Task<bool> IsEmailDuplicateAsync(string email, int? excludeCustomerId = null);
    Task<bool> IsPhoneDuplicateAsync(string phone, int? excludeCustomerId = null);
}
