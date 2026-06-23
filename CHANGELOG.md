# Changelog

## [0.5.0](https://github.com/emirhantopcuoglu/multi-tenant-ats/compare/v0.4.0...v0.5.0) (2026-06-23)


### Features

* **jobs:** clean up expired invitations with a Hangfire recurring job ([1bfc08d](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/1bfc08df63fb46f53027f8a8ae8dac20fdc16ef1))
* **jobs:** clean up expired invitations with a Hangfire recurring job ([bc9fc1d](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/bc9fc1d30ae28ad8f925eb30685570418f68c4c1))
* **messaging:** add consumer retry, dead-letter, and idempotency ([2fe66f7](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/2fe66f7d7033004314421cd88d524d395d2e9f43))
* **messaging:** add consumer retry, dead-letter, and idempotency ([51a5365](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/51a5365e1f9216a7ff217e74ca3bf3bdf255bcf8))
* **messaging:** add RabbitMQ broker and MassTransit bus ([78da0e2](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/78da0e21723fef6a93706705b684d86fc424eb9a))
* **messaging:** add RabbitMQ broker and MassTransit bus ([7d015fa](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/7d015fa04fd9e4386b540ef47cefa8cc0cd85ff3))
* **messaging:** deliver integration events via transactional outbox ([22a7370](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/22a73709c4a8c59262bd0a9bfe386186d9428ef6))
* **messaging:** deliver integration events via transactional outbox ([65208e9](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/65208e96861437c952bb0aba3bf491dc094685f2))
* **notifications:** email candidate when their application is rejected ([02bebe2](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/02bebe2437088a86df4680c5a04ac9169eca04f9))
* **notifications:** email candidate when their application is rejected ([d5de285](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/d5de28550c5b7d4018394f6da2c98f170869e45d))
* **notifications:** send candidate confirmation email via RabbitMQ ([56e7ec5](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/56e7ec5e2a284ed96b8bccc201983f83605e8838))

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
