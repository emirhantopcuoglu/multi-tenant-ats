using System.Collections.Concurrent;
using Ats.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace Ats.Api.IntegrationTests.Security;

[Collection(ApiTestCollection.Name)]
public sealed class PersonalDataLoggingTests
{
    private const string Email = "selinay.arvasoglu@example.test";
    private const string Phone = "+905550109988";

    private readonly AtsApiFactory _factory;

    public PersonalDataLoggingTests(AtsApiFactory factory) => _factory = factory;

    // Through the app's own Serilog pipeline, so unplugging the masking fails this too. Not through a
    // Microsoft ILogger: that writes to the static Log.Logger, which belongs to whichever test host was
    // built last.
    [Fact]
    public async Task an_email_or_phone_number_logged_by_mistake_should_be_masked()
    {
        var sink = new CapturingSink();
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(sink)));
        var applicationId = Guid.NewGuid();

        factory.Services.GetRequiredService<Serilog.ILogger>().Information(
            "Mailed {CandidateEmail} and texted {PhoneNumber} about application {ApplicationId}",
            Email, Phone, applicationId);

        var message = Assert.Single(sink.Events, e => e.MessageTemplate.Text.StartsWith("Mailed ")).RenderMessage();
        Assert.DoesNotContain(Email, message);
        Assert.DoesNotContain(Phone, message);
        Assert.Contains(applicationId.ToString(), message);
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }
}
