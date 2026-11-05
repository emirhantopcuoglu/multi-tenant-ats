using Ats.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Ats.Api.IntegrationTests.Configuration;

[Collection(ApiTestCollection.Name)]
public sealed class RequiredSettingsTests
{
    private static readonly string[] CredentialKeyParts = ["Password", "Secret", "AccessKey", "ApiKey"];

    private readonly AtsApiFactory _factory;

    public RequiredSettingsTests(AtsApiFactory factory)
    {
        _factory = factory;
    }

    // appsettings.json is loaded in every environment, so whatever it holds is what Production falls back to.
    [Fact]
    public void the_base_settings_file_should_hold_no_credentials()
    {
        var credentials = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build()
            .AsEnumerable()
            .Where(s => !string.IsNullOrEmpty(s.Value))
            .Where(s => CredentialKeyParts.Any(part => s.Key.Split(':')[^1].Contains(part, StringComparison.OrdinalIgnoreCase))
                || s.Value!.Contains("Password=", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Key);

        Assert.Empty(credentials);
    }

    [Theory]
    [InlineData("Email:Host", "")]
    [InlineData("RabbitMq:Password", "")]
    [InlineData("InterviewRoom:BaseUrl", "/interview-room")]
    [InlineData("InterviewRoom:BaseUrl", "file:///interview-room")]
    public void the_api_should_refuse_to_start_when_a_required_setting_is_missing_or_invalid(string key, string value)
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        var error = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(error);
        Assert.Contains(key, error.ToString());
    }

    [Fact]
    public void the_startup_error_should_name_every_missing_setting_at_once()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Email:Host", "")
            .UseSetting("RabbitMq:Password", ""));

        var error = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(error);
        Assert.Contains("Email:Host", error.ToString());
        Assert.Contains("RabbitMq:Password", error.ToString());
    }
}
