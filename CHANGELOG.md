# Changelog

## [0.2.0](https://github.com/emirhantopcuoglu/multi-tenant-ats/releases/tag/v0.2.0) (2026-06-14)

Sprints 1 & 2. Future versions are appended above this entry automatically by release-please.

### Features

* **auth/tenancy:** PostgreSQL + EF Core, path-based tenant resolution, ASP.NET Identity + JWT, global query-filter tenant isolation
* **jobs:** domain + lifecycle (draft/publish/close/archive), CQRS endpoints, pagination and filtering, public job listing
* **tenants:** user invitation flow via MailKit/MailHog with hashed single-use tokens
* **auth:** four roles (Admin, Recruiter, HiringManager, ReadOnly) + policy-based authorization
* **shared:** soft delete + audit fields with a combined tenant/soft-delete query filter
