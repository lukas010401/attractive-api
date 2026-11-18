using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using AttractiveCatalog.Api.Domain.Entities;
using AttractiveCatalog.Api.Infrastructure.Config;
using MimeKit;

namespace AttractiveCatalog.Api.Infrastructure.Email;

public static class OrderEmailComposer
{
    public static MimeMessage Compose(OrderEmailNotification notification, OrderEmailOptions options)
    {
        var payload = JsonSerializer.Deserialize<OrderEmailPayload>(notification.Payload)
            ?? throw new InvalidOperationException("Invalid order email payload.");
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        string Price(decimal amount) => $"{amount.ToString("N0", culture)} {(payload.Currency == "MGA" ? "Ar" : payload.Currency)}";
        var orderDate = new DateTimeOffset(DateTime.SpecifyKind(payload.CreatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(3));
        var text = new StringBuilder()
            .AppendLine($"Nouvelle commande {payload.Reference}")
            .AppendLine($"Date : {orderDate:dd/MM/yyyy HH:mm} (Madagascar, UTC+3)")
            .AppendLine($"Client : {payload.CustomerName}")
            .AppendLine($"Téléphone : {payload.CustomerPhone}")
            .AppendLine();
        var rows = new StringBuilder();
        foreach (var item in payload.Items)
        {
            text.AppendLine($"{item.Name} × {item.Quantity} — {Price(item.LineTotal)}");
            rows.Append($"<tr><td>{WebUtility.HtmlEncode(item.Name)}</td><td>{item.Quantity}</td><td>{WebUtility.HtmlEncode(Price(item.LineTotal))}</td></tr>");
        }
        text.AppendLine().AppendLine($"Total : {Price(payload.Total)}").AppendLine("Paiement à la livraison.")
            .AppendLine($"Voir la commande : {payload.AdminUrl}")
            .AppendLine("Consultez le backoffice pour connaître le statut actuel de la commande.");

        var message = new MimeMessage
        {
            MessageId = $"order-{notification.Id:N}@attractive-store.com",
            Date = new DateTimeOffset(DateTime.SpecifyKind(payload.CreatedAt, DateTimeKind.Utc)),
            Subject = $"Nouvelle commande {payload.Reference} — {Price(payload.Total)}"
        };
        message.From.Add(new MailboxAddress(options.SenderName, options.Username));
        message.To.Add(MailboxAddress.Parse(notification.Recipient));
        message.Body = new BodyBuilder
        {
            TextBody = text.ToString(),
            HtmlBody = $"""
                <!doctype html><html lang="fr"><body style="font-family:Arial,sans-serif;color:#1d1a18;line-height:1.6">
                <h1>Nouvelle commande {WebUtility.HtmlEncode(payload.Reference)}</h1>
                <p>{orderDate:dd/MM/yyyy HH:mm} — Madagascar (UTC+3)</p>
                <p>Client : {WebUtility.HtmlEncode(payload.CustomerName)}<br>Téléphone : {WebUtility.HtmlEncode(payload.CustomerPhone)}</p>
                <table cellpadding="8"><thead><tr><th>Produit</th><th>Quantité</th><th>Montant</th></tr></thead><tbody>{rows}</tbody></table>
                <p><strong>Total : {WebUtility.HtmlEncode(Price(payload.Total))}</strong><br>Paiement à la livraison.</p>
                <p><a href="{WebUtility.HtmlEncode(payload.AdminUrl)}">Voir la commande dans le backoffice</a></p>
                <p>Connectez-vous au backoffice pour consulter le statut actuel. La commande peut avoir évolué depuis cette notification.</p>
                </body></html>
                """
        }.ToMessageBody();
        return message;
    }
}
