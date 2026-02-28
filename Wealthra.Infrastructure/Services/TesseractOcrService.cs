using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Infrastructure.Services
{
    public class TesseractOcrService : IOcrService
    {
        private readonly ILogger<TesseractOcrService> _logger;

        public TesseractOcrService(ILogger<TesseractOcrService> logger)
        {
            _logger = logger;
        }

        public async Task<string> ExtractTextAsync(Stream imageStream, string language = "eng", CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting OCR text extraction with language: {Language}", language);

            var tempInputPath = Path.GetTempFileName();
            var tempPreprocessedPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
            var tempOutputPath = Path.GetTempFileName();

            try
            {
                // Write the image stream to disk
                await using (var fileStream = File.Create(tempInputPath))
                {
                    await imageStream.CopyToAsync(fileStream, cancellationToken);
                }

                // Step 1: Preprocess the image with ImageMagick for better OCR accuracy
                await PreprocessImageAsync(tempInputPath, tempPreprocessedPath, cancellationToken);

                // Step 2: Run Tesseract with optimized flags on the preprocessed image
                var text = await RunTesseractAsync(tempPreprocessedPath, tempOutputPath, language, cancellationToken);

                _logger.LogInformation("OCR extraction complete. Text length: {TextLength}", text.Length);
                return text;
            }
            finally
            {
                TryDelete(tempInputPath);
                TryDelete(tempPreprocessedPath);
                TryDelete(tempOutputPath);
                TryDelete(tempOutputPath + ".txt");
            }
        }

        /// <summary>
        /// Preprocesses the image using ImageMagick to improve OCR accuracy.
        /// Pipeline: grayscale → upscale → contrast stretch → binarize → despeckle → sharpen → deskew.
        /// </summary>
        private async Task PreprocessImageAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Preprocessing image with ImageMagick");

            // ImageMagick pipeline optimized for receipt images:
            // -colorspace Gray    : convert to grayscale (removes color noise)
            // -resize 300%        : upscale for better character recognition
            // -contrast-stretch 2%x1% : enhance contrast on faded thermal receipts
            // -threshold 50%      : binarize to clean black-on-white
            // -despeckle          : remove salt-and-pepper noise from phone photos
            // -sharpen 0x1        : sharpen character edges
            // -deskew 40%         : correct slight rotation from handheld photography
            var args = $"\"{inputPath}\" -colorspace Gray -resize 300% -contrast-stretch 2%x1% " +
                       $"-threshold 50% -despeckle -sharpen 0x1 -deskew 40% \"{outputPath}\"";

            var (exitCode, stderr) = await RunProcessAsync("convert", args, cancellationToken);

            if (exitCode != 0)
            {
                _logger.LogWarning(
                    "ImageMagick preprocessing failed (exit code {ExitCode}: {StdErr}). Falling back to raw image.",
                    exitCode, stderr);

                // Fallback: copy the original image so Tesseract still runs
                File.Copy(inputPath, outputPath, overwrite: true);
            }
            else
            {
                _logger.LogInformation("Image preprocessing completed successfully");
            }
        }

        /// <summary>
        /// Runs Tesseract OCR with flags optimized for receipt text extraction.
        /// </summary>
        private async Task<string> RunTesseractAsync(string inputPath, string outputBasePath, string language, CancellationToken cancellationToken)
        {
            // --psm 6 : assume a single uniform block of text (ideal for receipts)
            // --oem 1 : use LSTM neural net engine (more accurate than legacy)
            // --dpi 300: inform Tesseract of the image resolution after preprocessing
            var args = $"\"{inputPath}\" \"{outputBasePath}\" -l {language} --psm 6 --oem 1 --dpi 300";

            var (exitCode, stderr) = await RunProcessAsync("tesseract", args, cancellationToken);

            if (exitCode != 0)
            {
                _logger.LogError("Tesseract CLI failed with exit code {ExitCode}: {StdErr}", exitCode, stderr);
                throw new InvalidOperationException($"Tesseract OCR failed: {stderr}");
            }

            var outputFilePath = outputBasePath + ".txt";
            if (!File.Exists(outputFilePath))
            {
                throw new InvalidOperationException("Tesseract did not produce an output file.");
            }

            return await File.ReadAllTextAsync(outputFilePath, cancellationToken);
        }

        /// <summary>
        /// Runs an external process and returns its exit code and stderr output.
        /// </summary>
        private static async Task<(int ExitCode, string StdErr)> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return (process.ExitCode, stderr);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore cleanup errors */ }
        }
    }
}

