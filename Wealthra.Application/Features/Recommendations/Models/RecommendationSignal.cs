namespace Wealthra.Application.Features.Recommendations.Models
{
    public class RecommendationSignal
    {
        public string Source { get; set; } = string.Empty;
        public string Severity { get; set; } = "info";
        public string ReasonCode { get; set; } = string.Empty;
        public string Evidence { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }
}
