# Changelog

## [0.8.0](https://github.com/emirhantopcuoglu/multi-tenant-ats/compare/v0.7.0...v0.8.0) (2026-07-22)


### Features

* **applications:** add candidate application tracking API ([6632186](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/663218617bd877542d8fb98fba9cc06615669659))
* **applications:** add candidate application tracking API ([7954d18](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/7954d187a0b451b5ce79bf4b41384ceba31ac9a4))
* **applications:** auto-advance pipeline stage on interview scheduling ([49648d8](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/49648d8f6b8a1ae7dabc3c47d030a345a4bb4979))
* **applications:** auto-advance pipeline stage on interview scheduling ([33a6d8f](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/33a6d8fc8d2ee6d2ecc83f0ec96f3ed75ab4da4d))
* **applications:** carry the candidate account id on stage-changed events ([8e052d3](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/8e052d32b01c6860e4379271e170255714536de1))
* **applications:** extract text from DOCX CVs ([#104](https://github.com/emirhantopcuoglu/multi-tenant-ats/issues/104)) ([2338bdb](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/2338bdbd3a95318d6f4ba39f068c6dbe44baa9d8))
* **applications:** forward-only stage moves with a correction escape hatch ([1acdebf](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/1acdebf4bc8fb0a59ced0049d4e74110cc2e17b1))
* **applications:** forward-only stage moves with a correction escape hatch ([6006f15](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/6006f157749c1d800eac494d709771f4b7af4a79))
* **applications:** gate terminal stages behind hire and reject decisions ([5279b12](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/5279b129466cdb95eef7afd04f2071d6570ed904))
* **applications:** gate terminal stages behind hire and reject decisions ([9ce45fa](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/9ce45fac3567179b2f1e734206aa75af79b8b93b))
* **applications:** publish stage-changed integration event ([48512df](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/48512df61be7559fb7564e65d15dd16af4a9b839))
* **applications:** validate the apply form on both sides ([#107](https://github.com/emirhantopcuoglu/multi-tenant-ats/issues/107)) ([4769338](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/47693384e27a418353baf6176fcf949707d296fa))
* **candidates:** account freeze and soft delete ([27686df](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/27686df278bdd6a7f06ba724ef21d59fbd5c3be1))
* **candidates:** add a verified email change flow ([89183e8](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/89183e8647e1c8d0dcb7d001406d21f2e3eb6550))
* **candidates:** add candidate profile page and guard candidate routes ([2e0b15b](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/2e0b15bdf6de0e32c5846d767dd07bf5657270b7))
* **candidates:** add candidate profile page and guard candidate routes ([55f9df4](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/55f9df4347fdad3ab9b20954501d18e0e50cde94))
* **candidates:** add freeze, reactivate and delete endpoints ([e81cbd6](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/e81cbd687d909623449bfcd9e95168919ad3bdbd))
* **candidates:** add lifecycle status and soft delete to the account ([319078a](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/319078a47961512f4776209cfac76d69f9d4d858))
* **candidates:** add password change with security stamp ([405a539](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/405a539d4ab80c4642fe6ae7054e976214ef346f))
* **candidates:** add password change with security-stamp revocation ([139ef58](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/139ef58b4057142dd0ad26e68e819e6e1f541e92))
* **candidates:** add phone, residence and birth date to the account ([1779453](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/1779453ab5696bd15284272b4aadd416cc9e655e))
* **candidates:** add profile endpoints behind a dedicated service ([dc552d4](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/dc552d4319a9ac4413e35b1fe1d078eb60603a87))
* **candidates:** add profile field columns to the account schema ([13f787e](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/13f787ed75353f726804e8911a7e16f2cd56ac07))
* **candidates:** add security stamp and password change to the account ([7da1969](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/7da1969e380dcabf41b56851342438d8fdfc3dde))
* **candidates:** add security stamp column to the account schema ([aa08144](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/aa08144bf005dc61d66cb98bff9ec91e4c700b47))
* **candidates:** add the email change request domain and schema ([8920d4f](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/8920d4f2a14cd3a09212ba6ce4177bdcf1a8e5a0))
* **candidates:** candidate profile fields (phone, residence, birth date) ([10fab66](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/10fab662c8c060685a3440a5fd045e2a9e27898f))
* **candidates:** expose the two-phase email change endpoints ([1d025e9](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/1d025e97e0f06bfb588ff65f35f749cb94d149a9))
* **candidates:** show applied state on public job pages ([#106](https://github.com/emirhantopcuoglu/multi-tenant-ats/issues/106)) ([49c47a0](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/49c47a0f5d85bba5219dd79735f0892f2dca3db5))
* **cv-analysis:** judge candidates against the specific job's requirements ([997ec8e](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/997ec8ec763ba534ed046bd1e51a5b0a4790569a))
* **cv-analysis:** judge candidates against the specific job's requirements ([c58eddc](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/c58eddc7e429833d34b3248595ad66b6ba7c6d57))
* **interviews:** carry the candidate account id on interview-scheduled events ([f7a4738](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/f7a4738c2db2ec0601c1c2f02e0d472f9b09b6a7))
* **interviews:** publish interview-scheduled integration event ([05e07fd](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/05e07fdf7f9137787750ddca8c9934da793bce4e))
* **jobs:** add employment, level and location filters to the public feed ([5aaaf9b](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/5aaaf9bf761b114055527c30f2635c470bd2e34a))
* **jobs:** lock city and country to dropdown selection ([249bd70](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/249bd707276fb2acd0adbd1ed6e6b1632a392e62))
* **jobs:** lock city and country to dropdown selection ([fd178ea](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/fd178eac7ae4b2b907580704c4bd71b9676349dd))
* **jobs:** restrict salary currency to a fixed set of codes ([bbc8cbc](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/bbc8cbc019fb021a88449b5368d6a62da100f2b2))
* **jobs:** restrict salary currency to a fixed set of codes ([b79cb9a](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/b79cb9a3ed701afdfa28be163141b4b263abaf70))
* **jobs:** split location into city/country and add work arrangement ([56f9dec](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/56f9decd783da91463f5b8a548a95edb49dc35f8))
* **jobs:** split location into city/country and add work arrangement ([2054a8b](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/2054a8b6f9671c372de9358f8a31dfe03cc8218a))
* **notifications:** add application-viewed in-app notification ([109f267](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/109f2670b59629582f33a82e295a6082527d0896))
* **notifications:** add application-viewed in-app notification ([b4f204c](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/b4f204c02d0350c309eac5fd33d31b4818c0e7ea))
* **notifications:** add company-side new-application notification ([d3369c6](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/d3369c6d3b96c6a3c329befd20b0e83a0d1af0e3))
* **notifications:** add company-side new-application notification ([e570602](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/e5706022452be2746cded4d59745e54f17a87f95))
* **notifications:** add cv-downloaded in-app notification ([b54bcd0](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/b54bcd02ae89cc522ac82a529dfb0e9a6dc8656f))
* **notifications:** add cv-downloaded in-app notification ([e82e88c](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/e82e88cdc27b57536d14a1676403aebb04c58e78))
* **notifications:** add in-app notification store, consumers and candidate API ([0d435ab](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/0d435abfaf93d80a632cdd58061d4d3bbca5361a))
* **notifications:** email candidates on stage change and interview ([72da7fe](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/72da7fee0aab5c76bc4770c73b340f5623a2c3c7))
* **notifications:** email candidates on stage change and interview ([63f1fb1](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/63f1fb131a60a08c0137c98d46842ea50b5ea843))
* **notifications:** in-app notification backbone and candidate API ([5ca502a](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/5ca502a09cb4c1793649801225dce410fb780647))
* **notifications:** publish stage-changed and interview-scheduled events ([4f6973e](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/4f6973effb5bf39b7857d5e6f21a5eac11e0b11e))
* **tenants:** add editable company public profile ([c4f5366](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/c4f5366617f1220ff0866ca54e6ab13f2e06e637))
* **tenants:** add editable company public profile ([ddef1d5](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/ddef1d5fbd61e55111b65c831dc748eeb1156ba0))
* **web:** add account freeze, reactivate and delete flows ([b288c8d](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/b288c8d247e8e3850450159375fb54f948c0781f))
* **web:** add candidate notification bell and page ([50878a1](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/50878a173912af35d1f47ad9d210c02fe36f19ca))
* **web:** add candidate notification bell and page ([b0d35b2](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/b0d35b2479357bf7d70c929becf8770472d755d3))
* **web:** add password change form to the candidate profile page ([78527b3](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/78527b313ecfdb5b8fa2ab41eaf4d8e9e76365cb))
* **web:** add transparent application tracking for candidates ([ea0da7a](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/ea0da7ab91146ce70bf4d87183233b7303dfaf2d))
* **web:** add transparent application tracking for candidates ([033e74d](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/033e74d8f00737272728a4f88824404cb23fe195))
* **web:** collect phone, residence and birth date on the profile page ([765a7fb](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/765a7fb00c8a2665bd94bb7c774f1847006dd018))
* **web:** enrich the public job detail page ([7805a4c](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/7805a4c8c2c397f69df72fc000b2786a6111a86c))
* **web:** enrich the public job detail page ([700038e](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/700038ef66391f037ca5a9c6758c2bb10367997a))
* **web:** land candidate login on the marketplace homepage ([79cd7b3](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/79cd7b325bf21eac82b56016df8eee8cfa8c733f))
* **web:** land candidate login on the marketplace homepage ([fd06af7](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/fd06af7889385dcdae9dd52887bcd6075ea8df45))
* **web:** let candidates change their email with confirmation ([5ce0efd](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/5ce0efdede884bbff0dffa68a875823bb2e67caa))
* **web:** localize pipeline stage names, show interviews on both sides ([eeb1365](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/eeb13656ddbcb6a69cd0b8ba241c1875069f9017))
* **web:** localize pipeline stage names, show interviews on both sides ([e10130c](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/e10130cecd30f6aea7627345c294ba55b353c104))
* **web:** mask the candidate phone input ([6ea8186](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/6ea8186c76577baa753b1f7e23e94b242d19fc00))
* **web:** mask the candidate phone input with per-country formatting ([9f78449](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/9f78449962512a1417edec8fd0e8cfb617d7629b))
* **web:** rebuild the homepage into a full landing page ([d0fb804](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/d0fb8040a62fc5a2cde6ebbf2b238c9919c0be77))
* **web:** rebuild the homepage into a full landing page ([25d6885](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/25d6885bb06fa39295c1c9b4f36deb7766122818))
* **web:** separate candidate and company auth entry points ([#105](https://github.com/emirhantopcuoglu/multi-tenant-ats/issues/105)) ([93362e0](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/93362e0baa13b84365082171825e6cc68351334c))
* **web:** split candidate profile page into a settings area ([3a8ddf3](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/3a8ddf369ede377d4efea54a2fbcbe8beca09fbf))
* **web:** split candidate profile page into a settings area ([96fb77a](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/96fb77a25cb207d5ca249e0ef8ce9cfef961d856))


### Bug Fixes

* **cv-parsing:** tolerate non-numeric strings in LLM CV parse response ([dfb863d](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/dfb863d24e776c11a2a050f2c6fecfcf32f4bbb8))
* **cv-parsing:** tolerate non-numeric strings in LLM CV parse response ([cecb219](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/cecb219cd8379678eac63a4de777b44c188b1a52))
* **cv-parsing:** tolerate quoted numbers in LLM CV parse response ([eb6a708](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/eb6a7084dc45e702cc4dd8b8f4056ec4360e4b68))
* **cv-parsing:** tolerate quoted numbers in LLM CV parse response ([a1c9f27](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/a1c9f27a1130a411a95c5764d4880fc36e08ee1e))
* **web:** avoid excessively-deep i18next type instantiation in stageLabel ([15a8053](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/15a8053e2df00c6e64299531f7829afe590efe5c))
* **web:** narrow JobFormValues.salaryCurrency to the currency union ([4dc7bbb](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/4dc7bbbfe95a379613023263a213c5b971efd192))
* **web:** show login errors instead of redirecting on 401 ([#102](https://github.com/emirhantopcuoglu/multi-tenant-ats/issues/102)) ([1b0f14d](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/1b0f14d32f86f57803498251065368cad7baf661))
* **web:** stop rendering Viewed activity as submitted, unify timeline UI ([ed29206](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/ed292066e29272245b31d0605b47baf0633c69e2))
* **web:** stop rendering Viewed activity as submitted, unify timeline UI ([c8a5489](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/c8a5489a3bda0c9af2665f1521b48539559507be))

## [0.7.0](https://github.com/emirhantopcuoglu/multi-tenant-ats/compare/v0.6.0...v0.7.0) (2026-07-02)


### Features

* **web:** add dual candidate/company auth UI ([#95](https://github.com/emirhantopcuoglu/multi-tenant-ats/issues/95)) ([447f478](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/447f478324f406aaff3b33e3968f9ec0154b0904))

## [0.6.0](https://github.com/emirhantopcuoglu/multi-tenant-ats/compare/v0.5.0...v0.6.0) (2026-06-27)


### Features

* **applications:** add PostgreSQL full-text search for candidates ([db6fcd8](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/db6fcd87791830534d6e890b9802bfaa9b1e44e5))
* **applications:** add PostgreSQL full-text search for candidates ([79e72b5](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/79e72b56f0543b4991e64455ec1bcdac176a0f36))
* **applications:** parse uploaded CVs with Claude into structured data ([a1287ac](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/a1287ac652352f856047cfd3ca32e75132e86959))
* **applications:** parse uploaded CVs with Claude into structured data ([7add52e](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/7add52e7908efcd72e5bce51b4edde13c3e556dd))
* **audit:** add MongoDB audit log via EF Core SaveChangesInterceptor ([09a1b06](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/09a1b0652f2757ce42ed4ded82e802110bc68fff))
* **audit:** add MongoDB audit log via EF Core SaveChangesInterceptor ([fca91a4](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/fca91a47596e77fc82751ea6b1675542287f32ed))
* **interviews:** add interview feedback submission ([e0375c2](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/e0375c2b95d3c6c15e194aee4fbc20d91ddadeaa))
* **interviews:** add interview feedback submission ([46bc76a](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/46bc76ab50d5a92d33a5928ab2f8acf5d1cf5e8b))
* **interviews:** add reschedule, cancel, complete and no-show endpoints ([793e04e](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/793e04e0168877543c262c4cab0975ccaa27ac0a))
* **interviews:** add reschedule, cancel, complete and no-show endpoints ([755de05](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/755de05b2a54b63846dfd89c4c31aca25b4eec20))
* **interviews:** restrict feedback submission to assigned interviewers ([b1621d2](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/b1621d22627ecce2bf1ff6870737825ac8de180a))
* **interviews:** schedule interviews and view them by date or interviewer ([1ef293f](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/1ef293f115b40d8ca81975a3ff3ab270a5813794))
* **interviews:** schedule interviews and view them by date or interviewer ([3d1cb03](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/3d1cb035fecfff86b603ce0953197ffd4c907e8e))
* **observability:** add Serilog structured logging with Seq sink ([f341395](https://github.com/emirhantopcuoglu/multi-tenant-ats/commit/f3413956f9bdf8949ac73220dd8a11353caf0d61))

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
