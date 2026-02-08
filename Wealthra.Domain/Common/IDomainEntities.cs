using MediatR; // Nuget: MediatR
using System;
using Wealthra.Domain.Entities;

namespace Wealthra.Domain.Common
{
    public interface IDomainEvent : INotification
    {
        DateTime OccurredOn { get; }
    }
}