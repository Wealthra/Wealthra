using System.Collections.Generic;

namespace Wealthra.Application.Common.Models.Export
{
    public class ExportRequestDto
    {
        public string Title { get; set; } = "Wealthra Report";
        public List<ExportColumnDto> Columns { get; set; } = new();
        public List<Dictionary<string, object>> Data { get; set; } = new();
        public string? Filename { get; set; }
    }

    public class ExportColumnDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
