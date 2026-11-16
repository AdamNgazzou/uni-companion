# Uni Companion backend

For a guided code review, read [Architecture and engineering decisions](ARCHITECTURE.md). For Java-to-C# explanations, see [the learning guide](../docs/guides/backend-dotnet-for-java-engineers.md).

.NET 10 API with ASP.NET Core Identity, EF Core/PostgreSQL, private content-addressed files, and an unchanged supplied Gemini helper behind a private Python adapter. The connected Expo app is in ../mobile.

## First run

Install the .NET SDK version pinned in global.json. The setup in this workspace installed it in ~/.dotnet; scripts/dev.py discovers that location without modifying your shell configuration. Docker must be running.

From the repository root (uv is available in this workspace):

```bash
uv venv backend/.venv
uv pip sync --python backend/.venv/bin/python backend/ai/requirements.lock --require-hashes
python3 backend/scripts/dev.py setup
python3 backend/scripts/dev.py up
python3 backend/scripts/dev.py migrate
python3 backend/scripts/dev.py import
python3 backend/scripts/dev.py serve
```

The API listens on http://localhost:5080. Development OpenAPI: /openapi/v1.json. Readiness: /health/ready. Email inbox: http://localhost:8025. Development uses a loopback-only mail sink, not real external email delivery. The API does not migrate the database automatically when starting.

setup creates a random PostgreSQL password in ignored backend/.env with owner-only permissions. up starts only this project's PostgreSQL (port 55432) and mail inbox (1025/8025), capped at 384 MB and 64 MB. It does not affect unrelated containers. Stop these services with `python3 backend/scripts/dev.py down`; data is retained. Private files and Data Protection keys live in ignored backend/.local. Protect and retain these keys for stable app tokens; local keys are not encrypted at rest.

## Accounts

POST /api/v1/accounts/register with email, password, displayName. Passwords require 12–128 characters, upper/lowercase letters, a number, and a symbol. Registration always returns an accepted response for an eligible-format address, including duplicates. Open the confirmation email in the local inbox and follow its link. Then POST /api/v1/accounts/login with email and password.

Native login returns Microsoft's opaque bearer accessToken (5 minutes) and refreshToken (14 days). Send `Authorization: Bearer <accessToken>` on protected requests. POST /refresh with refreshToken to renew. These are not JWTs and refresh tokens are not single-use. Logout on a device means clearing its local credentials. POST /logout-all invalidates all account tokens immediately. Password reset/change, disablement, and security-stamp updates also invalidate access and refresh paths. The app checks active status/security stamps on every authenticated request.

Browser sign-in uses POST /api/v1/accounts/web/login and /web/logout. It returns no JavaScript tokens: the encrypted uni.session cookie is HTTP-only, SameSite=Strict, scoped to /api/v1, valid for 14 days with sliding expiration, and Secure in production. Set Accounts__WebOrigin to the exact HTTPS UI origin (falls back to Accounts__PublicBaseUrl); development also allows localhost:8081 and 127.0.0.1:8081. Login/logout and authenticated cookie mutations require that Origin. Host UI and /api/v1 on the same origin. The Metro development proxy forwards only this application cookie. Logout clears this browser cookie; logout-all/password changes/disablement invalidate both browser and native sessions. This app cookie is unrelated to the unchanged Gemini helper.

Available account routes: register, login, refresh, confirm-email (GET), resend-confirmation, forgot-password, reset-password, change-password, me (GET/PATCH displayName), logout-all. The default account rate limit is 20 requests per minute per client IP, plus Identity lockout after 5 failed password attempts for 15 minutes. Unknown JSON properties are rejected, including self-assigned roles.

Admin bootstrap grants the role to an already registered/confirmed user. It sets a new security stamp; sign in again afterward:

```bash
python3 backend/scripts/dev.py admin --email your-address@example.com
```

There is no public role assignment or admin demo switch in this API. Anyone registering initially belongs to Horizon University; enrollment verification is deliberately not implemented.

## Courses and materials

Authenticated users can list/create courses. A new student course retains its creator; creator membership is automatic. Join via POST /api/v1/courses/{id}/join. Course detail and GET /api/v1/materials/{id}/content require membership or admin. Downloads support byte ranges. PUT /api/v1/courses/{id}/recognition requires Admin and never changes authorship.

The import command consumes ../data/university-import/catalogue.json and its local files. This source snapshot is deliberately ignored by git; a fresh clone must obtain an authorized copy separately. It verifies every hash/size, copies files into private content-addressed storage, and transactionally upserts courses/materials/versions. The same snapshot can be rerun safely, including concurrent attempts. Original module/chapter/outcome metadata is retained. Attachments are never executed or served as a public directory.

## Validation

```bash
make backend-check
python3 backend/scripts/dev.py test
```

backend-check restores/builds, verifies .NET formatting, and runs offline Python contract tests including an exact helper hash check. test runs API integration tests on disposable, uniquely named companion_test_* databases in the local PostgreSQL instance. It requires `up`; never point it at production. The catalogue integration test generates disposable PDF/CSV fixtures; tests do not need the private university snapshot. Subprocess tests can use system Python with `Ai__Python=python3 python3 backend/scripts/dev.py test` from the repository root; actual indexing/generation still requires the configured AI environment. Existing root make format/check also include mobile checks; native phone testing remains separate.

