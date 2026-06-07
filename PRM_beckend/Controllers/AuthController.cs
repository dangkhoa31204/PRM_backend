using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PRM_beckend.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PRM_beckend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public record LoginRequest(string UsernameOrEmail, string Password);

    public record LoginResponse(string AccessToken, DateTime ExpiresAt, string Username, int Role);

    public record AdminCreateAccountRequest(string Username, string Email, string Password, string FullName, string? PhoneNumber, int Role);

    public record RegisterRequest(string Username, string Email, string Password, string FullName, string? PhoneNumber);

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("UsernameOrEmail and Password are required.");
        }

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.IsActive &&
                                      (a.Username == request.UsernameOrEmail || a.Email == request.UsernameOrEmail));

        if (account == null)
        {
            return Unauthorized();
        }

        var isValidPassword = false;
        try
        {
            isValidPassword = BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Unauthorized();
        }

        if (!isValidPassword)
        {
            return Unauthorized();
        }

        account.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var token = CreateToken(account);
        return Ok(token);
    }

    [HttpPost("register")]
    public async Task<ActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest("Username, Email, Password, and FullName are required.");
        }

        var exists = await _context.Accounts.AnyAsync(a => a.Username == request.Username || a.Email == request.Email);
        if (exists)
        {
            return Conflict("Username or Email already exists.");
        }

        var account = new Models.Account
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Role = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Register), new { accountId = account.AccountId }, new
        {
            account.AccountId,
            account.Username,
            account.Email,
            account.FullName,
            account.PhoneNumber,
            account.Role,
            account.IsActive,
            account.CreatedAt
        });
    }

    [Authorize(Roles = "1")]
    [HttpGet("admin-only")]
    public IActionResult AdminOnly()
    {
        return Ok("Admin access granted");
    }

    [Authorize(Roles = "1")]
    [HttpPost("admin-create")]
    public async Task<ActionResult> AdminCreate([FromBody] AdminCreateAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest("Username, Email, Password, and FullName are required.");
        }

        var exists = await _context.Accounts.AnyAsync(a => a.Username == request.Username || a.Email == request.Email);
        if (exists)
        {
            return Conflict("Username or Email already exists.");
        }

        var account = new Models.Account
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(AdminCreate), new { accountId = account.AccountId }, new
        {
            account.AccountId,
            account.Username,
            account.Email,
            account.FullName,
            account.PhoneNumber,
            account.Role,
            account.IsActive,
            account.CreatedAt
        });
    }

    private LoginResponse CreateToken(Models.Account account)
    {
        var key = _configuration["Jwt:Key"] ?? string.Empty;
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.AccountId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, account.Username),
            new(ClaimTypes.Role, account.Role.ToString())
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.AddHours(4);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new LoginResponse(tokenString, expires, account.Username, account.Role);
    }
}
