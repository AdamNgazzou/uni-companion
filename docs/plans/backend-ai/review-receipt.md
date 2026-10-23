# Backend foundation and retrieval receipt — 2026-10-21

Status: first implementation milestone complete locally; the full product plan remains open.

## Implemented

- .NET 10.0.402 solution with API, Core, Infrastructure, EF migrations and locked NuGet dependencies.
- Identity bearer accounts: confirmation, login, refresh, reset/change password, logout-all, active-account/security-stamp validation, role bootstrap and account rate limiting.
- PostgreSQL/pgvector and local SMTP inbox, pinned container images, private configuration/files/keys, documented commands.
- Student-created and admin-curated courses, membership, admin-only recognition preserving authorship, private range-enabled downloads.
- Idempotent 12-course / 35-attachment import; all 27 PDFs / 398 pages indexed into 814 passages.
- Pinned quantized multilingual CPU embeddings plus permission-filtered English/French text + pgvector search. Page diversity prevents repeated chunks from one page monopolizing results.
- Supplied gemini.py remains byte-for-byte unchanged, verified by SHA-256 test. No cookie, key, or upstream session added. Live benign probe returned HTTP 200 and the exact schema-valid requested JSON.

## Evidence

- make format and make check pass: zero .NET build warnings/errors, locked restore, formatting, 8 Python offline contract/chunking tests, mobile TypeScript and 3 mobile tests.
- scripts/dev.py test: 15 C# tests pass. API tests use actual disposable PostgreSQL databases, with a database-name assertion before migrations. Coverage includes auth accept/reject/revocation, rate-limit refusal, recognition roles, import idempotence, download membership, retrieval isolation/current-version filtering, and subprocess failure contracts.
- Pinned source recall@5: initially 9/12; after ranking improvements 10/12, meeting the predeclared target. Gold questions and expected pages were unchanged. Two known misses: ml-missing and mobile-lab. This is a retrieval benchmark, not proof of generated-answer correctness.
- Live restarted API smoke: real SMTP delivery to Mailpit; confirmation and login; unchanged 12-course catalogue; search denied before membership; actual local embedding + SQL search finds the expected ML PDF page; authenticated byte-range download; anonymous download denied; logout-all immediately revokes search access.
- Existing mobile formatting was unchanged. No emulator, deployment, commit, push, or runtime goal was created. Private dataset, model cache, .env and keys are ignored by git.

## Review and decisions

Used code-review against the new backend source/tests, root check commands and runtime documentation. The review helper could not freeze an unborn HEAD; performed direct source review with the concrete validation above instead.

Accepted/fixed:

- Test-host config originally captured the development DB connection before overrides. Replaced DB/storage/key resources explicitly in the fixture and asserted its disposable database name before migration. Removed only identified generated test records from the new local development database; reruns preserve its 12-course catalogue.
- NuGet identified a vulnerable transitive PKCS package. Pinned the serviced security version; no audit warning suppression.
- Process-start failures and malformed output could compromise cleanup. Added bounded stdout/stderr, typed failures, reliable gate/file-lease release and corresponding tests.
- Concurrency can load multiple CPU models across API/CLI instances. Shared local filesystem lease serializes normal bridge work; children unload after each operation.

Accepted baseline limitations: the two measured retrieval misses; no approximate vector index for this small corpus. No fix-now finding remains for the bounded foundation/retrieval milestone.

One evaluation attempt encountered PostgreSQL recovery after a backend exited with code 2. Container OOM flag was false; cause remains unproven. Waited for readiness, then reran evaluation and the complete test/live-smoke path successfully. Do not describe a confirmed OOM root cause.

## Remaining / next checkpoint

Implement durable study-job worker, generation quotas/retries and grounded result validation, summaries/quizzes/attempts; complete upload/private-owner behavior, community persistence/moderation, and mobile secure-token/API integration. Complete full answer-quality/adversarial evaluation and phone acceptance afterward. Current retrieval alone does not answer questions or generate study content. No production readiness is claimed.

User requested starting the planned build; this receipt closes the first substantial implementation unit. The living plan remains docs/plans/backend-ai/backend-ai-execplan.md. Setup/run/stop: backend/README.md. Temporary local services are stopped after verification to preserve RAM; data and indexes are retained.
