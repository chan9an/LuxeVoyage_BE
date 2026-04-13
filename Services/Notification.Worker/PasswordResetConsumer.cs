using MassTransit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Shared.Events;

namespace Notification.Worker;

/*
 * This consumer handles IPasswordResetRequestedEvent, which is published by Auth.API when a user
 * submits the forgot-password form. The event carries the pre-generated 6-digit OTP that was already
 * stored as a user claim in the Auth database. The worker's only job here is to format and deliver
 * the email — it has no knowledge of how the OTP was generated or validated. This separation of
 * concerns means the email delivery logic is completely independent of the authentication logic.
 */
public class PasswordResetConsumer : IConsumer<IPasswordResetRequestedEvent>
{
    private readonly IConfiguration _config;
    private readonly ILogger<PasswordResetConsumer> _logger;

    public PasswordResetConsumer(IConfiguration config, ILogger<PasswordResetConsumer> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IPasswordResetRequestedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation("[Notification] Sending OTP to {Email}", evt.Email);

        try
        {
            var smtp = _config.GetSection("Smtp");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(smtp["FromName"], smtp["FromEmail"]));
            message.To.Add(new MailboxAddress(evt.FirstName, evt.Email));
            message.Subject = "Your LuxeVoyage Password Reset Code";

            message.Body = new BodyBuilder
            {
                HtmlBody = EmailTemplates.PasswordResetOtp(evt.FirstName, evt.Otp)
            }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(smtp["Host"], int.Parse(smtp["Port"]!), SecureSocketOptions.SslOnConnect, context.CancellationToken);
            await client.AuthenticateAsync(smtp["Username"], smtp["Password"], context.CancellationToken);
            await client.SendAsync(message, context.CancellationToken);
            await client.DisconnectAsync(true, context.CancellationToken);

            _logger.LogInformation("[Notification] OTP email sent to {Email}", evt.Email);
        }
        catch (Exception ex)
        {
            /*
             * Same exception handling philosophy as the other consumers — we log the failure but
             * don't rethrow. If the email fails to send, the OTP is still stored in the database
             * and the user can request a resend. Letting the exception propagate would cause
             * MassTransit to retry the message, which could result in the user receiving multiple
             * OTP emails if the SMTP server was temporarily slow rather than actually down.
             */
            _logger.LogError(ex, "[Notification] Failed to send OTP to {Email}", evt.Email);
        }
    }
}
