namespace Wealthra.Application.Features.Recommendations.Models
{
    public class CollaborativeSuggestion
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public float Score { get; set; }
        public string Evidence { get; set; } = string.Empty;
    }
}
