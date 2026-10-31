using System.Globalization;
using System.Net;

namespace PCOBarbershop.Server.Services;

// Continutul HTML al e-mailurilor. Orice text venit de la utilizator trece prin HtmlEncode.
public static class EmailTemplates
{
    private static readonly CultureInfo Ro = CultureInfo.GetCultureInfo("ro-RO");

    public static string ConfirmationCode(string firstName, string code) => Layout(
        "Confirma adresa de e-mail",
        $"""
        <p>Salut, {Encode(firstName)}!</p>
        <p>Codul tau de confirmare pentru contul PCO Barbershop este:</p>
        <p style="font-size:28px;font-weight:bold;letter-spacing:6px;">{Encode(code)}</p>
        <p>Codul este valabil cateva minute. Daca nu ai creat tu acest cont, ignora acest mesaj.</p>
        """);

    public static string AppointmentConfirmed(string firstName, AppointmentInfo a) => Layout(
        "Programare confirmata",
        $"""
        <p>Salut, {Encode(firstName)}!</p>
        <p>Plata a fost primita si programarea ta este <strong>confirmata</strong>:</p>
        {Details(a)}
        <p>Te asteptam!</p>
        """);

    public static string AppointmentCancelled(string firstName, AppointmentInfo a, string? reason, bool refunded) => Layout(
        "Programare anulata",
        $"""
        <p>Salut, {Encode(firstName)}!</p>
        <p>Ne pare rau, programarea de mai jos a fost <strong>anulata</strong> de frizer:</p>
        {Details(a)}
        {(string.IsNullOrWhiteSpace(reason) ? "" : $"<p><strong>Motiv:</strong> {Encode(reason)}</p>")}
        {(refunded ? "<p>Suma platita a fost returnata pe card. Poate dura cateva zile pana apare in cont.</p>" : "")}
        <p>Poti face oricand o noua rezervare pe site.</p>
        """);

    private static string Details(AppointmentInfo a)
    {
        var local = SalonTime.ToLocal(a.StartUtc);
        return $"""
            <table style="border-collapse:collapse;">
              <tr><td style="padding:4px 12px 4px 0;">Serviciu</td><td><strong>{Encode(a.ServiceName)}</strong></td></tr>
              <tr><td style="padding:4px 12px 4px 0;">Frizer</td><td><strong>{Encode(a.BarberName)}</strong></td></tr>
              <tr><td style="padding:4px 12px 4px 0;">Data</td><td><strong>{local.ToString("dddd, dd MMMM yyyy", Ro)}</strong></td></tr>
              <tr><td style="padding:4px 12px 4px 0;">Ora</td><td><strong>{local:HH:mm}</strong></td></tr>
              <tr><td style="padding:4px 12px 4px 0;">Pret</td><td><strong>{a.Price.ToString("0.##", Ro)} RON</strong></td></tr>
            </table>
            """;
    }

    private static string Layout(string title, string content) => $"""
        <!DOCTYPE html>
        <html>
        <body style="font-family:Arial,Helvetica,sans-serif;color:#222;background:#f5f5f5;padding:24px;">
          <div style="max-width:520px;margin:0 auto;background:#fff;border-radius:8px;padding:24px;">
            <h2 style="margin-top:0;">{Encode(title)}</h2>
            {content}
            <hr style="border:none;border-top:1px solid #ddd;margin:24px 0 12px;">
            <p style="font-size:12px;color:#888;">PCO Barbershop &middot; Acest mesaj a fost trimis automat, te rugam sa nu raspunzi.</p>
          </div>
        </body>
        </html>
        """;

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}

public record AppointmentInfo(string ServiceName, string BarberName, DateTime StartUtc, decimal Price);
