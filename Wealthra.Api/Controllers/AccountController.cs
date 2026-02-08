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

namespace Wealthra.Api.Controllers
{
    public class AccountController : ApiControllerBase
    {
        [HttpPost("register")]
        public async Task<ActionResult<string>> Register(RegisterUserCommand command)
        {
            var userId = await Mediator.Send(command);
            return CreatedAtAction(nameof(Login), new { id = userId }, userId);
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginUserCommand command)
        {
            var response = await Mediator.Send(command);

            SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpiration);

            return Ok(new { response.Id, response.Email, response.Token });
        }

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

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> GetMe()
        {
            var response = await Mediator.Send(new GetMyProfileQuery());
            return Ok(response);
        }


        [Authorize]
        [HttpPut("update-profile")]
        public async Task<ActionResult> UpdateProfile(UpdateUserCommand command)
        {
            await Mediator.Send(command);
            return NoContent();
        }

        [Authorize]
        [HttpPut("update-password")]
        public async Task<ActionResult> UpdatePassword(UpdatePasswordCommand command)
        {
            await Mediator.Send(command);
            return NoContent();
        }
    }
}