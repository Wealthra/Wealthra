namespace Wealthra.Application.Common.Interfaces;

public interface IEmailTemplateService
{
    string GenerateForgotPasswordEmail(string code, int expiryMinutes = 15);
}
