using Wealthra.Domain.Common;

namespace Wealthra.Domain.Entities
{
    public class FinancialTip : BaseEntity
    {
        public string Topic { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Locale { get; set; } = "tr-TR";
        public string Tags { get; set; } = string.Empty;
    }
}
