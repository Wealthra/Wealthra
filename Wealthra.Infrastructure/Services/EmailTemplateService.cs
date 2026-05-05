using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Infrastructure.Services;

public class EmailTemplateService : IEmailTemplateService
{
    public string GenerateForgotPasswordEmail(string code, int expiryMinutes = 15)
    {
        return @$"
<div style=""background-color: #f8fafc; padding: 40px; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6;"">
  <div style=""max-width: 600px; margin: 0 auto; background: white; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05);"">
    <div style=""background-color: #0f172a; padding: 32px; text-align: center;"">
      <h1 style=""color: white; margin: 0; font-size: 28px; font-weight: 800; letter-spacing: -0.025em;"">Wealthra</h1>
    </div>
    <div style=""padding: 48px 40px; text-align: center;"">
      <h2 style=""color: #1e293b; margin: 0 0 16px 0; font-size: 24px; font-weight: 700;"">Reset your password</h2>
      <p style=""color: #64748b; margin: 0 0 40px 0; font-size: 16px;"">Use the code below to securely reset your password. This code will expire in <span style=""color: #0f172a; font-weight: 600;"">{expiryMinutes} minutes</span>.</p>
      
      <div style=""background-color: #f1f5f9; padding: 32px; border-radius: 12px; display: inline-block; border: 1px solid #e2e8f0;"">
        <span style=""font-size: 42px; font-weight: 800; letter-spacing: 12px; color: #0f172a; font-family: monospace;"">{code}</span>
      </div>
      
      <div style=""margin-top: 48px; padding-top: 32px; border-top: 1px solid #f1f5f9; text-align: left;"">
        <p style=""color: #94a3b8; font-size: 13px; margin: 0;"">
          <strong>Security Note:</strong> If you did not request a password reset, please ignore this email or contact support if you have concerns. For your security, never share this code with anyone.
        </p>
      </div>
    </div>
    <div style=""background-color: #f8fafc; padding: 24px; text-align: center; border-top: 1px solid #f1f5f9;"">
      <p style=""color: #94a3b8; font-size: 12px; margin: 0;"">
        &copy; {DateTime.UtcNow.Year} Wealthra Inc. All rights reserved.
      </p>
    </div>
  </div>
</div>";
    }
}
