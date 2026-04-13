using MassTransit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Shared.Events;

namespace Notification.Worker;

public class EmailVerificationConsumer : IConsumer<IEmailVerificationRequestedEvent>
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailVerificationConsumer> _logger;

    public EmailVerificationConsumer(IConfiguration config, ILogger<EmailVerificationConsumer> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IEmailVerificationRequestedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation("[Notification] Sending verification OTP to {Email}", evt.Email);

        try
        {
            var smtp = _config.GetSection("Smtp");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(smtp["FromName"], smtp["FromEmail"]));
            message.To.Add(new MailboxAddress(evt.FirstName, evt.Email));
            message.Subject = "Verify Your LuxeVoyage Account";

            message.Body = new BodyBuilder
            {
                HtmlBody = EmailTemplates.EmailVerification(evt.FirstName, evt.Otp)
            }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(smtp["Host"], int.Parse(smtp["Port"]!), SecureSocketOptions.SslOnConnect, context.CancellationToken);
            await client.AuthenticateAsync(smtp["Username"], smtp["Password"], context.CancellationToken);
            await client.SendAsync(message, context.CancellationToken);
            await client.DisconnectAsync(true, context.CancellationToken);

            _logger.LogInformation("[Notification] Verification OTP sent to {Email}", evt.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Notification] Failed to send verification OTP to {Email}", evt.Email);
        }
    }
}
