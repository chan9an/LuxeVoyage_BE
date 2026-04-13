using System.Text;

namespace Notification.Worker;

public static class EmailTemplates
{
    // ── Shared helpers ────────────────────────────────────────────────────────

    private static string Logo() =>
        "<tr><td align=\"center\" style=\"padding:40px 48px 24px;\">" +
        "<p style=\"margin:0;font-size:28px;font-style:italic;letter-spacing:.15em;color:#f2ca50;\">LuxeVoyage</p>" +
        "<p style=\"margin:6px 0 0;font-size:9px;letter-spacing:.45em;text-transform:uppercase;color:#5a4f35;font-family:'Helvetica Neue',sans-serif;\">Private Office</p>" +
        "</td></tr>";

    private static string GoldBar(int height = 4) =>
        $"<tr><td style=\"background:linear-gradient(135deg,#f2ca50,#d4af37);height:{height}px;\"></td></tr>";

    private static string Divider() =>
        "<tr><td style=\"padding:0 48px;\"><div style=\"height:1px;background:linear-gradient(to right,transparent,#3a3020,transparent);\"></div></td></tr>";

    private static string Footer(string note) =>
        "<tr><td align=\"center\" style=\"padding:28px 48px;\">" +
        "<p style=\"margin:0 0 6px;font-size:9px;letter-spacing:.2em;text-transform:uppercase;color:#2e2a1e;font-family:'Helvetica Neue',sans-serif;\">LuxeVoyage Private Office · India</p>" +
        $"<p style=\"margin:0;font-size:10px;color:#2e2a1e;font-family:'Helvetica Neue',sans-serif;\">{note}</p>" +
        "</td></tr>";

    private static string Wrap(string rows) =>
        "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"/><meta name=\"viewport\" content=\"width=device-width,initial-scale=1.0\"/></head>" +
        "<body style=\"margin:0;padding:0;background:#0e0e0e;font-family:'Georgia',serif;\">" +
        "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#0e0e0e;padding:48px 0;\"><tr><td align=\"center\">" +
        "<table width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#131313;border:1px solid #2a2a2a;border-radius:4px;overflow:hidden;\">" +
        rows +
        "</table></td></tr></table></body></html>";

    // ── Welcome email ─────────────────────────────────────────────────────────

