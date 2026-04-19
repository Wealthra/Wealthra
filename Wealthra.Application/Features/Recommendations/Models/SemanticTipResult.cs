namespace Wealthra.Application.Features.Recommendations.Models
{
    public class SemanticTipResult
    {
        public int TipId { get; set; }
        public string Topic { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Locale { get; set; } = "tr-TR";
        public string MatchReason { get; set; } = string.Empty;
    }
}