To manage migrations, with ~/.dotnet on PATH, run from backend:

```bash
dotnet tool restore
dotnet ef migrations add Name --project src/UniCompanion.Infrastructure --startup-project src/UniCompanion.Api
```

## Local document retrieval

```bash
python3 backend/scripts/dev.py index
python3 backend/scripts/dev.py evaluate
```

index extracts and embeds current PDFs sequentially, preserving the original PDF page numbers. It skips already indexed material versions. A quantized multilingual MiniLM model runs on one CPU thread with 384-dimensional vectors and token-aware chunks below its 128-token limit. We pin both the model repository revision and Python dependency hashes. The first run downloads public model files into private backend/.local/models; no model API key is needed. The measured initial embedding probe peaked at about 683 MB RSS. Processes unload afterward to preserve RAM. API and CLI bridge calls share a filesystem lease so normal index/evaluate/search commands do not load separate models concurrently.

POST /api/v1/courses/{id}/search with `{ "question": "..." }` returns up to 8 source passages with chunkId, materialId, versionId, fileName, page, text, and rank score. Membership/admin permission is enforced in the API and again in SQL before ranking. PostgreSQL full-text and pgvector cosine rankings are combined with reciprocal rank fusion; obsolete material versions are excluded. Vectors are stored as real[] and cast to pgvector in the ranking query. The current corpus is small enough to scan permitted candidates; approximate vector indexing is a later measured optimization.

The fixed evaluation contains 12 answerable French/English questions with expected document pages, plus 8 insufficient-evidence/access scenarios. evaluate measures source recall@5 only (target at least 10/12), not answer correctness. API tests separately prove member isolation and obsolete-version exclusion. Search remains a retrieval endpoint. The study-job API below adds validated generated answers, summaries and quizzes.

Never edit ai/gemini.py. No Gemini cookie, key, or upstream session is configured. ai/bridge.py provides error handling around it; deterministic tests never call Google. The live planning spike returned schema-valid JSON, but provider availability is not guaranteed. Generation runs only in the separate durable study worker, using the supplied helper unchanged.

## Deployment boundary

This is local development. Before hosting, configure HTTPS, trusted public email-confirmation URL, actual TLS SMTP delivery, private persistent storage/keys and backups, allowed hosts, and phone transport. Do not expose PostgreSQL or the mail inbox. Do not trust forwarded client IPs from arbitrary proxies. No production deployment has been performed.

## Durable study sessions

After migration, run the API and the worker in separate terminals:

```bash
python3 backend/scripts/dev.py serve
python3 backend/scripts/dev.py worker
```

The worker must be running for queued sessions to finish. Ctrl+C stops either process. A clean worker shutdown puts active work back into retry_wait; a crash is recovered after its 120-second lease expires. Keep the same private .local directory across restarts. One child AI process runs at a time, with a 10-minute/12-provider-call/3-execution budget per job. No Redis or external Python server is needed.

POST /api/v1/courses/{id}/study-jobs with an Idempotency-Key header and one of:

```json
{"kind":"answer","question":"What is classification?","language":"en"}
{"kind":"summary","materialId":"<server UUID>","language":"en"}
{"kind":"quiz","materialId":"<server UUID>","language":"fr","count":10}
```

A material may also scope an answer; chapterId scopes a summary/quiz to the course's lesson ID. Counts are 5 or 10. Poll GET /api/v1/study-jobs/{id}; GET /study-jobs lists the account's saved sessions. Successful results are persisted, source-version bound and reused for identical requests. Historical results carry stale=true and retain authorized access to their original PDFs. Cancel through POST /study-jobs/{id}/cancel. Limits: 5 new jobs/minute, 50/day, 2 pending/account and 20 globally. Oversized or unindexed summary/quiz scopes fail explicitly.

Quiz results omit correct choices and explanations. POST /quizzes/{jobId}/attempts with its own Idempotency-Key and exactly one {questionId,optionId} per question. The server grades from the immutable definition and returns corrections; client-provided score fields are rejected. GET /quiz-attempts?page=1 and /quiz-attempts/{id} expose only the current owner's authorized history.

Original PDFs: GET /materials/{id}/versions/{versionId}/content, including byte ranges. Extracted text: GET /materials/{id}/versions/{versionId}/pages/{physicalPage}. Authentication and live course membership/admin permission are checked on every read. Model-provided IDs and exact source quotes are validated; source metadata always comes from the database. Citation validation does not establish factual correctness by itself.

Opt-in live quality exercise (API need not run, worker must run):

```bash
python3 backend/scripts/dev.py evaluate-study
```

It uses the unchanged gold manifest, creates an isolated temporary local account, respects admission limits, writes the private report backend/.local/study-evaluation.json, then removes its temporary account/jobs. The report includes private course excerpts and quiz keys for source review; do not commit it. Ordinary tests never contact the live provider.

## Community and sample activity

