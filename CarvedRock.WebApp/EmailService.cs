using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
namespace CarvedRock.WebApp;

public sealed class EmailService(IConfiguration configuration) : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var connection = configuration.GetConnectionString("smtp")
            ?? throw new InvalidOperationException("SMTP connection is missing. Run the WebApp through Aspire.");
        var address = new Uri(connection.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase)
            ? connection["Endpoint=".Length..].TrimEnd(';') : connection);
        using var smtp = new SmtpClient(address.Host, address.Port > 0 ? address.Port : 25);
        using var message = new MailMessage("e-commerce@carvedrock.com", email, subject, htmlMessage) { IsBodyHtml = true };
        await smtp.SendMailAsync(message);
    }
}
