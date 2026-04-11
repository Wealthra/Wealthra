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

namespace Wealthra.Api.Controllers
{
    public class AccountController : ApiControllerBase
    {
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<string>> Register(RegisterUserCommand command)
        {
            var userId = await Mediator.Send(command);
            return CreatedAtAction(nameof(Login), new { id = userId }, userId);
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginUserCommand command)
        {
            var response = await Mediator.Send(command);

            SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpiration);

            return Ok(new { response.Id, response.Email, response.Token });
        }

        [AllowAnonymous]
        [HttpPost("refresh-token")]
        public async Task<ActionResult<AuthResponse>> RefreshToken()
        {
            var refreshToken = Request.Cookies["refresh-token"];

            var accessToken = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { message = "Tokens are missing" });
            }

            var command = new RefreshTokenCommand(accessToken, refreshToken);
            var response = await Mediator.Send(command);

            SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpiration);

            return Ok(new { response.Id, response.Email, response.Token });
        }

        [AllowAnonymous]
        [HttpPost("revoke-token")]
        public IActionResult RevokeToken()
        {
            Response.Cookies.Delete("refresh-token");
            return Ok(new { message = "Token revoked" });
        }

        private void SetRefreshTokenCookie(string token, DateTime expires)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = expires,
                Secure = true,
                SameSite = SameSiteMode.Strict
            };

            Response.Cookies.Append("refresh-token", token, cookieOptions);
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> GetMe()
        {
            var response = await Mediator.Send(new GetMyProfileQuery());
            return Ok(response);
        }


        [HttpPut("update-profile")]
        public async Task<ActionResult> UpdateProfile(UpdateUserCommand command)
        {
            await Mediator.Send(command);
            return NoContent();
        }

        [HttpPut("update-password")]
        public async Task<ActionResult> UpdatePassword(UpdatePasswordCommand command)
        {
            await Mediator.Send(command);
            return NoContent();
        }

        [HttpPut("preferred-currency")]
        public async Task<ActionResult> ChangePreferredCurrency([FromBody] ChangePreferredCurrencyCommand command)
        {
            await Mediator.Send(command);
            return NoContent();
        }

        [HttpDelete("me")]
        public async Task<ActionResult> DeleteAccount()
        {
            await Mediator.Send(new Wealthra.Application.Features.Identity.Commands.DeleteAccount.DeleteAccountCommand());
            Response.Cookies.Delete("refresh-token");
            return NoContent();
        }

        // Admin endpoint for now
        [HttpPut("admin/update-tier")]
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
    }
}