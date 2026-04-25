using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Identity.Models;

namespace Wealthra.Application.Features.Identity.Commands.Login
{
    public record LoginUserCommand(string Email, string Password) : IRequest<AuthResponse>;

    public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
    {
        public LoginUserCommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty();
        }
    }

    public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, AuthResponse>
    {
        private readonly IIdentityService _identityService;

        public LoginUserCommandHandler(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        public async Task<AuthResponse> Handle(LoginUserCommand request, CancellationToken cancellationToken)
        {
            var (result, response) = await _identityService.LoginAsync(request.Email, request.Password);

            if (!result.Succeeded || response == null)
            {
                // Throw Unauthorized to trigger 401
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            return response!;
        }
    }
}