    public static string Welcome(string firstName)
    {
        var body =
            GoldBar(4) +
            Logo() +
            Divider() +
            "<tr><td style=\"padding:44px 48px 28px;\">" +
            "<p style=\"margin:0 0 10px;font-size:10px;letter-spacing:.35em;text-transform:uppercase;color:#d4af37;font-family:'Helvetica Neue',sans-serif;\">A Personal Welcome</p>" +
            $"<h1 style=\"margin:0 0 22px;font-size:38px;font-weight:400;font-style:italic;color:#e2e2e2;line-height:1.2;\">Welcome, {firstName}.</h1>" +
            "<p style=\"margin:0 0 18px;font-size:15px;line-height:1.85;color:#9a8f78;font-family:'Helvetica Neue',sans-serif;font-weight:300;\">" +
            "Your membership to the <strong style=\"color:#d4af37;font-weight:400;\">LuxeVoyage Private Office</strong> has been confirmed. " +
            "You now hold exclusive access to our curated collection of India's most extraordinary properties — " +
            "each selected for its architectural significance, impeccable service, and unique sense of place." +
            "</p>" +
            "<p style=\"margin:0;font-size:15px;line-height:1.85;color:#9a8f78;font-family:'Helvetica Neue',sans-serif;font-weight:300;\">" +
            "From palatial heritage hotels in Rajasthan to serene lake resorts in Kerala — your journey begins now." +
            "</p></td></tr>" +
            "<tr><td style=\"padding:0 48px 40px;\">" +
            "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#1a1a1a;border:1px solid #2a2a2a;border-radius:4px;\"><tr><td style=\"padding:28px 32px;\">" +
            "<p style=\"margin:0 0 18px;font-size:9px;letter-spacing:.4em;text-transform:uppercase;color:#d4af37;font-family:'Helvetica Neue',sans-serif;\">Your Privileges</p>" +
            "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\">" +
            "<tr><td style=\"padding:10px 0;border-bottom:1px solid #252525;\"><p style=\"margin:0;font-size:13px;color:#e2e2e2;font-family:'Helvetica Neue',sans-serif;\">&#10022;&nbsp; <span style=\"color:#f2ca50;\">Curated Properties</span> <span style=\"color:#5a4f35;\">&#8212; Luxury verified hotels across India</span></p></td></tr>" +
            "<tr><td style=\"padding:10px 0;border-bottom:1px solid #252525;\"><p style=\"margin:0;font-size:13px;color:#e2e2e2;font-family:'Helvetica Neue',sans-serif;\">&#10022;&nbsp; <span style=\"color:#f2ca50;\">Seamless Reservations</span> <span style=\"color:#5a4f35;\">&#8212; Book with a single click</span></p></td></tr>" +
            "<tr><td style=\"padding:10px 0;\"><p style=\"margin:0;font-size:13px;color:#e2e2e2;font-family:'Helvetica Neue',sans-serif;\">&#10022;&nbsp; <span style=\"color:#f2ca50;\">24/7 Concierge</span> <span style=\"color:#5a4f35;\">&#8212; Your private office, always available</span></p></td></tr>" +
            "</table></td></tr></table></td></tr>" +
            "<tr><td align=\"center\" style=\"padding:0 48px 48px;\">" +
            "<a href=\"http://localhost:4200/hotels\" style=\"display:inline-block;background:linear-gradient(135deg,#f2ca50,#d4af37);color:#1a1200;text-decoration:none;font-size:10px;font-weight:700;letter-spacing:.28em;text-transform:uppercase;padding:16px 44px;border-radius:2px;font-family:'Helvetica Neue',sans-serif;\">Begin Your Journey</a>" +
            "</td></tr>" +
            Divider() +
            Footer("You received this because you created an account at luxevoyage.in") +
            GoldBar(2);

        return Wrap(body);
    }

    // ── Booking confirmation email ─────────────────────────────────────────────

