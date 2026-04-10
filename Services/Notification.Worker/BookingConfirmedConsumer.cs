using MassTransit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Shared.Events;

namespace Notification.Worker;

public class BookingConfirmedConsumer : IConsumer<IBookingStartedEvent>
{
    private readonly IConfiguration _config;
    private readonly ILogger<BookingConfirmedConsumer> _logger;

    public BookingConfirmedConsumer(IConfiguration config, ILogger<BookingConfirmedConsumer> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IBookingStartedEvent> context)
    {
        var evt = context.Message;

        if (string.IsNullOrEmpty(evt.GuestEmail))
        {
            _logger.LogWarning("[Notification] Booking {BookingId} has no guest email — skipping", evt.BookingId);
            return;
        }

        _logger.LogInformation("[Notification] Sending booking confirmation to {Email} for booking {BookingId}",
            evt.GuestEmail, evt.BookingId);

        try
        {
            var smtp = _config.GetSection("Smtp");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(smtp["FromName"], smtp["FromEmail"]));
            message.To.Add(new MailboxAddress(evt.GuestName, evt.GuestEmail));
            message.Subject = $"Your LuxeVoyage Reservation — {evt.HotelName}";

            message.Body = new BodyBuilder
            {
                HtmlBody = EmailTemplates.BookingConfirmation(
                    guestName:   evt.GuestName,
                    hotelName:   evt.HotelName,
                    roomName:    evt.RoomName,
                    location:    evt.Location,
                    checkIn:     evt.CheckInDate,
                    checkOut:    evt.CheckOutDate,
                    totalPrice:  evt.TotalPrice,
                    guestCount:  evt.GuestCount,
                    roomsBooked: evt.RoomsBooked,
                    bookingId:   evt.BookingId)
            }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(smtp["Host"], int.Parse(smtp["Port"]!), SecureSocketOptions.SslOnConnect, context.CancellationToken);
            await client.AuthenticateAsync(smtp["Username"], smtp["Password"], context.CancellationToken);
            await client.SendAsync(message, context.CancellationToken);
            await client.DisconnectAsync(true, context.CancellationToken);

            _logger.LogInformation("[Notification] Booking confirmation sent to {Email}", evt.GuestEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Notification] Failed to send booking confirmation to {Email}", evt.GuestEmail);
        }
    }
}
