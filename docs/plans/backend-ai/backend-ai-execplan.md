# Connect Uni Companion to .NET and grounded AI

## Purpose / Big Picture

Turn the existing polished mobile prototype into a working university companion: students authenticate, join courses, read real materials, ask questions with page citations, study summaries and quizzes, and participate in course communities. Preserve the current visual design. Build for one university and one developer, without a fixed deadline.

Implementation authorized by the user on 2026-10-21: start building in the current checkout. Continue reversible local implementation and validation. Deployment, publishing, and GitHub pushes remain outside scope.

## Progress

- [x] Confirm one repository, with separate mobile/ and backend/ directories, using the current checkout.
- [x] Inventory existing mobile prototype and imported content.
- [x] Save the supplied helper unchanged at backend/ai/gemini.py; inspect syntax and configuration offline.
- [x] Write implementation sequence, security boundaries, validation strategy, and proposed objective.
- [x] Phase 0: .NET 10.0.402 installed; PostgreSQL/pgvector available; exact-helper live JSON probe passed.
- [x] Phase 1: API/Core/Infrastructure foundation, migration, local runtime, real PostgreSQL harness, check commands.
- [ ] Phase 2: backend accounts and authorization implemented/tested; mobile secure-token client remains pending.
- [ ] Phase 3: 12 courses/35 materials imported, idempotence and member-only downloads tested; uploads and mobile reader remain pending.
- [ ] Phase 4: 27 PDFs/398 pages indexed into 814 passages; 10/12 expected-source recall@5 achieved; durable worker recovery and complete adversarial/answer-quality checks remain pending.
- [ ] Phase 5: implement cited answers, summaries, quizzes, and attempts.
- [ ] Phase 6: implement community persistence and moderation.
- [ ] Phase 7: connect mobile and prove complete phone flows.

## Context and Orientation

Workspace: /home/lenovo/projects/mobile-project. Posture: current-checkout, main, no commits yet; existing related files are untracked. Preserve them. Remote: AdamNgazzou/uni-companion. No worktree is needed. There is no TeamBoost ticket or repository-specific backend delivery workflow.

First reads: PRODUCT.md, DESIGN.md, mobile/AGENTS.md, Makefile, docs/plans/mobile-prototype/mobile-prototype-execplan.md, and docs/research/horizon-lms-api.json. Before editing mobile, inspect its actual package scripts and existing navigation; resolve the local Expo Router guidance against the existing React Navigation implementation explicitly rather than silently migrating navigation.

Current mobile is Expo SDK 57, React Native, with local demo auth/data; its approved design remains unchanged. The root Makefile now validates backend and mobile. .NET SDK 10.0.402 is installed in ~/.dotnet; Python dependencies are pinned in backend/ai/requirements.lock and installed in backend/.venv. Docker/pgvector and the local SMTP inbox have been validated.

Content input is the ignored data/university-import/catalogue.json plus its files: 12 courses, 35 attachments (27 PDFs, 3 CSVs, XML/XSD/XSL, 2 Python files), 398 PDF pages, approximately 21 MB. All PDF pages yielded text; layout, tables, and retrieval quality are not yet evaluated. Source identifiers, ordering, outcomes, file hashes, and sanitized provenance are retained. Never execute imported Python files.

The LMS hierarchy is course -> module -> section/lesson -> component -> attachment. Imported data is a local snapshot, so implementation does not depend on the old LMS login token. Signed Firebase download URLs working without a bearer header do not imply the storage bucket is public. Never commit university credentials, access URL tokens, raw documents, or personal progress.

## Decision Log

Locked by the user:

- One repository: mobile/ and backend/; current checkout, no separate worktree.
- .NET backend, existing React Native mobile, one university, open registration initially (joining assigns that university; no enrollment verification in v1).
- Admin-curated university courses plus visibly student-authored courses; admin recognition preserves original authorship. Recognition means app-admin reviewed, not university endorsement.
- Use exactly backend/ai/gemini.py as supplied: empty cookie, same endpoint/protocol/timeout, no added API key, cookie, or upstream session.
- No emulator; final interactive testing uses the user's phone and Expo Go.

Recommended architecture for implementation:

