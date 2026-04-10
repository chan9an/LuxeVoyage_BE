using MassTransit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Shared.Events;

namespace Notification.Worker;

public class UserRegisteredConsumer : IConsumer<IUserRegisteredEvent>
{
    private readonly IConfiguration _config;
    private readonly ILogger<UserRegisteredConsumer> _logger;

    public UserRegisteredConsumer(IConfiguration config, ILogger<UserRegisteredConsumer> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IUserRegisteredEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation("[Notification] Welcome email triggered for {Email}", evt.Email);

        try
        {
            var smtp = _config.GetSection("Smtp");

            // Build the MIME message
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(smtp["FromName"], smtp["FromEmail"]));
            message.To.Add(new MailboxAddress($"{evt.FirstName} {evt.LastName}".Trim(), evt.Email));
            message.Subject = "Welcome to LuxeVoyage — Your Private Office Awaits";

            message.Body = new BodyBuilder
            {
                HtmlBody = EmailTemplates.Welcome(evt.FirstName)
            }.ToMessageBody();

            // Send via MailKit
            using var client = new SmtpClient();

            await client.ConnectAsync(
                smtp["Host"],
                int.Parse(smtp["Port"]!),
                SecureSocketOptions.SslOnConnect,   // Gmail port 465 — implicit SSL
                context.CancellationToken);

            await client.AuthenticateAsync(
                smtp["Username"],
                smtp["Password"],
                context.CancellationToken);

            await client.SendAsync(message, context.CancellationToken);
            await client.DisconnectAsync(true, context.CancellationToken);

            _logger.LogInformation("[Notification] Welcome email sent to {Email}", evt.Email);
        }
        catch (Exception ex)
        {
            // Log but don't rethrow — a failed email must never block the registration flow
            _logger.LogError(ex, "[Notification] Failed to send welcome email to {Email}", evt.Email);
        }
    }
}
