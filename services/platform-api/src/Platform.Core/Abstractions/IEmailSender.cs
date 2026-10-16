namespace Platform.Core.Abstractions;

public sealed record EmailMessage(string To, string Subject, string Html, string Text);

/// <summary>Outgoing email (SPEC §3.5). The adapter is <c>Platform.Infrastructure/Email/Resend*.cs</c>, plain HTTP, no SDK.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Public URLs of the three apps, used in the links of outgoing emails (env <c>*_APP_URL</c>).</summary>
public sealed record AppLinks(string Admin, string Candidates, string Clients);
