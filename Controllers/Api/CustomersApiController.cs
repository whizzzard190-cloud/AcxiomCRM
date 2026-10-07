using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/customers")]
[Authorize]
public class CustomersApiController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersApiController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _customerService.GetCustomersAsync(User, search, null, null, page, pageSize);
        var dtos = result.Items.Select(ToDto).ToList();
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _customerService.GetCustomerByIdAsync(User, id);
        if (customer == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Requested record was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }
        return Ok(ToDto(customer));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid data.", Status = StatusCodes.Status400BadRequest });
        }

        if (await _customerService.IsEmailDuplicateAsync(dto.Email))
        {
            return Conflict(new ProblemDetails
            {
                Title = "A customer with this email address already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        if (await _customerService.IsPhoneDuplicateAsync(dto.Phone))
        {
            return Conflict(new ProblemDetails
            {
                Title = "A customer with this phone number already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var customer = new Customer
        {
            CustomerName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Notes = dto.Notes,
            OwnerId = dto.OwnerId ?? ""
        };

        var (success, error, created) = await _customerService.CreateCustomerAsync(User, customer);
        if (!success)
        {
            if (error != null && (error.Contains("already exists") || error.Contains("duplicate", StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict(new ProblemDetails { Title = error, Status = StatusCodes.Status409Conflict });
            }
            return BadRequest(new ProblemDetails { Title = error ?? "Failed to create customer.", Status = StatusCodes.Status400BadRequest });
        }

        var responseDto = ToDto(created!);
        return CreatedAtAction(nameof(GetById), new { id = responseDto.CustomerId }, responseDto);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid data.", Status = StatusCodes.Status400BadRequest });
        }

        var existing = await _customerService.GetCustomerByIdAsync(User, id);
        if (existing == null)
        {
            return NotFound(new ProblemDetails { Title = "Requested record was not found.", Status = StatusCodes.Status404NotFound });
        }

        if (await _customerService.IsEmailDuplicateAsync(dto.Email, id))
        {
            return Conflict(new ProblemDetails { Title = "A customer with this email address already exists.", Status = StatusCodes.Status409Conflict });
        }

        if (await _customerService.IsPhoneDuplicateAsync(dto.Phone, id))
        {
            return Conflict(new ProblemDetails { Title = "A customer with this phone number already exists.", Status = StatusCodes.Status409Conflict });
        }

        var customer = new Customer
        {
            CustomerId = id,
            CustomerName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Status = dto.Status,
            Notes = dto.Notes,
            OwnerId = dto.OwnerId ?? existing.OwnerId
        };

        var (success, error, updated) = await _customerService.UpdateCustomerAsync(User, customer);
        if (!success)
        {
            if (error == "You are not authorized to perform this action.")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = error, Status = StatusCodes.Status403Forbidden });
            }
            return BadRequest(new ProblemDetails { Title = error ?? "Update failed.", Status = StatusCodes.Status400BadRequest });
        }

        return Ok(ToDto(updated!));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _customerService.DeactivateCustomerAsync(User, id);
        if (!success)
        {
            if (error == "Requested record was not found.")
            {
                return NotFound(new ProblemDetails { Title = error, Status = StatusCodes.Status404NotFound });
            }
            if (error == "You are not authorized to perform this action.")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = error, Status = StatusCodes.Status403Forbidden });
            }
            return BadRequest(new ProblemDetails { Title = error ?? "Failed to deactivate customer.", Status = StatusCodes.Status400BadRequest });
        }

        return Ok(new { Message = "Customer deactivated successfully." });
    }

    private static CustomerDto ToDto(Customer c) => new()
    {
        CustomerId = c.CustomerId,
        CustomerCode = c.CustomerCode,
        CustomerName = c.CustomerName,
        Email = c.Email,
        Phone = c.Phone,
        CompanyName = c.CompanyName,
        Address = c.Address,
        City = c.City,
        State = c.State,
        Status = c.Status,
        Notes = c.Notes,
        OwnerId = c.OwnerId,
        OwnerName = c.Owner?.FullName,
        CreatedDate = c.CreatedDate
    };
}
