using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Identity.Models;

namespace Wealthra.Application.Features.Identity.Commands.VerifyResetCode;

public record VerifyResetCodeCommand(string Email, string Code) : IRequest<bool>;

public class VerifyResetCodeCommandValidator : AbstractValidator<VerifyResetCodeCommand>
{
    public VerifyResetCodeCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}

public class VerifyResetCodeCommandHandler : IRequestHandler<VerifyResetCodeCommand, bool>
{
    private readonly ICacheService _cacheService;

    public VerifyResetCodeCommandHandler(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task<bool> Handle(VerifyResetCodeCommand request, CancellationToken cancellationToken)
    {
        var cacheEntry = await _cacheService.GetAsync<PasswordResetCacheEntry>($"password-reset:{request.Email.Trim().ToLowerInvariant()}", cancellationToken);
        if (cacheEntry == null || cacheEntry.ExpiresOn < DateTimeOffset.UtcNow)
        {
            return false;
        }

        return string.Equals(cacheEntry.Code, request.Code.Trim(), StringComparison.Ordinal);
    }
}
