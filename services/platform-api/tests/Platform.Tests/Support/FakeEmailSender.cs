using System.Collections.Concurrent;
using Platform.Core.Abstractions;

namespace Platform.Tests.Support;

/// <summary>Keeps every email the API sends; <see cref="Fail"/> makes the next sends throw (provider down).</summary>
public sealed class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public bool Fail { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (Fail)
            throw new HttpRequestException("provider down");
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> To(string address) => Sent.Where(m => m.To == address).ToList();
}
