using MassTransit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Shared.Events;

namespace Notification.Worker;

/*
 * This consumer handles the IUserRegisteredEvent, which is published by Auth.API after a user
 * successfully verifies their email address. The separation between registration and this event
 * is intentional — we only send the welcome email to users who have proven they own the email
 * address they registered with. IConsumer<T> is the MassTransit interface that marks this class
 * as a message handler. MassTransit discovers it via the AddConsumer registration in Program.cs
 * and instantiates it through the DI container whenever a matching message arrives on the queue.
 */
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

            /*
             * MimeKit is a third-party library for building RFC-compliant email messages. We use it
             * instead of the built-in System.Net.Mail because it has much better support for HTML
             * emails, attachments, and encoding edge cases. MimeMessage is the top-level email object,
             * and BodyBuilder is a helper that constructs the MIME multipart body structure — it handles
             * the Content-Type headers and boundary markers that email clients need to render HTML correctly.
             */
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(smtp["FromName"], smtp["FromEmail"]));
            message.To.Add(new MailboxAddress($"{evt.FirstName} {evt.LastName}".Trim(), evt.Email));
            message.Subject = "Welcome to LuxeVoyage — Your Private Office Awaits";

            message.Body = new BodyBuilder
            {
                HtmlBody = EmailTemplates.Welcome(evt.FirstName)
            }.ToMessageBody();

            /*
             * MailKit is the SMTP client library that pairs with MimeKit. We use it instead of
             * System.Net.Mail.SmtpClient because it has proper async support, better TLS handling,
             * and is actively maintained. SecureSocketOptions.SslOnConnect tells MailKit to establish
             * an implicit SSL connection immediately on port 465, which is Gmail's preferred mode.
             * The alternative, StartTls on port 587, upgrades an unencrypted connection to TLS after
             * the initial handshake — both work, but SslOnConnect on 465 is more reliable with Gmail.
             */
            using var client = new SmtpClient();
            await client.ConnectAsync(smtp["Host"], int.Parse(smtp["Port"]!), SecureSocketOptions.SslOnConnect, context.CancellationToken);
            await client.AuthenticateAsync(smtp["Username"], smtp["Password"], context.CancellationToken);
            await client.SendAsync(message, context.CancellationToken);
            await client.DisconnectAsync(true, context.CancellationToken);

            _logger.LogInformation("[Notification] Welcome email sent to {Email}", evt.Email);
        }
        catch (Exception ex)
        {
            /*
             * We deliberately catch and swallow exceptions here rather than rethrowing them.
             * If we let an exception propagate out of Consume, MassTransit would treat the message
             * as failed and move it to an error queue, potentially retrying it multiple times.
             * A failed email is not a critical error — the user's account was already created
             * successfully. We log the failure so we can investigate it, but we don't want a
             * transient SMTP issue to create noise in the dead-letter queue.
             */
            _logger.LogError(ex, "[Notification] Failed to send welcome email to {Email}", evt.Email);
        }
    }
}
