using System;

namespace Wealthra.Domain.Common
{
    public interface IDomainEvent
    {
        DateTime OccurredOn { get; }
    }
}