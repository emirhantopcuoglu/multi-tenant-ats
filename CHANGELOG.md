# Changelog

## [0.4.0](https://github.com/emirhantopcuoglu/multi-tenant-ats/compare/v0.3.0...v0.4.0) (2026-06-19)


### Features

* **api:** add distributed rate limiting backed by Redis ([4d07106](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/4d07106fc8f7a3845c87ac16be6ab7b3cc6e91f2))
* **api:** add distributed rate limiting backed by Redis ([0592f47](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/0592f47434e1c70ce6ea7d50d6bbe260395ee760))
* **applications:** index MongoDB activity log for tenant-scoped reads ([e0438f3](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/e0438f3b0380e5c2a95fe0dc2743b55f496cc4ba))
* **applications:** index MongoDB activity log for tenant-scoped reads ([edf454c](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/edf454c19dda0e2fa0bcaed6e803a96c89ea7b2d))
* **tenants:** cache slug-to-tenant lookup in Redis ([ea85b7d](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/ea85b7df41b417bd503e8578aceae8cced0af790))
* **tenants:** cache slug-to-tenant lookup in Redis ([e6c4876](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/e6c487619b8fb69e815f0084d421dbe85a2592d1))


### Performance Improvements

* **db:** index public job and application listing queries ([c7fd795](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/c7fd795101dcaf023da3da66c69f0011b39ca612))
* **db:** index public job and application listing queries ([4514f8c](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/4514f8c91d1763f629be946d00df126b066612c5))

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
