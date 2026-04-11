using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Wealthra.Application.Features.Identity.Commands.Login;
using Wealthra.Application.Features.Identity.Commands.RefreshToken;
using Wealthra.Application.Features.Identity.Commands.Register;
using Wealthra.Application.Features.Identity.Commands.UpdatePassword;
using Wealthra.Application.Features.Identity.Models;
using Wealthra.Application.Features.Identity.Queries.GetMyProfile;
using Wealthra.Application.Features.Identity.Commands.UpdateUser;
using Wealthra.Application.Features.Identity.Commands.ChangePreferredCurrency;
using Wealthra.Application.Features.Identity.Commands.UpdateUserTier;
using Wealthra.Application.Features.Identity.Queries.GetUserUsage;
using Wealthra.Application.Features.Identity.Queries.SearchUserUsages;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Configuration;

namespace Wealthra.Api.Controllers
{
    public class AccountController : ApiControllerBase
    {
        private readonly IAntiforgery _antiforgery;
        private readonly IConfiguration _configuration;

        public AccountController(IAntiforgery antiforgery, IConfiguration configuration)
        {
            _antiforgery = antiforgery;
            _configuration = configuration;
        }

        // ─── CSRF ──────────────────────────────────────────────────────────────
        // The frontend must call this endpoint once on startup (or before any
        // mutating request) to receive the XSRF-TOKEN cookie and the request
        // token it must echo back in the X-XSRF-TOKEN header.
        [AllowAnonymous]
        [HttpGet("csrf-token")]
        public IActionResult GetCsrfToken()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            // Return the request token in the response body as well, so
            // JavaScript SPAs can read it without the cookie overhead.
            return Ok(new { token = tokens.RequestToken });
        }

        // ─── AUTH ──────────────────────────────────────────────────────────────

        [AllowAnonymous]
        [HttpPost("register")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<string>> Register(RegisterUserCommand command)
        {
            var userId = await Mediator.Send(command);
            return CreatedAtAction(nameof(Login), new { id = userId }, userId);
        }

        [AllowAnonymous]
        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginUserCommand command)
        {
            var response = await Mediator.Send(command);

            SetAccessTokenCookie(response.Token);
            SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpiration);

            // Never expose the JWTs in the response body — the frontend only needs identity metadata.
            return Ok(new { response.Id, response.Email });
        }

        [AllowAnonymous]
        [HttpPost("refresh-token")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RefreshToken()
        {
            var refreshToken = Request.Cookies["refresh-token"];
            var accessToken  = Request.Cookies["access-token"];

            if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { message = "Tokens are missing" });
            }

            var refreshCommand = new RefreshTokenCommand(accessToken, refreshToken);
            var response = await Mediator.Send(refreshCommand);

            SetAccessTokenCookie(response.Token);
            SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpiration);

            return Ok(new { response.Id, response.Email });
        }

        [AllowAnonymous]
        [HttpPost("revoke-token")]
        [ValidateAntiForgeryToken]
        public IActionResult RevokeToken()
        {
            Response.Cookies.Delete("access-token");
            Response.Cookies.Delete("refresh-token");
            return Ok(new { message = "Tokens revoked" });
        }

        // ─── PROFILE ───────────────────────────────────────────────────────────

        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> GetMe()
        {
            var response = await Mediator.Send(new GetMyProfileQuery());
            return Ok(response);
        }

        [HttpPut("update-profile")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UpdateProfile(UpdateUserCommand command)
        {
            await Mediator.Send(command);
            return NoContent();
        }

        [HttpPut("update-password")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UpdatePassword(UpdatePasswordCommand command)
        {
            await Mediator.Send(command);
            return NoContent();
        }

        [HttpPut("preferred-currency")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePreferredCurrency([FromBody] ChangePreferredCurrencyCommand command)
        {
            await Mediator.Send(command);
            return NoContent();
        }

        [HttpDelete("me")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteAccount()
        {
            await Mediator.Send(new Wealthra.Application.Features.Identity.Commands.DeleteAccount.DeleteAccountCommand());
            Response.Cookies.Delete("access-token");
            Response.Cookies.Delete("refresh-token");
            return NoContent();
        }

        // ─── ADMIN ─────────────────────────────────────────────────────────────

        [HttpPut("admin/update-tier")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UpdateTier(UpdateUserTierCommand command)
        {
            await Mediator.Send(command);
            return NoContent();
        }

        [HttpGet("me/usage")]
        public async Task<ActionResult<UserUsageDto>> GetMyUsage()
        {
            var response = await Mediator.Send(new GetUserUsageQuery());
            return Ok(response);
        }

        [HttpGet("admin/usages")]
        public async Task<ActionResult<List<UserUsageDto>>> GetUsersUsage([FromQuery] string? email, [FromQuery] string? name)
        {
            var command = new SearchUserUsagesQuery { Email = email, Name = name };
            var response = await Mediator.Send(command);
            return Ok(response);
        }

        // ─── COOKIE HELPERS ────────────────────────────────────────────────────

        private void SetAccessTokenCookie(string token)
        {
            var expiryMinutes = double.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "15");

            var cookieOptions = new CookieOptions
            {
                HttpOnly  = true,
                Secure    = true,
                SameSite  = SameSiteMode.None,
                Path      = "/",
                Expires   = DateTime.UtcNow.AddMinutes(expiryMinutes)
            };

            Response.Cookies.Append("access-token", token, cookieOptions);
        }

        private void SetRefreshTokenCookie(string token, DateTime expires)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure   = true,
                SameSite = SameSiteMode.None,
                // Scope the refresh-token cookie only to the refresh endpoint
                // so it is not sent on every API call.
                Path     = "/api/account/refresh-token",
                Expires  = expires
            };

            Response.Cookies.Append("refresh-token", token, cookieOptions);
        }
    }
}