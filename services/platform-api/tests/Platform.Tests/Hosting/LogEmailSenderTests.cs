using Microsoft.Extensions.Logging;
using Platform.Core.Abstractions;
using Platform.Infrastructure.Email;

namespace Platform.Tests.Hosting;

/// <summary>Without a Resend key the email goes to the log; the body (sign-in and password-setup links) only in Development.</summary>
public sealed class LogEmailSenderTests
{
    private const string Link = "https://admin.example.test/setup?token=SECRET-LINK";
    private static readonly EmailMessage Message = new("ana@example.test", "Tu acceso", $"<a href=\"{Link}\">x</a>", $"Entra aquí: {Link}");

    [Fact]
    public async Task Development_logs_the_body_so_the_link_can_be_followed_locally()
    {
        var log = new CapturingLogger();
        await new LogEmailSender(log, includeBody: true).SendAsync(Message, CancellationToken.None);

        var line = Assert.Single(log.Lines);
        Assert.Contains(Link, line);
        Assert.Contains("ana@example.test", line);
    }

    [Fact]
    public async Task Elsewhere_logs_recipient_and_subject_but_never_the_body()
    {
        var log = new CapturingLogger();
        await new LogEmailSender(log, includeBody: false).SendAsync(Message, CancellationToken.None);

        var line = Assert.Single(log.Lines);
        Assert.Contains("ana@example.test", line);
        Assert.Contains("Tu acceso", line);
        Assert.DoesNotContain("SECRET-LINK", line);
    }

    private sealed class CapturingLogger : ILogger<LogEmailSender>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }
}
