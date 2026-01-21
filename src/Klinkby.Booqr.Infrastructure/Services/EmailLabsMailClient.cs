using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Klinkby.Booqr.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Infrastructure.Services;

internal sealed partial class EmailLabsMailClient : IMailClient
{
    private readonly LoggerMessages _log;
    private readonly string _smtpAccount;
    private readonly string _fromAddress;
    private readonly HttpClient _httpClient;

    public EmailLabsMailClient([FromKeyedServices(nameof(EmailLabsMailClient))]
        HttpClient httpClient,
        IOptions<InfrastructureSettings> options,
        ILogger<EmailLabsMailClient> logger)
    {
        InfrastructureSettings optionsValue = options.Value;
        _httpClient = httpClient;
        _log = new LoggerMessages(logger);
        _smtpAccount = optionsValue.MailClientAccount;
        _fromAddress = optionsValue.MailClientFromAddress;
        _log.EmailLabsClient(_smtpAccount, _fromAddress);
    }

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
        var response = EmailLabsMailClientResponse.FromGeneralNetworkError();
        try
        {
            // https://dev.emaillabs.io/#api-Send-new_sendmail
            HttpResponseMessage responseMessage = await _httpClient.PostAsync(
                new Uri("api/new_sendmail", UriKind.Relative),
                form,
                cancellationToken); // throws if there's no connection

            if (responseMessage.IsSuccessStatusCode)
            {
                _log.SendEmailSuccess();
                return;
            }

            response = await DeserializeFailureResponse(responseMessage, cancellationToken);

            responseMessage.EnsureSuccessStatusCode(); // throws HttpRequestException
        }
        catch (HttpRequestException ex)
        {
            throw new MailClientException(response.Message, response.Status, response.Code, ex);
        }
    }

    private static async Task<EmailLabsMailClientResponse> DeserializeFailureResponse(HttpResponseMessage responseMessage,
        CancellationToken cancellationToken)
    {
        EmailLabsMailClientResponse response =
            await responseMessage.Content.ReadFromJsonAsync(
                   MailJsonSerializerContext.Default.EmailLabsMailClientResponse,
                   cancellationToken)
               ?? EmailLabsMailClientResponse.FromGeneralServerError(responseMessage.StatusCode);
        return response;
    }

    private sealed partial class LoggerMessages(ILogger<EmailLabsMailClient> logger)
    {
        [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Ref by SG")]
        private readonly ILogger<EmailLabsMailClient> _logger = logger;

        [LoggerMessage(1020, LogLevel.Information, "Send email to {To}: {Subject}")]
        public partial void SendEmail(string to, string subject);

        [LoggerMessage(1021, LogLevel.Information, "Email sent successfully")]
        public partial void SendEmailSuccess();

        [LoggerMessage(1022, LogLevel.Information, "EmailLabs sending through {SmtpAccount} from {FromAddress}")]
        public partial void EmailLabsClient(string smtpAccount, string fromAddress);
    }

    private sealed record EmailLabsMailClientResponse(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("message")]
        string Message,
        [property: JsonPropertyName("data")] object? Data = null)
    {
        internal static EmailLabsMailClientResponse FromGeneralNetworkError() =>
            new(0, "NetworkError", "General network error");

        internal static EmailLabsMailClientResponse FromGeneralServerError(HttpStatusCode statusCode) =>
            new((int)statusCode, "ServerError", $"General server error: {statusCode}");
    }

    [JsonSerializable(typeof(EmailLabsMailClientResponse))]
    private sealed partial class MailJsonSerializerContext : JsonSerializerContext;
}
