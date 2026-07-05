using System.Text.Json;
using Ats.Modules.Notifications.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Notifications.Application.Notifications;

// The payload goes out as a JSON object (JsonElement), not a string-encoded blob: the client reads
// notification.payload.jobTitle directly instead of parsing a string field. The Type string tells
// it which shape (and which localized template) to expect.
public sealed record NotificationDto(
    Guid Id,
    string Type,
    JsonElement Payload,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

// ---- List ----
// The recipient is always taken from the authenticated caller by the API layer, never from client
// input — which is the whole ownership model: every query in this module filters on
// (RecipientType, RecipientId), so one recipient can never see another's rows.
public sealed record ListNotificationsQuery(
    NotificationRecipientType RecipientType,
    Guid RecipientId,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<NotificationDto>>;

public sealed class ListNotificationsHandler
    : IQueryHandler<ListNotificationsQuery, PagedResult<NotificationDto>>
{
    private readonly INotificationsDbContext _db;

    public ListNotificationsHandler(INotificationsDbContext db) => _db = db;

    public async Task<Result<PagedResult<NotificationDto>>> Handle(
        ListNotificationsQuery query, CancellationToken ct)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var baseQuery = _db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientType == query.RecipientType && n.RecipientId == query.RecipientId);

        var total = await baseQuery.CountAsync(ct);

        var rows = await baseQuery
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new { n.Id, n.Type, n.Payload, n.CreatedAtUtc, n.ReadAtUtc })
            .ToListAsync(ct);

        var items = rows
            .Select(n => new NotificationDto(
                n.Id, n.Type.ToString(), ParsePayload(n.Payload), n.CreatedAtUtc, n.ReadAtUtc))
            .ToList();

        return Result.Success(new PagedResult<NotificationDto>(items, page, pageSize, total));
    }

    // The payload is written by this module's own consumers, so a malformed document should be
    // impossible — but one bad row must degrade to an empty object, not 500 the whole feed.
    private static JsonElement ParsePayload(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var empty = JsonDocument.Parse("{}");
            return empty.RootElement.Clone();
        }
    }
}

// ---- Unread count ----
// The number behind the bell badge. Split from the list query because the client polls this far
// more often than it opens the list, and it must stay a cheap COUNT instead of a page load.
public sealed record GetUnreadNotificationCountQuery(
    NotificationRecipientType RecipientType,
    Guid RecipientId) : IQuery<int>;

public sealed class GetUnreadNotificationCountHandler
    : IQueryHandler<GetUnreadNotificationCountQuery, int>
{
    private readonly INotificationsDbContext _db;

    public GetUnreadNotificationCountHandler(INotificationsDbContext db) => _db = db;

    public async Task<Result<int>> Handle(GetUnreadNotificationCountQuery query, CancellationToken ct)
    {
        var count = await _db.Notifications
            .AsNoTracking()
            .CountAsync(
                n => n.RecipientType == query.RecipientType
                     && n.RecipientId == query.RecipientId
                     && n.ReadAtUtc == null,
                ct);

        return Result.Success(count);
    }
}
