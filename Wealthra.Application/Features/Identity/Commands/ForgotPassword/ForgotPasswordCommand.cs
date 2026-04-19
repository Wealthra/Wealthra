using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Identity.Models;

namespace Wealthra.Application.Features.Identity.Commands.ForgotPassword;

public record ForgotPasswordCommand(string Email) : IRequest<Unit>;

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Unit>
{
    private readonly IIdentityService _identityService;
    private readonly ICacheService _cacheService;
    private readonly IEmailSender _emailSender;
    private readonly IAdminRealtimeService _adminRealtimeService;

    public ForgotPasswordCommandHandler(
        IIdentityService identityService,
        ICacheService cacheService,
        IEmailSender emailSender,
        IAdminRealtimeService adminRealtimeService)
    {
        _identityService = identityService;
        _cacheService = cacheService;
        _emailSender = emailSender;
        _adminRealtimeService = adminRealtimeService;
    }

    public async Task<Unit> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var cooldownKey = $"password-reset-cooldown:{normalizedEmail}";
        var cooldown = await _cacheService.GetAsync<PasswordResetCacheEntry>(cooldownKey, cancellationToken);
        if (cooldown != null)
        {
            return Unit.Value;
        }

        var resetTokenResult = await _identityService.GeneratePasswordResetTokenAsync(normalizedEmail);
        if (!resetTokenResult.Success)
        {
            return Unit.Value;
        }

        var code = Random.Shared.Next(100000, 999999).ToString();
        var cacheEntry = new PasswordResetCacheEntry
        {
            UserId = resetTokenResult.UserId,
            Token = resetTokenResult.Token,
            Code = code,
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(15)
        };

        await _cacheService.SetAsync($"password-reset:{normalizedEmail}", cacheEntry, TimeSpan.FromMinutes(15), cancellationToken);
        await _cacheService.SetAsync(cooldownKey, cacheEntry, TimeSpan.FromMinutes(1), cancellationToken);

        var body = $"<p>Your Wealthra password reset code is: <strong>{code}</strong></p><p>This code expires in 15 minutes.</p>";
        await _emailSender.SendEmailAsync(normalizedEmail, "Wealthra password reset code", body, cancellationToken);
        await _adminRealtimeService.PublishActivityAsync("auth.password-reset.requested", $"Password reset requested for {normalizedEmail}.", null, cancellationToken);

        return Unit.Value;
    }
}
