using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Platform.Core.Abstractions;

namespace Platform.Infrastructure.Email;

/// <summary>Env: <c>RESEND_API_KEY</c> (empty locally → <see cref="LogEmailSender"/>), <c>EMAIL_FROM</c> ("Nombre &lt;correo@dominio&gt;").</summary>
public sealed record EmailSettings(string From, string? ResendApiKey);

/// <summary>Resend's REST API (<c>POST /emails</c>) over plain HTTP; swapping providers means replacing this file.</summary>
public sealed class ResendEmailSender(HttpClient http, EmailSettings settings) : IEmailSender
{
    public static readonly Uri BaseAddress = new("https://api.resend.com/");

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = settings.From,
                to = new[] { message.To },
                subject = message.Subject,
                html = message.Html,
                text = message.Text,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ResendApiKey);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new EmailNotSentException($"Resend answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
    }
}

/// <summary>Local development without a Resend key: the message (with its links) goes to the API log instead.</summary>
public sealed partial class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Logged(logger, message.To, message.Subject, message.Text);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email (not sent, RESEND_API_KEY is empty) to {To}: {Subject}\n{Text}")]
    private static partial void Logged(ILogger logger, string to, string subject, string text);
}

public sealed class EmailNotSentException(string message) : Exception(message);
