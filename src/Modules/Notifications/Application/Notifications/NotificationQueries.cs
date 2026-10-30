using System.Text.Json;
using Ats.Modules.Notifications.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Notifications.Application.Notifications;

// Payload goes out as a JSON object; Type tells the client which shape and template to use.
public sealed record NotificationDto(
    Guid Id,
    string Type,
    JsonElement Payload,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

// Every query filters on (RecipientType, RecipientId), set from the caller by the API.
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

    // A malformed payload becomes an empty object instead of failing the whole feed.
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