Authenticated routes under `/api/v1/community/posts`: GET feed (`sort=recent|top`, optional `courseId`, `kind`, `page`, `limit` up to 30); POST thread; GET `/{id}` and `/{id}/replies`; POST `/{id}/replies`; PUT `/{id}/vote` with `value` -1, 0 or 1; DELETE `/{id}` for its author/admin. Threads accept title (1–180), body (1–8000), kind (Question/Study tip/Discussion), optional courseId, and isAnonymous. Replies accept body and isAnonymous. Creation requires a stable Idempotency-Key; a changed payload with the same key returns 409. Writes are limited to 30/minute/account. All current accounts share one campus; reading discussions does not grant course-material access. Replies are chronological, paged 30 at a time, and limited to one level.

Public projections omit author IDs and hide names for anonymous content. OP markers only connect replies with the same anonymity mode as their root, avoiding identity disclosure through mixed named/anonymous replies. Ownership remains stored privately. Owner controls and report/admin review screens are available in the connected app.

Run `python3 backend/scripts/dev.py migrate`, then `python3 backend/scripts/dev.py seed-community`. The latter is Development-only, uses deterministic sample IDs and a transaction/lock, adds missing seed rows without rewriting real content, and sends no emails. Eight fictional identities have no password, no confirmation and IsActive=false. Ten threads, 26 replies and 178 votes reference existing course labels where available; no imported courses are modified. Missing courses fall back to Campus life. UI labels sample content explicitly. Rerunning restores missing sample activity, so it is not a cleanup command.

Community account extensions: GET /api/v1/accounts/me/discussions?kind=threads|replies&page=1 derives ownership from the authenticated session. It includes anonymous contributions only for their owner and provides the destination reply page. /api/v1/bookmarks supports account-owned thread/material bookmarks and rechecks material membership on list reads. /community/posts/{id}/subscription provides follow/mute state; PATCH /community/posts/{id} edits owner text while keeping identity fixed; /resolution selects or clears a helpful reply on the owner’s question. Deletion redacts content and keeps reply continuity. Original creation hashes remain stable across editing/deletion.

Reports and identity-mode-scoped hiding are under /community/posts/{id}/report and /block. /community/blocks lists opaque owner-only records for undo. /community/reports read/review requires Admin. Hiding anonymous content never suppresses the same account’s named posts, avoiding a visible identity link. Lists are bounded/paged. The CommunityAccounts migration backfills hashes and follows existing real contributions; it does not emit notifications. On this constrained local database use DOTNET_PROCESSOR_COUNT=1 make backend-test for fixture migrations.

Activity inbox: replies in followed threads and completed/failed study sessions appear under the bell on Learn and You. All/Unread, read state, live destinations and four saved in-app preferences are available; grouped upvotes are opt-in. The worker dispatches activity independently of generation. Phone push delivery is still pending its development build and credentials.

Practice uses the existing bounded study worker and source permissions. Generic job reads omit hints, solutions and rubrics; owner-only /practice/{job}/progress and task actions reveal them deliberately. Attempts are limited to 20 per task and are idempotent under an account lock. No submitted code execution. The explicit local quality command is python3 backend/scripts/dev.py evaluate-practice (optional --cases ml-en); its pinned three-subject English/French manifest is backend/tests/practice-evaluation.json and its raw evidence stays under ignored .local/evidence/next. Generation success alone is not a quality score.

Saved course conversations are available from Ask AI on a course/PDF and You → Course conversations. Explain, Hint and Check reasoning use fresh authorized sources and recent message excerpts. Sent messages survive reopening; one reply runs at a time, with up to 40 turns per conversation. Individual deletion and clear history stop pending replies and preserve practice attempts. Get AI feedback on a saved written/code attempt reviews the submitted text against its task/rubric. Each feedback paragraph identifies saved-work reasoning or cited course sources; code is not executed and feedback is not an official mark.

You → Your learning shows actual quiz/written/lab submissions, assistance, self-marked completion and AI feedback separately. The revision queue uses each quiz’s latest saved attempt and concepts you save yourself; edit topic labels/notes, snooze, dismiss or restore them. Correcting a missed question removes it from the queue. Study history and My discussions are searchable. Daily reminders start off, use your chosen timezone and quiet hours, and appear in Activity while the local worker runs. Phone push still requires a configured development build.

Phone push transport is prepared but disabled until the actual Expo project, FCM credentials and physical development build are configured. See [phone notification setup](../docs/guides/phone-notifications.md). `npm start` stays on Expo Go; `npm run start:dev` is for the installed development build.

## Fast unit tests and CI

From backend/, `dotnet test tests/UniCompanion.UnitTests` checks scope disposal, worker shutdown/recovery and assembly dependency boundaries without PostgreSQL or external services. The integration suite still requires real PostgreSQL and creates disposable databases. `Ai__Python=python3 make backend-test` from the repository root can use a system interpreter for deterministic fixture scripts; actual AI operations require the full private runtime environment.

The source workflow at `.github/workflows/backend.yml` prepares locked restore, formatting/build checks, offline Python contracts, Release build and both test projects with an ephemeral pgvector service. It is not a deployment workflow. A successful GitHub run is not claimed before it is actually executed.
