namespace Wealthra.Domain.Exceptions
{
    public class UnsupportedBudgetOperationException : DomainException
    {
        public UnsupportedBudgetOperationException(string message)
            : base("Budget Operation Error", message)
        {
        }
    }
}