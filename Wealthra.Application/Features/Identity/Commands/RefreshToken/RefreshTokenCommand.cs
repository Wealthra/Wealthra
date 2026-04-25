using FluentValidation;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Identity.Models;
using ValidationException = Wealthra.Application.Common.Exceptions.ValidationException;

namespace Wealthra.Application.Features.Identity.Commands.RefreshToken
{
    public record RefreshTokenCommand(string Token, string RefreshToken) : IRequest<AuthResponse>;

    public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
    {
        public RefreshTokenCommandValidator()
        {
            RuleFor(x => x.Token).NotEmpty();
            RuleFor(x => x.RefreshToken).NotEmpty();
        }
    }

    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
    {
        private readonly IIdentityService _identityService;

        public RefreshTokenCommandHandler(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            var (result, response) = await _identityService.RefreshTokenAsync(request.Token, request.RefreshToken);

            if (!result.Succeeded || response == null)
            {
                // If refresh fails, user must log in again. Return 401/400.
                throw new ValidationException(
                    result.Errors.GroupBy(e => "Token", e => e)
                                 .ToDictionary(g => g.Key, g => g.ToArray()));
            }

            return response!;
        }
    }
}