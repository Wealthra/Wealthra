using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;
using ValidationException = Wealthra.Application.Common.Exceptions.ValidationException;

namespace Wealthra.Application.Features.Identity.Commands.UpdatePassword
{
    public record UpdatePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<Result>;

    public class UpdatePasswordCommandValidator : AbstractValidator<UpdatePasswordCommand>
    {
        public UpdatePasswordCommandValidator()
        {
            RuleFor(v => v.CurrentPassword).NotEmpty();
            RuleFor(v => v.NewPassword).NotEmpty().MinimumLength(8)
                .NotEqual(v => v.CurrentPassword).WithMessage("New password must be different from current password.");
        }
    }

    public class UpdatePasswordCommandHandler : IRequestHandler<UpdatePasswordCommand, Result>
    {
        private readonly IIdentityService _identityService;
        private readonly ICurrentUserService _currentUserService;

        public UpdatePasswordCommandHandler(IIdentityService identityService, ICurrentUserService currentUserService)
        {
            _identityService = identityService;
            _currentUserService = currentUserService;
        }

        public async Task<Result> Handle(UpdatePasswordCommand request, CancellationToken cancellationToken)
        {
            var result = await _identityService.ChangePasswordAsync(
                _currentUserService.UserId!,
                request.CurrentPassword,
                request.NewPassword);

            if (!result.Succeeded)
            {
                throw new ValidationException(result.Errors.GroupBy(e => "Password", e => e).ToDictionary(g => g.Key, g => g.ToArray()));
            }

            return result;
        }
    }
}
