using MediatR;
using FluentValidation;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Identity.Commands.ChangePreferredCurrency
{
    public record ChangePreferredCurrencyCommand(string Currency) : IRequest<Unit>;

    public class ChangePreferredCurrencyCommandValidator : AbstractValidator<ChangePreferredCurrencyCommand>
    {
        public ChangePreferredCurrencyCommandValidator()
        {
            RuleFor(v => v.Currency)
                .NotEmpty()
                .Must(c => c == "TRY" || c == "USD" || c == "EUR")
                .WithMessage("Currency must be TRY, USD, or EUR.");
        }
    }

    public class ChangePreferredCurrencyCommandHandler : IRequestHandler<ChangePreferredCurrencyCommand, Unit>
    {
        private readonly IIdentityService _identityService;
        private readonly ICurrentUserService _currentUserService;

        public ChangePreferredCurrencyCommandHandler(IIdentityService identityService, ICurrentUserService currentUserService)
        {
            _identityService = identityService;
            _currentUserService = currentUserService;
        }

        public async Task<Unit> Handle(ChangePreferredCurrencyCommand request, CancellationToken cancellationToken)
        {
            var result = await _identityService.ChangePreferredCurrencyAsync(_currentUserService.UserId, request.Currency);
            
            if (!result.Succeeded)
            {
                throw new Exception("Failed to change preferred currency: " + string.Join(", ", result.Errors));
            }

            return Unit.Value;
        }
    }
}