- .NET 10 LTS, ASP.NET Core, EF Core, PostgreSQL with pgvector, one modular monolith and a background worker sharing application code. Pin compatible stable package versions during setup. No microservices, message broker, Redis, or mandatory mediator/generic repository framework.
- Feature-oriented endpoints and use cases, explicit DTOs, EF mappings, and small interfaces at actual external boundaries. Business rules do not depend on HTTP, EF, or Python.
- ASP.NET Core Identity's supported first-party bearer-token flow for the native app. These are opaque protected tokens, not custom JWTs or an OAuth/OIDC server. Future third-party login is separate scope.
- Private local file storage for development, addressed by server-generated identifiers; keep a storage interface for later object storage. Do not expose raw filesystem paths or a public static uploads directory.
- Local multilingual embeddings for retrieval; the supplied Gemini helper handles generation only. No additional AI API credential is needed for a downloaded local embedding model. Benchmark model quality and memory before locking the model.

Sources: [.NET support](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [Identity API bearer authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0), [PostgreSQL full-text indexes](https://www.postgresql.org/docs/current/textsearch-indexes.html), [multilingual sentence-transformer models](https://sbert.net/docs/sentence_transformer/pretrained_models.html).

## Proposed Folder Structure

API, Core, Infrastructure, Python adapters and tests now exist. Worker and generative study modules remain planned.

```text
mobile/                            # existing Expo app
backend/
  UniCompanion.sln
  src/
    UniCompanion.Api/              # HTTP, DTOs, policies, composition
      Features/{Accounts,Courses,Materials,Study,Community}/
    UniCompanion.Core/             # entities, use cases, external ports
      {Accounts,Courses,Materials,Study,Community}/
    UniCompanion.Infrastructure/   # Identity, EF, storage, AI bridge
      {Identity,Persistence,Storage,Ai}/
    UniCompanion.Worker/            # durable import and AI jobs
  ai/
    gemini.py                      # supplied file, unchanged
    bridge.py                      # future framed JSON stdin/stdout
    extraction.py
    embeddings.py
    prompts/
  tests/
    UniCompanion.UnitTests/
    UniCompanion.IntegrationTests/
    python/
  compose.yaml                     # local PostgreSQL; optional mail sink
  .env.example                     # names/placeholders only
  README.md                        # setup, run, admin bootstrap, checks
docs/plans/backend-ai/             # plan and continuation objective
```

Api and Worker use Core and Infrastructure; Infrastructure implements Core ports; Core has no infrastructure dependencies. Both processes use one database. Keep file/runtime/model caches out of git. Add projects only when the next phase needs them.

## Permissions and Authentication

Use Identity password hashing, email confirmation, password reset, lockout, and rate-limited account endpoints. Local email testing uses a development mail sink; actual delivery requires a configured sender before a production claim. Return non-enumerating reset/registration responses. Require confirmed accounts for protected features.

Use short access tokens (initial target 5 minutes) and expiring refresh tokens (initial target 14 days). Mobile keeps access tokens in memory and refresh tokens in Expo SecureStore, never AsyncStorage. Persist ASP.NET Data Protection keys privately so restarts do not unexpectedly invalidate tokens. Keep HTTPS and trusted development transport documented for a real phone.

Built-in Identity refresh tokens are not a claim of single-use rotation or per-device session management. V1 logout clears device credentials; provide logout-all by changing the account security stamp. Validate current account status/security stamp against the access-token identity on protected requests and test that logout-all, password changes, account disablement, and role changes invalidate both access and refresh paths. Reissue current claims only after validation. Do not hand-roll JWT signing to fill this gap.

Bootstrap the first admin through a local administrative command. Registration cannot choose roles or recognized status. Remove the prototype admin switch and fake login from connected flows. The app's own login tokens are unrelated to the prohibition on adding credentials to the Gemini helper.

Course catalogue and course creation require an authenticated account. Joining a course grants access to that course's shared materials/community; owner-only uploads remain private. Admin imports create curated courses. Student courses retain creator identity; only admin can recognize them. Course owners can manage their own course content; public requests cannot supply an arbitrary owner ID.

Bound upload size, verify supported file signatures, sanitize display names, and generate storage paths on the server. PDFs/documents are data; prevent path traversal and never execute attachments. Authorize every material download, retrieval query, AI job/result, quiz attempt, edit, and delete on the server. Filter eligible material versions before retrieval and enforce the same rules again on returned citation IDs. Anonymous posts hide public author identifiers and profile links in every public DTO. The server retains ownership for edits, rate limits, reports, and admin moderation. State this clearly in the posting UI. No promise of anonymity from the administrator.

## Interfaces and Data Model

Use /api/v1 routes, camelCase DTOs, UTC timestamps, explicit bounded pagination, validation errors using ProblemDetails, request IDs, and cancellation. Publish OpenAPI and consume one typed mobile client. Never expose EF entities directly.

Core records: User, Course, CourseMembership, Module, Lesson, LearningOutcome, Material, MaterialVersion, DocumentChunk, StudyJob, Summary, Quiz, Question, QuizAttempt, Thread, Comment, Vote, Report, Bookmark. Preserve upstream source IDs and source kind separately from local primary keys. Scope uniqueness constraints to their owning resource. Quiz correct answers stay server-side until submission; grading and scores are server-authoritative.

Representative contracts: accounts login/refresh/logout-all; courses join and recognize; materials upload/content; study jobs create/status/result; course chat; quiz attempts submit; threads/comments/votes/reports. Use idempotency keys for generation and attempt submission. An upvote/downvote is one transactional vote per user and target; clicking the same vote removes it, switching changes its value.

## AI Boundary and Recovery

.NET owns permissions, retrieval, storage, jobs, grading, and public errors. Python extracts pages, embeds text, and invokes the supplied generation helper. A private subprocess bridge receives bounded JSON through stdin (never prompts in shell arguments), calls gemini.ask_raw, and emits framed typed JSON. Fixed interpreter/script paths, no shell, bounded output, cancellation, and timeout/termination handling are required. Start with one worker and one generation at a time to respect RAM and avoid uncontrolled upstream requests.

Preserve helper bytes; SHA-256 at planning: cc6120d2bec42986ec13be5b92655bedcd35bf23ccade0f9a80b29ead8942630. It is excluded from automatic rewriting/formatting. Put resilience and schema handling outside it. Redirect optional helper debug output away from bridge stdout without changing the helper. Use a persistent bridge only if needed for keeping an embedding model loaded; restart cleanly after protocol failure.

Do not classify an HTTP 200 alone as success: reject empty answers, helper error strings/status None, parser exceptions, malformed response envelopes, and invalid generated JSON. Respect Retry-After when available, bound retry counts, and show retriable failures to users. Preserve the 45-second helper request timeout; choose a slightly larger bounded bridge timeout. A failed live probe means report the actual failure and retain the exact provider constraint; do not inject credentials or silently substitute a provider.

Store originals privately with SHA-256. A material content change creates a version; extraction, chunks, embeddings, summaries, and quizzes identify that version. Jobs have durable database state, atomic claim/lease, retry limits, and recovery after worker death. Use transactional row claims (e.g. SKIP LOCKED), not an in-memory queue. Import retries upsert source identities; rerunning the same snapshot creates no duplicate courses or materials.

Extract per-page text, preserve original PDF page numbers, and review layout before chunking. Use token-aware chunks that fit the embedding model. Evaluate a CPU multilingual model such as paraphrase-multilingual-MiniLM-L12-v2; French and English are both required. Hybrid PostgreSQL full-text + vector retrieval combines ranks after permission filters. Cache by material version, prompt/schema version, and model identifier. No full-corpus prompt uploads.

Answers must cite supplied chunk IDs that map to actual document pages. Validate IDs and quotations against retrieved text; do not claim automated checks guarantee factual correctness. Treat document instructions as untrusted content. If evidence is insufficient, say so. Summaries operate by document/chapter with bounded hierarchical synthesis. Quizzes use validated structured output, source citations, plausible distractors, and answer explanations. Persist accepted generated artifacts; avoid regenerating every course on every app launch.

## Plan of Work

0. **Compatibility proof.** Install .NET 10 SDK, verify PostgreSQL/pgvector availability, record resource use. Offline-test the helper protocol using mocked requests. Make one explicit benign live provider probe with the supplied helper, without course content or credentials, and record status/usable output. A passing probe must yield a non-empty parsed answer with no helper error plus one schema-valid JSON generation with the expected fields; record each result without storing private content. HTTP 200 with an empty/error/challenge response fails. A failed probe blocks Phase 5 generation, not accounts, imports, extraction, embeddings, or retrieval evaluation.
1. **Foundation.** Create solution and four projects as needed, dependency injection, configuration validation, migrations, ProblemDetails, OpenAPI, health checks, private storage, and structured logs that omit tokens/prompts/document bodies. Add unit and real-PostgreSQL integration harnesses, reproducible setup, and backend make targets. Do not start an emulator.
2. **Accounts and policies.** Implement confirmed email/password accounts, refresh, logout-all, admin bootstrap, resource policies, and mobile secure token client. Prove access rejection/revocation and ownership before connecting protected study features. Before generation is enabled, enforce an account-scoped job quota in the API/database (initial development limits: 5 generation submissions per minute, 50 per day, and 2 pending jobs per account); charge idempotent replays once and make the limits explicit to users.
3. **Courses and materials.** Import the local catalogue idempotently, retain chapter order/outcomes and all 35 attachments, implement membership, student course creation and admin recognition. Authorized downloads verify stored hashes. Reader must show actual imported content and open a citation's original page; investigate a private local PDF viewer on Expo Go rather than leaking a private URL to an external viewer.
4. **Extraction and retrieval.** Ingest all 27 PDFs, record page coverage, normalize/chunk/embed, index, and evaluate retrieval using the fixed evaluation set below. Add version invalidation and worker crash recovery. Non-PDF attachments remain downloadable; parse structured datasets only when a defined study use case needs them.
5. **Study features.** Implement jobs/status, grounded Q&A, summaries, quizzes, attempts, explanations, and citation navigation. Provider failures are visible, never replaced by fabricated successful results. Confirm useful French/English output on real material and cap prompt size and job rate.
6. **Community.** Persist threads, comments, anonymous identity handling, votes, bookmarks, reports, and admin moderation. Test concurrency, ownership, and public DTO privacy. Support author edits/deletes without exposing identity on anonymous content.
7. **Mobile integration and proof.** Replace local repositories with the typed API client feature by feature. Preserve design, loading/empty/offline/error states, keyboard and accessibility behavior. Connected mode must not silently fall back to fake auth/sample content. Use a single production dependency composition with no demo repositories/admin switch; test a failed login and unavailable API to prove they produce errors rather than a demo session. Keep any development preview entry point separate from that composition. Validate on the user's Expo Go phone after code is ready. Document local setup; deployment is a separate task.

## Validation and Acceptance — Testing Strategy Checkpoint

**Touched surfaces:** authentication and credentials, API contracts, persistence/migrations, private file storage, background jobs, AI provider bridge, retrieval isolation, community anonymity, mobile integration.

**Risk class:** security-critical for auth/authorization/privacy; high for real-datastore and worker behavior. Planning/file-copy changes alone do not establish those guarantees.

**Required validation:**

- Existing make format and make check remain the mobile baseline. Extend the root with backend format/check targets during foundation; document exact commands before claiming backend coverage. Expected checks: dotnet format --verify-no-changes, dotnet build, dotnet test, Python compile/pytest for owned bridge code. Do not format the supplied helper. Add a backend check that fails when its SHA-256 differs from the pinned digest; explicitly exclude its path from any Python formatter.
- Use WebApplicationFactory with real PostgreSQL/pgvector for database/API integration. Do not use EF InMemory to prove constraints, transactions, SQL retrieval, or migrations.
- Auth matrix: missing/invalid/expired/tampered access, refresh after logout-all/password change/disable, unconfirmed email, registration role injection, student/admin allow/deny, no plaintext credential storage. Two distinct users prove private material, job/result, and attempt isolation, including forged resource/citation IDs.
- Import every attachment hash, exact 12-course/35-file snapshot counts, module/lesson ordering and outcomes; rerun import without duplication. Replacement creates a new version and excludes obsolete chunks. Check rollback/migration behavior on disposable data.
- Worker tests: duplicate enqueue, concurrent claim, lease expiry/restart, bounded retries, cancellation, invalid model output, failed subprocess, and clean shutdown. Failed jobs never become successful empty artifacts.
- Bridge tests mock the actual requests.post conversation: exact endpoint/body/headers with empty cookie and timeout 45; overlapping snapshots/deltas; HTTP 429/5xx; numeric/missing Retry-After; timeouts; empty/malformed response. Catch parser failures outside the immutable helper. One opt-in live spike is distinct from deterministic CI.
- Community tests: concurrent votes preserve uniqueness/counts; non-owner edits fail; anonymous author IDs/email/avatar/profile links never appear in public thread/comment DTOs, including nested replies and notifications.
- Pin 20 human-reviewed retrieval questions from Machine Learning and Développement Mobile: 12 answerable (French/English), 4 insufficient-evidence, 4 adversarial/access-control cases. Record expected document/page for each answerable question. Target expected page in top 5 for at least 10/12; all four isolation/adversarial cases must reject or abstain appropriately. Human review all 12 answers for supported claims and readable citations; zero invalid citation IDs. If targets fail, improve extraction/retrieval instead of changing gold answers to fit output.
- Phone acceptance: register/confirm/login/refresh/logout; browse/join real course; open original material/citation page; ask cited question; read summary; submit graded quiz and restore attempt history; post/comment anonymously; vote/bookmark/report; admin recognition; restart persistence; offline and provider failure recovery. Preserve approved design and no hidden demo auth.

**Deferred validation:** production deployment/SMTP delivery, backup restore, production load/penetration testing, iOS release, university enrollment verification, and hosted object storage. Do not claim production readiness from local tests. Phone interaction is pending until code is complete and user phone available.

**Release smoke required:** before any eventual release, real email flow, authenticated API/mobile connection, private PDF download, one real grounded study job, database/worker readiness, and anonymous posting privacy. Release/deploy is outside this plan's local build authorization.

## Build Loop and Continuation Contract

Use execution-planning to keep this plan current, build-loop for one phase at a time, testing-strategy at each changed risk surface, and code-review for meaningful implementation units. Goal-design is setup only and is not a recurring execution dependency.

Hook inventory: root Makefile covers backend/mobile checks and backend/README.md owns local runtime instructions; mobile/AGENTS.md governs mobile changes. No dedicated backend testing guide, Build Loop profile, CI backend gate or PR template exists. backend/README.md and the Makefile now provide the canonical local runtime/check instructions. Do not invent TeamBoost conventions.

At each phase record scope, changes, exact validation/results, accepted limitations, and next checkpoint here. Do not mark all work complete because a build passes. Technical completion requires deterministic tests; runtime validation requires actual datastore/provider behavior; product completion requires phone flows and study-quality review. Local code review does not require creating a PR.

## Current Receipt and Next Checkpoint

2026-10-21: Foundation/accounts/course import/retrieval milestone implemented and locally verified. Full planned product remains incomplete: durable generation jobs, cited answers/summaries/quizzes/attempts, upload/privacy flows, community backend, and mobile connectivity/phone acceptance remain. No active runtime goal, deployment, commit, or push was created. Next build unit is Phase 5 study-job/worker implementation, carrying forward the pending Phase 3/4 items. Current evidence is in review-receipt.md and backend/README.md.

## Active Implementation Unit

2026-10-21: Foundation/auth/import/retrieval unit complete. Next active unit: durable study jobs and generation. Its detailed implementation checkpoint must be written before edits. Scope of the completed unit: backend code, local PostgreSQL/mail runtime, tests, root check commands, documentation. Validate with the immutable helper probe/contract tests, .NET build, real PostgreSQL API auth tests, and existing mobile checks. Preserve all mobile work; no emulator or external publishing. Record provider failure without changing the helper; account/content work remains unblocked.

### Foundation receipt — 2026-10-21

Implemented API/Core/Infrastructure, PostgreSQL migration, private hashed storage, course import/membership/recognition, Identity bearer accounts and SMTP mail sink. .NET 10.0.402 installed locally. The unchanged helper produced the exact requested JSON in a live probe (HTTP 200, 11 answer characters, schema valid). Five deterministic Python contract checks pass.

11 API integration tests pass against isolated, disposable PostgreSQL databases. Initial live smoke exposed test-host configuration capturing the development connection before test overrides; fixed by explicitly replacing test DB/storage/key services and asserting the disposable database name before migration. Removed only identified test-owned records from the newly created local development database. Rerun passed; development retains exactly 12 imported courses. SMTP/confirmation/login/catalogue/logout-all live smoke passed. make format and make check passed (backend build, offline helper tests, mobile TypeScript and 3 tests); mobile files unchanged.

Local code-review applied to backend working-tree files, Makefile, and ignore rules. Review helper could not freeze an unborn git HEAD; performed direct file review instead. Accepted/fixed the test-database isolation finding and patched a vulnerable transitive PKCS dependency rather than suppressing NuGet audit. No commits or pushes. No backend deployment or emulator. Foundation review has no remaining fix-now finding; study/community/mobile connectivity are explicitly incomplete.

Next unit: document extraction and multilingual hybrid retrieval, with pinned evaluation data and real PostgreSQL isolation tests. API currently running locally on port 5080; database/mail via backend/compose.yaml. Local commands and stop procedure: backend/README.md.

### Retrieval receipt — 2026-10-21

All 27 PDFs / 398 pages indexed into 814 original-page passages. Quantized multilingual MiniLM via FastEmbed 0.9.0, pinned repository revision faf4aa4225822f3bc6376869cb1164e8e3feedd0, 384 dimensions, CPU thread limit 1, chunks <=110 tokens plus special tokens. Initial embedding probe peaked at 683 MB RSS; subprocesses unload after use. Shared filesystem lease prevents API/CLI model loads from overlapping during normal bridge operations. Immutable Gemini helper hash still enforced by tests.

Initial retrieval evaluation: 9/12 expected sources at rank <=5. Improved English/French lexical stemming, meaningful OR terms, title weighting and page diversity; gold questions/expected pages were not changed. Final: 10/12, meeting the pinned baseline target. Known misses: ml-missing and mobile-lab. This measures retrieval, not generated-answer correctness. Four insufficient-evidence scenarios remain pending generation, and access-control scenarios have API/SQL tests rather than a claim that a model can enforce permissions.

One evaluation run encountered PostgreSQL recovery mode after a backend process exited with code 2; container was not reported OOM-killed, root cause unproven. Waited for readiness, serialized AI work, and reran successfully. Do not label this as confirmed memory exhaustion. No unrelated containers/processes were stopped.

Final validation: make format + make check passed; 15 C# backend tests, 8 Python tests and 3 mobile tests passed. C# tests include real PostgreSQL API isolation/version filtering and bridge error contracts. Live restarted API passed SMTP confirmation/login, unchanged 12-course catalogue, denial before membership, actual query embedding + SQL retrieval with the correct PDF page, authorized byte-range download, anonymous refusal, and logout-all revocation. Existing mobile files remained unchanged. Full API/phone connection is still pending.

Local review target: newly written backend source/tests/runtime scripts plus root checks/docs. Accepted/fixed: test DB isolation, serviced PKCS dependency, process-start/lease cleanup, malformed subprocess output. Review tool could not freeze HEAD because this repository has no commit; direct source review and actual validation were used instead. No open fix-now finding for this bounded milestone. Retain the known retrieval misses as accepted baseline limitations; do not describe the full build as complete.

End state: temporary API/database/mail processes stopped after verification to preserve RAM. Imported data, indexes, private keys and model cache are retained. Resume with scripts/dev.py up, then serve. Next checkpoint is the durable study-generation unit; overall product execution remains incomplete.

### Next study-engine planning checkpoint

The next unit now has a concrete execution plan at ../study-engine/study-engine-execplan.md and a proposed compact mission anchor at ../study-engine/objective.md. These own new study-job/generation/mobile evidence; this umbrella retains the completed foundation/retrieval baseline and later community/upload scope. Current request is planning only; no new execution goal or implementation started. Continue from the study-engine unit when instructed.