    public static string BookingConfirmation(
        string guestName,
        string hotelName,
        string roomName,
        string location,
        DateTime checkIn,
        DateTime checkOut,
        decimal totalPrice,
        int guestCount,
        int roomsBooked,
        Guid bookingId)
    {
        var nights = Math.Max(1, (checkOut - checkIn).Days);
        var guestLabel = $"{guestCount} guest{(guestCount != 1 ? "s" : "")} &middot; {roomsBooked} room{(roomsBooked != 1 ? "s" : "")}";
        var firstName = guestName.Split(' ')[0];

        string Row(string label, string value) =>
            "<tr>" +
            $"<td style=\"padding:13px 0;border-bottom:1px solid #252525;width:38%;\"><p style=\"margin:0;font-size:10px;letter-spacing:.2em;text-transform:uppercase;color:#5a4f35;font-family:'Helvetica Neue',sans-serif;\">{label}</p></td>" +
            $"<td style=\"padding:13px 0;border-bottom:1px solid #252525;text-align:right;\"><p style=\"margin:0;font-size:13px;color:#e2e2e2;font-family:'Helvetica Neue',sans-serif;\">{value}</p></td>" +
            "</tr>";

        var body = new StringBuilder();
        body.Append(GoldBar(4));
        body.Append(Logo());
        body.Append(Divider());

        // Headline
        body.Append("<tr><td style=\"padding:40px 48px 24px;\">");
        body.Append("<p style=\"margin:0 0 8px;font-size:10px;letter-spacing:.35em;text-transform:uppercase;color:#d4af37;font-family:'Helvetica Neue',sans-serif;\">Reservation Confirmed</p>");
        body.Append($"<h1 style=\"margin:0 0 16px;font-size:34px;font-weight:400;font-style:italic;color:#e2e2e2;line-height:1.2;\">Your stay is secured, {firstName}.</h1>");
        body.Append($"<p style=\"margin:0;font-size:14px;line-height:1.8;color:#9a8f78;font-family:'Helvetica Neue',sans-serif;font-weight:300;\">Your reservation at <strong style=\"color:#d4af37;font-weight:400;\">{hotelName}</strong> has been received and is pending confirmation. Our concierge team will reach out within 24 hours.</p>");
        body.Append("</td></tr>");

        // Details card
        body.Append("<tr><td style=\"padding:0 48px 32px;\">");
        body.Append("<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#1a1a1a;border:1px solid #2a2a2a;border-radius:4px;overflow:hidden;\">");
        body.Append("<tr><td style=\"background:#1f1c14;padding:14px 24px;\"><p style=\"margin:0;font-size:9px;letter-spacing:.4em;text-transform:uppercase;color:#d4af37;font-family:'Helvetica Neue',sans-serif;\">Reservation Details</p></td></tr>");
        body.Append("<tr><td style=\"padding:0 24px;\"><table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\">");
        body.Append(Row("Property", hotelName));
        body.Append(Row("Room", roomName));
        body.Append(Row("Location", location));
        body.Append(Row("Check-in", checkIn.ToString("dddd, dd MMMM yyyy")));
        body.Append(Row("Check-out", checkOut.ToString("dddd, dd MMMM yyyy")));
        body.Append(Row("Duration", $"{nights} night{(nights != 1 ? "s" : "")}"));
        body.Append(Row("Guests", guestLabel));
        // Total row — no border-bottom
        body.Append("<tr>");
        body.Append("<td style=\"padding:16px 0;\"><p style=\"margin:0;font-size:10px;letter-spacing:.2em;text-transform:uppercase;color:#5a4f35;font-family:'Helvetica Neue',sans-serif;\">Total</p></td>");
        body.Append($"<td style=\"padding:16px 0;text-align:right;\"><p style=\"margin:0;font-size:22px;font-weight:700;color:#f2ca50;font-family:'Helvetica Neue',sans-serif;\">&#8377;{totalPrice:N0}</p></td>");
        body.Append("</tr>");
        body.Append("</table></td></tr>");
        body.Append($"<tr><td style=\"background:#111;padding:10px 24px;\"><p style=\"margin:0;font-size:9px;letter-spacing:.15em;color:#3a3020;font-family:'Helvetica Neue',sans-serif;\">Booking Reference: {bookingId.ToString().ToUpper()}</p></td></tr>");
        body.Append("</table></td></tr>");

        // CTA
        body.Append("<tr><td align=\"center\" style=\"padding:0 48px 44px;\">");
        body.Append("<a href=\"http://localhost:4200/my-bookings\" style=\"display:inline-block;background:linear-gradient(135deg,#f2ca50,#d4af37);color:#1a1200;text-decoration:none;font-size:10px;font-weight:700;letter-spacing:.28em;text-transform:uppercase;padding:16px 44px;border-radius:2px;font-family:'Helvetica Neue',sans-serif;\">View My Bookings</a>");
        body.Append("</td></tr>");

        body.Append(Divider());
        body.Append(Footer("Questions? Contact your dedicated concierge at concierge@luxevoyage.in"));
        body.Append(GoldBar(2));

        return Wrap(body.ToString());
    }

    // ── Email Verification OTP ────────────────────────────────────────────────

