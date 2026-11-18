using AttractiveCatalog.Api.Domain.Entities;
using AttractiveCatalog.Api.Infrastructure.Config;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace AttractiveCatalog.Api.Infrastructure.Email;

public interface IOrderEmailSender
{
    Task SendAsync(OrderEmailNotification notification, CancellationToken cancellationToken);
}

public sealed class SmtpOrderEmailSender(IOptions<OrderEmailOptions> options) : IOrderEmailSender
{
    public async Task SendAsync(OrderEmailNotification notification, CancellationToken cancellationToken)
    {
        using var message = OrderEmailComposer.Compose(notification, options.Value);
        using var client = new SmtpClient();
        var security = options.Value.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(options.Value.Host, options.Value.Port, security, cancellationToken);
        client.AuthenticationMechanisms.Remove("XOAUTH2");
        await client.AuthenticateAsync(options.Value.Username, options.Value.Password, cancellationToken);
        await client.SendAsync(new SmtpSendRequest(message), cancellationToken);
    }
}
