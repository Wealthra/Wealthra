using FluentValidation;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Identity.Commands.Login
{
    // DTO
    public record LoginUserCommand(string Email, string Password) : IRequest<string>;

    // Validator
    public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
    {
        public LoginUserCommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty();
        }
    }

    // Handler
    public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, string>
    {
        private readonly IIdentityService _identityService;

        public LoginUserCommandHandler(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        public async Task<string> Handle(LoginUserCommand request, CancellationToken cancellationToken)
        {
            var token = await _identityService.AuthenticateAsync(request.Email, request.Password);

            if (token == null)
            {
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            return token;
        }
    }
}