# Changelog

## [0.3.0](https://github.com/emirhantopcuoglu/multi-tenant-ats/compare/v0.2.0...v0.3.0) (2026-06-17)


### Features

* **applications:** add candidate, application and pipeline domain ([657f4a4](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/657f4a4ec8485f5c9417d22d568f8f098f768f42))
* **applications:** add candidate, application and pipeline domain ([b628069](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/b62806920036fcab0201e4ea5b5d4baf49f9a4d3))
* **applications:** add public job application endpoint ([f75f21b](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/f75f21b81151360de46dbf68018d66085a63b78c))
* **applications:** add public job application endpoint ([a55a643](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/a55a64350623e97688f165807bf5ab35cacc37c2))
* **applications:** add recruiter application endpoints ([02d8746](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/02d87468b825f546b8f05082bce7cd1982d825d5))
* **applications:** add recruiter application endpoints ([1ddaf47](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/1ddaf47aa07375669ab192551311030dd5a04ae6))
* **applications:** record an append-only activity log ([1d984f9](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/1d984f99601f02c0cee489b8a4c2d3175791b027))
* **applications:** record an append-only activity log ([5827cc6](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/5827cc6a85428277fe7947f5b7d3323b3232c197))
* **storage:** add MinIO file storage abstraction ([14af147](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/14af147259d2b511f3cd66ff7c3fa1f8124dd2c3))
* **storage:** add MinIO file storage abstraction ([69dd624](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/69dd624e676184686f450b453e10989890998ac2))

## [0.2.0](https://github.com/emirhantopcuoglu/multi-tenant-ats/releases/tag/v0.2.0) (2026-06-14)

Sprints 1 & 2. Future versions are appended above this entry automatically by release-please.

### Features

* **auth/tenancy:** PostgreSQL + EF Core, path-based tenant resolution, ASP.NET Identity + JWT, global query-filter tenant isolation
* **jobs:** domain + lifecycle (draft/publish/close/archive), CQRS endpoints, pagination and filtering, public job listing
* **tenants:** user invitation flow via MailKit/MailHog with hashed single-use tokens
* **auth:** four roles (Admin, Recruiter, HiringManager, ReadOnly) + policy-based authorization
* **shared:** soft delete + audit fields with a combined tenant/soft-delete query filter
