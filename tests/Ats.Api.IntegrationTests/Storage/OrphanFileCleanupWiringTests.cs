using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.Api.IntegrationTests.Storage;

// The cleanup against the app's own registrations and a real MinIO.
[Collection(ApiTestCollection.Name)]
public sealed class OrphanFileCleanupWiringTests
{
    private readonly AtsApiFactory _factory;

    public OrphanFileCleanupWiringTests(AtsApiFactory factory) => _factory = factory;

    // A local time read as UTC would shift every file's age by the machine's offset.
    [Fact]
    public async Task a_listed_file_should_carry_its_upload_time_in_utc()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var key = await UploadAsync(storage);

        var listed = await FindAsync(storage, key);

        Assert.NotNull(listed);
        Assert.InRange(listed.LastModifiedUtc, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
        await storage.DeleteAsync(key);
    }

    [Fact]
    public async Task the_scheduled_job_should_keep_a_file_uploaded_moments_ago()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var key = await UploadAsync(storage);

        await scope.ServiceProvider.GetRequiredService<OrphanFileCleanupJob>().CleanupAsync();

        Assert.NotNull(await FindAsync(storage, key));
        await storage.DeleteAsync(key);
    }

    // A module missing here would have all its files deleted the next night.
    [Fact]
    public void every_module_that_stores_files_should_have_an_owner()
    {
        using var scope = _factory.Services.CreateScope();

        var owners = scope.ServiceProvider.GetServices<IStoredFileOwner>()
            .Select(owner => owner.GetType())
            .OrderBy(type => type.Name);

        Assert.Equal([typeof(ApplicationCvFileOwner), typeof(CandidateAccountCvFileOwner)], owners);
    }

    private static async Task<string> UploadAsync(IFileStorage storage)
    {
        var key = $"{Guid.NewGuid()}/{Guid.NewGuid()}/{Guid.NewGuid()}-cv.pdf";
        using var content = new MemoryStream("%PDF-1.4"u8.ToArray());
        await storage.UploadAsync(key, content, content.Length, "application/pdf");
        return key;
    }

    private static async Task<StoredFile?> FindAsync(IFileStorage storage, string key)
    {
        await foreach (var file in storage.ListAsync())
        {
            if (file.Key == key)
                return file;
        }

        return null;
    }
}
