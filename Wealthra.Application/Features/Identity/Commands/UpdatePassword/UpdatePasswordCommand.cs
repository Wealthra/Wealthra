using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;

namespace Wealthra.Application.Features.Identity.Commands.UpdatePassword
{
    public record UpdateUserCommand(string FirstName, string LastName, string? AvatarUrl): IRequest<Result>;

    public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
    {
        public UpdateUserCommandValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("First name is required.")
                .MaximumLength(50).WithMessage("First name cannot exceed 50 characters.");
            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Last name is required.")
                .MaximumLength(50).WithMessage("Last name cannot exceed 50 characters.");
            RuleFor(x => x.AvatarUrl)
                .Must(uri => string.IsNullOrEmpty(uri) || Uri.IsWellFormedUriString(uri, UriKind.Absolute))
                .WithMessage("Avatar URL must be a valid URL.");
        }
    }


    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result>
    {
        private readonly IIdentityService _identityService;
        private readonly ICurrentUserService _currentUserService;

        public UpdateUserCommandHandler(IIdentityService identityService, ICurrentUserService currentUserService)
        {
            _identityService = identityService;
            _currentUserService = currentUserService;
        }

        public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Result.Failure(new[] { "User not authenticated." });
            var result = await _identityService.UpdateUserAsync(userId, request.FirstName, request.LastName, request.AvatarUrl);
            return result;
        }
    }

}
