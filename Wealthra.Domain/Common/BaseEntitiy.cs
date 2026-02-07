using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema; // Only for [NotMapped], strictly speaking we can avoid this using FluentAPI, but this is acceptable for simplicity.

namespace Wealthra.Domain.Common
{
    public abstract class BaseEntity
    {
        public int Id { get; protected set; }

        private readonly List<IDomainEvent> _domainEvents = new();

        // NotMapped prevents EF Core from trying to create a table column for this
        [NotMapped]
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        public void AddDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void RemoveDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Remove(domainEvent);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }
    }
}