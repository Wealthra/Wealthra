using System.Threading;
using System.Threading.Tasks;

namespace Wealthra.Application.Common.Interfaces
{
    public interface ICurrencyExchangeService
    {
        Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken cancellationToken = default);
    }
}
