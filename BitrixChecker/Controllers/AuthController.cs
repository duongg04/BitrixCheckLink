using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BitrixChecker.Configuration;
using BitrixChecker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BitrixChecker.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IOptions<JwtOptions> jwt) : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly JwtOptions _jwt = jwt.Value;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Tên đăng nhập và mật khẩu là bắt buộc." });
        }

        var user = await _userManager.FindByNameAsync(request.UserName)
                   ?? await _userManager.FindByEmailAsync(request.UserName);

        if (user == null)
        {
            return Unauthorized(new { message = "Thông tin đăng nhập không đúng." });
        }

        var passwordResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, true);
        if (!passwordResult.Succeeded)
        {
            return Unauthorized(new { message = "Thông tin đăng nhập không đúng." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = CreateToken(user, roles);

        return Ok(new
        {
            token,
            user = new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.DisplayName,
                roles
            }
        });
    }

    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Tên đăng nhập và mật khẩu là bắt buộc." });
        }

        var normalizedRole = (request.Role ?? "User").Trim();
        if (normalizedRole != "Admin" && normalizedRole != "User")
            return BadRequest(new { message = "Vai trò chỉ được phép là Admin hoặc User." });

        var existing = await _userManager.FindByNameAsync(request.UserName);
        if (existing != null)
        {
            return Conflict(new { message = "Tên đăng nhập đã tồn tại." });
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.UserName : request.DisplayName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        var roleResult = await _userManager.AddToRoleAsync(user, normalizedRole);
        if (!roleResult.Succeeded)
            return Problem("Không thể gán vai trò cho người dùng vừa tạo.", statusCode: StatusCodes.Status500InternalServerError);
        return Ok(new { message = "Tạo người dùng thành công." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new
        {
            user.Id,
            user.UserName,
            user.Email,
            user.DisplayName,
            roles
        });
    }

    [HttpGet("users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _userManager.Users.ToListAsync();
        var result = new List<object>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.DisplayName,
                roles
            });
        }
        return Ok(result);
    }

    private string CreateToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var secret = Encoding.UTF8.GetBytes(_jwt.Key);
        var securityKey = new SymmetricSecurityKey(secret);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public class LoginRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
    }
}
