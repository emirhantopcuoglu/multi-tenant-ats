using System.Text.Json;
using Ats.Modules.Notifications.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Notifications.Infrastructure;

public sealed class NotificationCandidateDataExporter : ICandidateDataExporter
{
    private readonly NotificationsDbContext _db;

    public NotificationCandidateDataExporter(NotificationsDbContext db) => _db = db;

    public string Section => "notifications";

    public async Task<object> ExportAsync(Guid candidateAccountId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientType == NotificationRecipientType.Candidate && n.RecipientId == candidateAccountId)
            .OrderBy(n => n.CreatedAtUtc)
            .Select(n => new { n.Type, n.Payload, n.CreatedAtUtc, n.ReadAtUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(n => new
        {
            Type = n.Type.ToString(),
            Payload = JsonSerializer.Deserialize<JsonElement>(n.Payload),
            n.CreatedAtUtc,
            n.ReadAtUtc
        }).ToList();
    }
}
