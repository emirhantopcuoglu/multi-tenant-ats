# Ats

A multi-tenant applicant tracking system. Companies post jobs, move applications through a hiring
pipeline and schedule interviews. Candidates use one account to apply to any company and follow
their applications.

Still in development.

## Stack

- .NET 9, ASP.NET Core, EF Core on PostgreSQL
- RabbitMQ (MassTransit), MongoDB, Redis, MinIO, Hangfire
- React 19, Vite and Tailwind in `src/Ats.Web`
- Serilog, OpenTelemetry, Prometheus and Grafana

The backend is a modular monolith. Each module in `src/Modules` (Tenants, Jobs, Applications,
Interviews, CandidateAccounts, Notifications) has its own Domain, Application, Infrastructure and Api
projects and its own database schema. Modules talk through ports in `src/Shared` or through events
on the bus.

## Running locally

You need the .NET 9 SDK, Node 20.19+ and Docker.

Start the infrastructure:

```bash
docker compose up -d
```

Create the certificate the API encrypts its DataProtection keys with (once, pick any password):

```bash
DATA_PROTECTION_CERT_PASSWORD=<password> bash scripts/new-dataprotection-cert.sh
```

Set the API secrets once:

```bash
cd src/Ats.Api
dotnet user-secrets set "Jwt:Secret" "<at least 32 random characters>"
dotnet user-secrets set "DataProtection:CertificatePath" "../../secrets/dataprotection.pfx"
dotnet user-secrets set "DataProtection:CertificatePassword" "<password>"
dotnet user-secrets set "FileStorage:AccessKey" "minioadmin"
dotnet user-secrets set "FileStorage:SecretKey" "minioadmin"
dotnet user-secrets set "Llm:ApiKey" "<GitHub token with Models access>"  # optional, for CV parsing
```

The API doesn't migrate on startup, so apply the migrations first:

```bash
dotnet run --project src/Ats.Api -- migrate
```

Run the API (http://localhost:5236) and the web app (http://localhost:5173):

```bash
dotnet run --project src/Ats.Api
cd src/Ats.Web && npm install && npm run dev
```

Emails go to MailHog at http://localhost:8025.

### Everything in Docker

Create the certificate as above, copy `.env.example` to `.env`, set `JWT_SECRET` and
`DATA_PROTECTION_CERT_PASSWORD`, then:

```bash
docker compose --profile app up -d --build
```

This runs the migrations, then the API on http://localhost:5236 and the web app on
http://localhost:8080.

## Tests

```bash
dotnet test tests/Ats.UnitTests
dotnet test tests/Ats.ArchitectureTests
dotnet test tests/Ats.IntegrationTests   # needs Docker (Testcontainers)
cd src/Ats.Web && npm test
```
