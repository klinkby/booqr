using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Infrastructure.MailClient;

internal sealed partial class EmailLabsMailClient(
    [FromKeyedServices(nameof(EmailLabsMailClient))]
    HttpClient httpClient,
    IOptions<InfrastructureSettings> options,
    ILogger<EmailLabsMailClient> logger) : IMailClient
{
    private readonly LoggerMessages _log = new(logger);
    private readonly string _smtpAccount = options.Value.MailClientAccount ?? "";
    private readonly string _fromAddress = options.Value.MailClientFromAddress ?? "";

    public async Task Send(Message message, CancellationToken cancellationToken = default)
    {
        _log.SendEmail(message.To, message.Subject);
        using FormUrlEncodedContent form = new(
            new KeyValuePair<string, string>[]
            {
                new($"to[{message.To}]", ""),
                new("smtp_account", _smtpAccount),
                new("subject", message.Subject),
                new("text", message.Body),
                new("from", _fromAddress)
            }.AsReadOnly());
        // https://dev.emaillabs.io/#api-Send-new_sendmail
        HttpResponseMessage responseMessage = await httpClient.PostAsync(
            new Uri("api/new_sendmail", UriKind.Relative),
            form,
            cancellationToken);
        if (responseMessage.IsSuccessStatusCode)
        {
            _log.SendEmailSuccess();
            return;
        }

        EmailLabsMailClientResponse response =
            await responseMessage.Content.ReadFromJsonAsync(
                MailJsonSerializerContext.Default.EmailLabsMailClientResponse,
                cancellationToken)
            ?? new EmailLabsMailClientResponse((int)responseMessage.StatusCode, "Failed", "General error sending mail");
        try
        {
            responseMessage.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            throw new MailClientException(response.Message, response.Status, response.Code, ex);
        }
    }

    private sealed partial class LoggerMessages(ILogger<EmailLabsMailClient> logger)
    {
        private readonly ILogger<EmailLabsMailClient> _logger = logger;

        [LoggerMessage(1020, LogLevel.Information, "Send email to {To}: {Subject}")]
        public partial void SendEmail(string to, string subject);

        [LoggerMessage(1021, LogLevel.Information, "Email sent successfully")]
        public partial void SendEmailSuccess();
    }

    private sealed record EmailLabsMailClientResponse(
        [property: JsonPropertyName("code")]    int Code,
        [property: JsonPropertyName("status")]  string Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("data")]    object? Data = null);

    [JsonSerializable(typeof(EmailLabsMailClientResponse))]
    private sealed partial class MailJsonSerializerContext : JsonSerializerContext;
}