    public static string EmailVerification(string firstName, string otp) =>
        Wrap(
            GoldBar(4) +
            Logo() +
            Divider() +
            "<tr><td style=\"padding:44px 48px 28px;\">" +
            "<p style=\"margin:0 0 10px;font-size:10px;letter-spacing:.35em;text-transform:uppercase;color:#d4af37;font-family:'Helvetica Neue',sans-serif;\">Verify Your Account</p>" +
            $"<h1 style=\"margin:0 0 22px;font-size:34px;font-weight:400;font-style:italic;color:#e2e2e2;line-height:1.2;\">Welcome, {firstName}.</h1>" +
            "<p style=\"margin:0 0 28px;font-size:15px;line-height:1.85;color:#9a8f78;font-family:'Helvetica Neue',sans-serif;font-weight:300;\">" +
            "Use the code below to verify your email and activate your <strong style=\"color:#d4af37;\">LuxeVoyage Private Office</strong> account. This code expires in <strong style=\"color:#d4af37;\">10 minutes</strong>." +
            "</p>" +
            "<div style=\"background:#1a1a1a;border:1px solid #3a3020;border-radius:4px;padding:28px;text-align:center;margin-bottom:28px;\">" +
            $"<p style=\"margin:0;font-size:42px;font-weight:700;letter-spacing:.3em;color:#f2ca50;font-family:'Helvetica Neue',sans-serif;\">{otp}</p>" +
            "<p style=\"margin:8px 0 0;font-size:10px;letter-spacing:.2em;text-transform:uppercase;color:#5a4f35;font-family:'Helvetica Neue',sans-serif;\">Verification code &middot; valid 10 minutes</p>" +
            "</div>" +
            "<p style=\"margin:0;font-size:13px;line-height:1.7;color:#5a4f35;font-family:'Helvetica Neue',sans-serif;\">" +
            "If you did not create a LuxeVoyage account, please ignore this email." +
            "</p></td></tr>" +
            Divider() +
            Footer("You received this because you registered at luxevoyage.in") +
            GoldBar(2)
        );

    // ── Password Reset OTP email ───────────────────────────────────────────────

    public static string PasswordResetOtp(string firstName, string otp) =>
        Wrap(
            GoldBar(4) +
            Logo() +
            Divider() +
            "<tr><td style=\"padding:44px 48px 28px;\">" +
            "<p style=\"margin:0 0 10px;font-size:10px;letter-spacing:.35em;text-transform:uppercase;color:#d4af37;font-family:'Helvetica Neue',sans-serif;\">Password Reset</p>" +
            $"<h1 style=\"margin:0 0 22px;font-size:34px;font-weight:400;font-style:italic;color:#e2e2e2;line-height:1.2;\">Your reset code, {firstName}.</h1>" +
            "<p style=\"margin:0 0 28px;font-size:15px;line-height:1.85;color:#9a8f78;font-family:'Helvetica Neue',sans-serif;font-weight:300;\">" +
            "Use the code below to reset your LuxeVoyage password. This code expires in <strong style=\"color:#d4af37;\">10 minutes</strong>." +
            "</p>" +
            "<div style=\"background:#1a1a1a;border:1px solid #3a3020;border-radius:4px;padding:28px;text-align:center;margin-bottom:28px;\">" +
            $"<p style=\"margin:0;font-size:42px;font-weight:700;letter-spacing:.3em;color:#f2ca50;font-family:'Helvetica Neue',sans-serif;\">{otp}</p>" +
            "<p style=\"margin:8px 0 0;font-size:10px;letter-spacing:.2em;text-transform:uppercase;color:#5a4f35;font-family:'Helvetica Neue',sans-serif;\">One-time password &middot; valid 10 minutes</p>" +
            "</div>" +
            "<p style=\"margin:0;font-size:13px;line-height:1.7;color:#5a4f35;font-family:'Helvetica Neue',sans-serif;\">" +
            "If you did not request a password reset, please ignore this email. Your account remains secure." +
            "</p></td></tr>" +
            Divider() +
            Footer("You received this because a password reset was requested for your LuxeVoyage account.") +
            GoldBar(2)
        );
}