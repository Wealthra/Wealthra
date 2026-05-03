using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Common.Interfaces;

/// <summary>
/// Builds per-category monthly metrics in a single target currency (unlike the legacy SQL view, which mixed raw amounts).
/// </summary>
public interface IMonthlyCategoryMetricsCalculator
{
    Task<List<MonthlyCategoryMetric>> ComputeForMonthAsync(
        string userId,
        int year,
        int month,
        string targetCurrency,
        CancellationToken cancellationToken = default);
}
