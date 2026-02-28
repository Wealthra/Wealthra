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

            // Save the uploaded image to a temp file
            var tempInputPath = Path.GetTempFileName();
            var tempOutputPath = Path.GetTempFileName();

            try
            {
                // Write the image stream to disk
                await using (var fileStream = File.Create(tempInputPath))
                {
                    await imageStream.CopyToAsync(fileStream, cancellationToken);
                }

                // Call the tesseract CLI: tesseract <input> <output_base> -l <lang>
                // Tesseract appends ".txt" to the output base path automatically
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "tesseract",
                        Arguments = $"\"{tempInputPath}\" \"{tempOutputPath}\" -l {language}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    _logger.LogError("Tesseract CLI failed with exit code {ExitCode}: {StdErr}", process.ExitCode, stderr);
                    throw new InvalidOperationException($"Tesseract OCR failed: {stderr}");
                }

                // Tesseract writes output to <output_base>.txt
                var outputFilePath = tempOutputPath + ".txt";
                if (!File.Exists(outputFilePath))
                {
                    throw new InvalidOperationException("Tesseract did not produce an output file.");
                }

                var text = await File.ReadAllTextAsync(outputFilePath, cancellationToken);

                _logger.LogInformation("OCR extraction complete. Text length: {TextLength}", text.Length);

                return text;
            }
            finally
            {
                // Clean up temp files
                TryDelete(tempInputPath);
                TryDelete(tempOutputPath);
                TryDelete(tempOutputPath + ".txt");
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore cleanup errors */ }
        }
    }
}
