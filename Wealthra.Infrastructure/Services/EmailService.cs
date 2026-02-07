using System.Threading.Tasks;

namespace Wealthra.Infrastructure.Services
{
    public class EmailService
    {
        public Task SendEmailAsync(string to, string subject, string body)
        {
            // Integration with MailKit goes here later
            return Task.CompletedTask;
        }
    }
}