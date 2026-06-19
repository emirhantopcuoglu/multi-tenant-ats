# One-off migration: activity log PostgreSQL → MongoDB (Sprint 4.1)

Copies existing rows from `applications."Activities"` (PostgreSQL `jsonb`) into the MongoDB
`ats.application_activities` collection, matching the shape `ActivityDocument` expects.

## Ordering (important)

Run this **before** applying the `DropApplicationActivityTable` EF migration — once the table is
dropped there is nothing left to copy.

```
1. Apply earlier migrations / have the Activities table populated.
2. Run this migration script (below).
3. dotnet ef database update   # applies DropApplicationActivityTable, removing the old table.
```

In a fresh dev environment with no real data you can skip straight to step 3; there is nothing to
migrate.

## Why a script and not a code path

The copy is a one-time data move, not application behaviour. Keeping it out of the app avoids
shipping throwaway code that reads a table we are about to delete (dead code the moment it runs).

## Field mapping

| PostgreSQL column        | Mongo field        | Notes                                         |
|--------------------------|--------------------|-----------------------------------------------|
| `Id` (uuid)              | `_id` (string)     | Guids stored as strings (see `ActivityDocument`) |
| `TenantId` (uuid)        | `tenantId`         | preserved — tenant isolation depends on it    |
| `ApplicationId` (uuid)   | `applicationId`    |                                               |
| `ActivityType` (varchar) | `activityType`     |                                               |
| `ActorUserId` (uuid?)    | `actorUserId`      | null for anonymous (Submitted) activities     |
| `Payload` (jsonb)        | `payload`          | embedded as a nested document, not a string   |
| `OccurredAtUtc` (timestamptz) | `occurredAtUtc` | emitted as extended JSON `{"$date": ...}` so it imports as a BSON date |

## Run it (against the docker-compose containers)

```bash
# 1. Export each row as one line of mongoimport-ready (extended) JSON.
docker exec ats-postgres psql -U ats -d ats -At -c "
  SELECT jsonb_build_object(
    '_id',           \"Id\"::text,
    'tenantId',      \"TenantId\"::text,
    'applicationId', \"ApplicationId\"::text,
    'activityType',  \"ActivityType\",
    'actorUserId',   CASE WHEN \"ActorUserId\" IS NULL THEN NULL ELSE \"ActorUserId\"::text END,
    'payload',       \"Payload\",
    'occurredAtUtc', jsonb_build_object(
      '\$date', to_char(\"OccurredAtUtc\" AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.MS\"Z\"'))
  )
  FROM applications.\"Activities\";
" > activities.json

# 2. Load into MongoDB.
docker cp activities.json ats-mongo:/tmp/activities.json
docker exec ats-mongo mongoimport \
  --db ats --collection application_activities \
  --type json --file /tmp/activities.json

# 3. Verify the count matches, then apply the EF migration that drops the table.
docker exec ats-mongo mongosh ats --quiet --eval "db.application_activities.countDocuments()"
```

`mongoimport` uses `_id` as the document key, so re-running the import is idempotent on the same
rows (duplicate `_id`s are rejected, not duplicated).
