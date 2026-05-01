using FluentValidation;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.Identity.Commands.UpdateUserTier
{
    public class UpdateUserTierCommand : IRequest<Unit>
    {
        public string Email { get; set; } = string.Empty;
        public SubscriptionTier NewTier { get; set; }
    }

    public class UpdateUserTierCommandValidator : AbstractValidator<UpdateUserTierCommand>
    {
        public UpdateUserTierCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("A valid email is required.");

            RuleFor(x => x.NewTier)
                .IsInEnum().WithMessage("Invalid tier specified.");
        }
    }

    public class UpdateUserTierCommandHandler : IRequestHandler<UpdateUserTierCommand, Unit>
    {
        private readonly IIdentityService _identityService;
        private readonly IAdminAuditService _adminAuditService;
        private readonly ICurrentUserService _currentUserService;

        public UpdateUserTierCommandHandler(
            IIdentityService identityService,
            IAdminAuditService adminAuditService,
            ICurrentUserService currentUserService)
        {
            _identityService = identityService;
            _adminAuditService = adminAuditService;
            _currentUserService = currentUserService;
        }

        public async Task<Unit> Handle(UpdateUserTierCommand request, CancellationToken cancellationToken)
        {
            var result = await _identityService.UpdateUserTierAsync(request.Email, request.NewTier);
            if (!result)
            {
                throw new NotFoundException("User", request.Email);
            }

            await _adminAuditService.WriteAsync(
                _currentUserService.UserId ?? "unknown",
                "update_user_tier",
                null,
                new { request.Email, request.NewTier },
                _currentUserService.ClientIpAddress,
                cancellationToken);

            return Unit.Value;
        }
    }
}
