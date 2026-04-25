namespace Wealthra.Application.Common.Models;

public record ExportFileDto(string FileName, string ContentType, byte[] Content);