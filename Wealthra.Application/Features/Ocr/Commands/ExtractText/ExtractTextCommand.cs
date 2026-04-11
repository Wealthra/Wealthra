using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Ocr.Commands.ExtractText
{
    // 1. The Command (Input)
    public record ExtractTextCommand : IRequest<ExtractTextResponse>
    {
        public IFormFile Image { get; init; } = null!;
        public string Language { get; init; } = "eng";
    }

    // 2. The Response
    public record ExtractTextResponse
    {
        public string Text { get; init; } = string.Empty;
        public float Confidence { get; init; }
    }

    // 3. The Validator
    public class ExtractTextCommandValidator : AbstractValidator<ExtractTextCommand>
    {
        private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif"];
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public ExtractTextCommandValidator()
        {
            RuleFor(v => v.Image)
                .NotNull().WithMessage("An image file is required.")
                .Must(file => file != null && file.Length > 0).WithMessage("The uploaded file is empty.")
                .Must(file => file != null && file.Length <= MaxFileSizeBytes).WithMessage("File size must not exceed 10 MB.")
                .Must(file =>
                {
                    if (file == null) return false;
                    var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
                    return AllowedExtensions.Contains(extension);
                }).WithMessage($"Only the following file types are allowed: {string.Join(", ", AllowedExtensions)}");

            RuleFor(v => v.Language)
                .NotEmpty().WithMessage("Language code must not be empty.");
        }
    }

    // 4. The Handler
    public class ExtractTextCommandHandler : IRequestHandler<ExtractTextCommand, ExtractTextResponse>
    {
        private readonly IOcrService _ocrService;
        private readonly IUsageTrackerService _usageTrackerService;

        public ExtractTextCommandHandler(IOcrService ocrService, IUsageTrackerService usageTrackerService)
        {
            _ocrService = ocrService;
            _usageTrackerService = usageTrackerService;
        }

        public async Task<ExtractTextResponse> Handle(ExtractTextCommand request, CancellationToken cancellationToken)
        {
            if (!await _usageTrackerService.CanUseOcrAsync(cancellationToken))
            {
                throw new Application.Common.Exceptions.ForbiddenAccessException("OCR quota exceeded or feature not available for your current tier.");
            }

            await using var stream = request.Image.OpenReadStream();

            var text = await _ocrService.ExtractTextAsync(stream, request.Language, cancellationToken);
            await _usageTrackerService.IncrementOcrAsync(cancellationToken);

            return new ExtractTextResponse
            {
                Text = text.Trim(),
                Confidence = 0 // confidence is set by service if supported
            };
        }
    }
}
