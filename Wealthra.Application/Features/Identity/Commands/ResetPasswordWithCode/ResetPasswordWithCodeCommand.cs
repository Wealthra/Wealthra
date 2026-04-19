using FluentValidation;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Features.Identity.Models;
using ValidationException = Wealthra.Application.Common.Exceptions.ValidationException;

namespace Wealthra.Application.Features.Identity.Commands.ResetPasswordWithCode;

public record ResetPasswordWithCodeCommand(string Email, string Code, string NewPassword) : IRequest<Result>;

public class ResetPasswordWithCodeCommandValidator : AbstractValidator<ResetPasswordWithCodeCommand>
{
    public ResetPasswordWithCodeCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Code).NotEmpty().Length(6);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public class ResetPasswordWithCodeCommandHandler : IRequestHandler<ResetPasswordWithCodeCommand, Result>
{
    private readonly ICacheService _cacheService;
    private readonly IIdentityService _identityService;
    private readonly IAdminRealtimeService _adminRealtimeService;

    public ResetPasswordWithCodeCommandHandler(
        ICacheService cacheService,
        IIdentityService identityService,
        IAdminRealtimeService adminRealtimeService)
    {
        _cacheService = cacheService;
        _identityService = identityService;
        _adminRealtimeService = adminRealtimeService;
    }

    public async Task<Result> Handle(ResetPasswordWithCodeCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var key = $"password-reset:{email}";
        var cacheEntry = await _cacheService.GetAsync<PasswordResetCacheEntry>(key, cancellationToken);
        if (cacheEntry == null || cacheEntry.ExpiresOn < DateTimeOffset.UtcNow || !string.Equals(cacheEntry.Code, request.Code.Trim(), StringComparison.Ordinal))
        {
            throw new ValidationException(new Dictionary<string, string[]> { { "Code", new[] { "Invalid or expired reset code." } } });
        }

        var result = await _identityService.ResetPasswordWithTokenAsync(cacheEntry.UserId, cacheEntry.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.GroupBy(x => "Password", x => x).ToDictionary(x => x.Key, x => x.ToArray()));
        }

        await _cacheService.RemoveAsync(key, cancellationToken);
        await _adminRealtimeService.PublishActivityAsync("auth.password-reset.completed", $"Password reset completed for {email}.", null, cancellationToken);

        return result;
    }
}
