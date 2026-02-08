using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Wealthra.Application.Features.Identity.Commands.Login;

namespace Wealthra.Api.Controllers
{
    public class AccountController : ApiControllerBase
    {
        [HttpPost("login")]
        public async Task<ActionResult<string>> Login(LoginUserCommand command)
        {
            // Mediator handles Validation -> Logic -> Response
            var token = await Mediator.Send(command);
            return Ok(new { Token = token });
        }

    }
}