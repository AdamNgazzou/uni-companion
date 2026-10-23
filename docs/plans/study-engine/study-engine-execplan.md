# Deliver the connected course study journey

## Purpose / Big Picture

A student signs in on the existing Expo app, joins Machine Learning, asks a question, gets a supported answer, and taps a citation to open the actual PDF at the cited page. The same foundation then supplies saved summaries, generated quizzes, backend grading and attempt history. Generation continues on the server when the app closes; reopening resumes the job/result.

This is the next concrete delivery unit under ../backend-ai/backend-ai-execplan.md. Implementation authorized by the user: "do it". Reversible local code/runtime/test work in the defined scope is now active; external publication remains out of scope. The objective is the active implementation direction.

## Progress and Current Unit

- [x] Inspect current backend/mobile code and prior validation receipt.
- [x] Confirm existing current-checkout workspace choice and preserve all uncommitted work.
- [x] Define the study-engine contracts, sequencing, quality bar and test strategy.
- [ ] Unit 0: connectivity, navigation and private PDF compatibility proof.
- [x] Unit 1: durable study jobs, quotas, cancellation and provider failure contracts.
- [x] Unit 2: grounded Q&A and authorized source/version access.
- [ ] Unit 3: connect real mobile accounts/courses/Q&A/citation viewer.
- [x] Unit 4: saved material/chapter summaries with complete scope coverage.
- [x] Unit 5: generated quizzes, server grading and persistent attempts.
- [ ] Unit 6: integrate summaries/quizzes/history and complete quality/device acceptance.

Plan-state decision: update the usable umbrella plan with this unit-specific execution plan. Keep foundation evidence in its existing receipt; keep all new execution state and decisions here. No independent status board or TeamBoost project is needed.

## Context and Orientation

Workspace: /home/lenovo/projects/mobile-project, current checkout main, no commits/HEAD yet, one worktree. User already chose one repository with mobile/ and backend/. Do not ask the worktree question again. Preserve existing untracked mobile/backend/design/docs and ignored private data. Do not create/switch worktrees, commit, push, merge, or open a PR as part of this local planning objective.

Read first: ../backend-ai/review-receipt.md, backend/README.md, Makefile, backend/src/UniCompanion.Api/Program.cs, Infrastructure/Ai/{PythonAiBridge,CourseSearch,CorpusIndexer}.cs, Core/Study/DocumentChunk.cs, backend/ai/bridge.py, mobile/AGENTS.md, mobile/{App.tsx,src/store.tsx,src/types.ts,src/screens/auth.tsx,src/screens/study.tsx}, DESIGN.md.

Baseline, previously verified rather than rerun during planning:

- .NET 10.0.402, API/Core/Infrastructure, PostgreSQL/pgvector, Identity opaque bearer accounts, confirmation/reset/logout-all, member-only materials and admin recognition.
- Local ignored snapshot: 12 courses, 35 attachments, 27 PDFs/398 pages, 814 version-bound chunks. Do not reacquire LMS credentials or scrape again.
- Quantized multilingual MiniLM CPU embeddings, pinned model/dependencies; initial probe peaked at 683 MB RSS. Normal API/CLI bridge calls share one file lease; subprocesses unload after use.
- Fixed backend/tests/retrieval-evaluation.json: 12 answerable questions, 4 insufficient-evidence and 4 access scenarios. Source recall@5 is 10/12; known misses ml-missing/mobile-lab. Source recall is not generated-answer quality.
- 15 C# backend tests, 8 Python tests, 3 mobile tests and make format/check passed at the last milestone. API/database/mail were stopped afterward to preserve RAM.
- Mobile uses SDK 57 and the approved cobalt/orange design. It is still a local demo: sample IDs, fake verification and admin preview, AsyncStorage-backed state. Do not treat these as real credentials or migrate them into server records.

Local overlays: mobile/AGENTS.md requires SDK-specific documentation, Expo Router, lint and type checks. It conflicts with the existing manually configured React Navigation root. Resolve this mechanically in Unit 0/3 with Expo Router routes preserving existing screen components/design; do not ignore the rule or turn it into a redesign. Root Makefile and backend/README.md are the existing runtime/check hooks. There is no root AGENTS.md, backend testing guide, backend CI gate or PR template. Shared skills are adapted to .NET/PostgreSQL, not TeamBoost/FastAPI/Mongo/Redis.

## Scope and Non-Negotiable Constraints

Include: durable generation jobs, provider error mapping, authorization, citation validation/version access, material-scoped/course-scoped Q&A, material/chapter summaries, quizzes/attempts, minimal account-profile persistence needed by existing onboarding, real mobile auth/course/study integration, offline/error handling and the necessary Router/PDF integration.

Exclude: community persistence/moderation, student file uploads/private-owner upload visibility, new recommendation/agent frameworks, advanced adaptive learning, notifications, deployment, real SMTP production delivery, university enrollment verification, cloud storage migration, model replacement, and visual redesign. In the connected app, unavailable community/upload features show an honest unavailable state; do not populate authenticated screens with demo threads or simulate successful actions. Prototype preview can remain only as a separately named development entrypoint with separate storage, never an auth fallback.

Keep backend/ai/gemini.py exactly unchanged: SHA-256 cc6120d2bec42986ec13be5b92655bedcd35bf23ccade0f9a80b29ead8942630. Add no Gemini key, cookie, upstream session or substitute provider. Application login tokens remain separate from that constraint. Never send whole PDFs or the whole corpus to generation; send bounded authorized excerpts. No emulator, SDK upgrade, paid service, or production side effect.

## Decisions and Discoveries

1. Build PostgreSQL-backed jobs before the Q&A handler uses generation. The previous conversational order put Q&A first conceptually; the dependency order is jobs -> Q&A -> first connected phone journey -> summaries/quizzes. The first vertical slice is useful without waiting for every study feature.
2. Add UniCompanion.Worker as a fourth .NET executable reusing Core/Infrastructure. No Redis, broker, public Python service, or duplicate business logic in Python.
3. Python bridge already returns retryable/status/retryAfter on generation failures; PythonAiBridge currently discards those by throwing a generic AiUnavailableException. Introduce a typed generation result/error at that boundary without changing the supplied helper. Validate deserialization and bound process output/cleanup. Lock-wait timeout is local resource contention, not evidence of an upstream HTTP failure.
4. Jobs are private to their requesting account even when source PDFs are shared. Future shared summaries/quiz publication require separate authorization and are out of scope.
5. Pin source version IDs for a job at acceptance. New material versions exclude old chunks from new retrieval. Existing artifacts stay bound to their original snapshot and display an older-version label, never silently appear current. Authorized historical PDF access makes their citations usable.
6. Use normal request/response plus job polling, not token streaming in this unit. Do not turn partial model output into a successful answer.
7. Account logout clears the device session and data. It does not cancel server jobs by itself. Disabled accounts or revoked course membership prevent continued processing/publication and later reads. Workers recheck live authorization; they never reuse a persisted admin boolean or store user access/refresh tokens.
8. Preserve the 10/12 retrieval baseline; do not alter gold answers to make generation look better. The two source misses are retrieval limitations to address if answer-quality evaluation requires it.

## Data, Interfaces and Permission Contracts

Core/Study: StudyJob, StudyArtifact, ArtifactSource, QuizDefinition, QuizQuestion, QuizAttempt. Infrastructure/Persistence owns mappings/migrations; Infrastructure/Study owns durable queue/storage and generation adapters. Api/Features/Study exposes explicit DTOs. Worker executes typed handlers for answer, summary, quiz. Never serialize an EF entity or raw model object to the mobile client.

StudyJob records: owner ID, course ID, kind, bounded request, pinned source versions, source fingerprint, idempotency key/request hash, state, timestamps, next attempt, attempt count, lease owner/fencing generation, lease expiry, progress stage and safe error code. Persist partial summary map artifacts with their inputs so retries can reuse validated work. Do not persist authentication tokens, raw HTTP headers or credential-bearing URLs.

Proposed API (final exact DTO schema locked in Unit 1 before implementation):

- POST /api/v1/courses/{id}/study-jobs, Idempotency-Key header; kind answer/summary/quiz; question or material/chapter scope; language en/fr; bounded quiz count. Return 202 + job ID/status URL, or an existing authorized result for an identical request.
- GET /api/v1/study-jobs/{id} and GET /api/v1/study-artifacts/{id}: owner + live course authorization; minimal job status and safe results only.
- POST /api/v1/study-jobs/{id}/cancel: owner only, cancellation visible after app restart.
- GET /api/v1/materials/{id}/versions/{versionId}/content: authorized original immutable PDF, correct byte ranges, version belongs to material, no public URLs.
- GET /api/v1/materials/{id}/versions/{versionId}/pages/{page}: authorized extracted-page text for accessibility/source inspection; actual PDF navigation is still required for completion.
- POST /api/v1/quizzes/{id}/attempts with Idempotency-Key and question-ID/answer pairs; GET /api/v1/quiz-attempts with bounded pagination and owner-only results.
- Add the minimal own-profile update fields used by existing Setup/EditProfile if needed. No public role, university, account-active or recognition assignment.

Reject summary/quiz scopes whose pinned versions are not fully indexed with an explicit 409 material_processing response; do not enqueue work that must wait indefinitely for an unimplemented ingestion worker. For Q&A with no eligible source text, produce a persisted insufficient-evidence result without calling the provider. Use existing ProblemDetails conventions and explicit handling of 400/401/403/404/409/429/503. Unknown/foreign private job IDs return indistinguishable 404; unauthenticated requests return 401. Model-provided URLs, source titles, page numbers, author IDs and permissions are never authoritative: server supplies source metadata from validated IDs.

### Queue, quota and idempotence rules

- States: queued -> running -> succeeded, or retry_wait -> running, failed, cancelled. Success is committed atomically with its validated artifact; progress stages do not imply success.
- Claim atomically with PostgreSQL row locks/SKIP LOCKED, a unique lease owner and fencing token. Initial lease 120 seconds, heartbeat every 20 seconds on a separate connection/context. Every progress/completion write compares ownership/fencing; stale workers cannot publish or overwrite newer work.
- One active generation/embedding child across API/worker/CLI via the existing file lease; worker concurrency 1. Heartbeats continue during lock waits and long summaries. Bound job elapsed duration/provider calls, not just individual HTTP timeouts. Initial budget: 10 minutes, <=12 upstream calls/job including repairs, <=3 executions/job. Fail explicitly when scope cannot fit; never silently summarize only the first pages.
- Retry only classified transient failures with capped backoff/jitter and valid Retry-After; permanent protocol/schema failures get at most one repair generation within the call budget. Missing credentials are not a reason to add credentials to Gemini. Do not sleep with a DB transaction open; schedule next attempt.
- Protect API admission with a per-account advisory lock/transaction. Initial limits: 5 new jobs/minute, 50/rolling 24 hours, 2 pending jobs/account, plus a global pending ceiling of 20. Check idempotency before charging quota. Duplicate key+same request returns original ID; same key+different request is 409, even when a cached artifact exists. Use a separate small request-key mapping if needed to preserve that contract.
- Cache successful validated artifacts by owner, kind, exact source fingerprint, question/options/language, prompt/schema version and generation adapter version. Cache lookup and result reads recheck authorization. Cancellation/failure does not masquerade as a reusable successful cache entry.
- Graceful shutdown cancels child work and leaves a recoverable lease/state; app closure does not cancel. Worker crash recovery may repeat an external generation call (at-least-once execution), but fencing/idempotence guarantee one published artifact and no duplicate accepted attempt. Do not promise exactly-once upstream calls.

## Plan of Work

### Unit 0 — Resolve phone transport, routes and actual PDFs

Inspect current mobile APIs against SDK 57 docs. Prove a bundled, pinned PDF.js renderer in the Expo Go-supported WebView can open an authenticated private PDF and select a physical PDF page. Fetch bytes in the app, not an external document viewer; no bearer token in URLs or WebView messages. Bundle PDF.js assets locally, disallow arbitrary remote navigation/network requests, safely encode PDF data, and retain only current-session bounded caches. JavaScript is allowed only for the bundled renderer; document scripting/actions are disabled. Confirm Android/WebView worker/file-origin constraints with a tiny proof before committing the whole reader.

Canonical development connectivity candidate: keep the existing Expo tunnel and add a narrowly scoped Metro /api/v1 proxy to the loopback .NET API, with health readiness for setup. Proxy only the configured local API, preserve status/headers/ranges, bound uploads/body/response behavior, never expose OpenAPI/admin CLI or become an arbitrary target proxy. Resolve the API origin from trusted Expo dev configuration, not arbitrary user input. Prove binary downloads and confirmation-email links from a phone: the configured public confirmation URL must be reachable without API tokens. If the tunnel/proxy cannot satisfy these tests, record one replacement transport decision before mobile integration; no unrelated cloud deployment.

Plan Router cutover under mobile/src/app using existing screen components, fonts, tokens, gestures/back behavior and tabs. Use Expo Router as the single navigation owner per mobile/AGENTS.md; do not mount a second independent NavigationContainer. This is a required compatibility change, not permission to redraw screens. At execution time re-read versioned Router/SecureStore/FileSystem/WebView docs and install SDK-compatible modules with npx expo install.

Exit: private PDF/page, actual phone transport and route feasibility established; package choices/risks recorded. If physical phone is unavailable, complete code/desktop proofs and keep native acceptance pending, not falsely passed.

### Unit 1 — Build durable job and generation boundaries

Add Worker project, typed use cases/ports, EF migrations and the API contracts above. Preserve typed retry details at PythonAiBridge, bounded parsing, fixed executable/script paths, current shared process lease and 45-second helper request timeout. Add request validation, transactional idempotence/quotas, job ownership, bounded cache, heartbeat/fencing, cancellation, safe progress/errors and fixture-based generation tests. Add worker command to backend/scripts/dev.py using the existing private configuration and include it in README startup/stop instructions.

Exit: a fake-provider job survives restart, retries only when appropriate, produces one artifact, enforces quotas under concurrent submissions, and never exposes another account's data. Real provider usage is a bounded opt-in smoke, never a CI dependency.

### Unit 2 — Generate supported answers and serve their sources

Resolve eligible chunks from the pinned source snapshot after live membership/account checks. Questions can be course-wide or explicitly restricted to a material; do not retrieve the whole course and filter afterward. Bound the question to model capacity or handle overlong input explicitly. Select diverse source pages; do not treat a nonempty top-k or its RRF score as proof that the question is answerable.

Generation prompt separates system instructions, user question and quoted untrusted evidence. Structured output contains answer/insufficient-evidence status and per-paragraph source references with chunk IDs and exact quotes. Validate schema, sizes, IDs, version/material relations, physical page numbers and normalized contiguous quotes. Every substantive answer paragraph needs a source reference. For insufficient evidence, produce an honest supported abstention. Request one repair if necessary; otherwise fail rather than publish fabricated citations. Schema/quote validation proves references exist, not factual entailment; qualitative evaluation below remains mandatory.

Persist authorized artifact sources and add historical PDF/page-text routes. Recheck current owner/course permissions before publishing and before each job/artifact/source read; do not disclose snippets through errors, progress or debug logs.

Exit: real Machine Learning answer/citation works through authenticated HTTP; no-evidence, forged citations and cross-user/cross-course tests pass.

### Unit 3 — Deliver the first connected mobile journey

Implement mobile/src/api typed DTO/client modules, auth/session module and feature repositories. Access token in memory, refresh token in SecureStore; no AsyncStorage secrets. Verify real opaque token sizes and native write/read failures on the target phone; fail visibly rather than silently storing secrets insecurely. Do not require Expo Go-unsupported biometric options. Single-flight refresh handles concurrent 401s; retry the original request once only, preserving idempotency keys. A refresh transport failure does not erase the saved session; an invalid/revoked refresh does. Logout immediately clears current account tokens, queued requests, results and cached documents; account switches cannot reuse another user's caches.

Replace fake login/123456 confirmation/admin switch with actual account flows. Handle email confirmation/resend and password reset codes honestly. Resolve Expo Router cutover, protected routes and current profile/course API field differences (server UUIDs, absent prototype color/code fields). Presentation defaults may be derived deterministically; never silently fall back to sample courses/content. Existing onboarding fields must either persist through the own-profile API or be clearly device-only preferences, not falsely saved server state.

Connect browse/join/course/material pages, Ask, resumable jobs and the actual PDF reader. Save job IDs keyed by account, not credentials or answer keys; poll with bounded backoff only while active and stop on terminal state/logout. Closing the app leaves the server job intact. Preserve design, skeletons, keyboard behavior and accessible loading/error/empty states. Unbuilt community/upload actions show unavailable states in connected mode.

Exit: real phone can register/confirm/login, join Machine Learning, ask, reopen a pending/completed job, and navigate to the correct private PDF page. This is the first product checkpoint; do not postpone it until every later feature is finished.

### Unit 4 — Produce saved, scoped summaries

Select one immutable material version or a chapter's ordered version list. Summarize bounded page groups, validate/cite each map result, then synthesize using those grounded intermediate results and source excerpts. Persist map progress for retry reuse. Record complete page coverage including blank pages; omissions or unextractable pages are disclosed. Bound calls/time before enqueueing or reject unsupported scope, rather than truncating silently. Citations remain actual original pages, including documents with different printed page labels.

Reuse the same job engine/cache; subsequent opening fetches the saved artifact. A changed source or prompt/schema version creates a different cache fingerprint. Historical summaries show source version status. No automatic generation of every course at startup.

Exit: summaries for the pinned ML document and one chapter cover their full declared scope, have real citations and survive restart without regenerating on view.

### Unit 5 — Generate quizzes and grade attempts on the server

Reuse the mobile's current count choices only if supported by bounded evidence/call budgets; inspect its exact choices during DTO locking. For v1 lock 5 and 10 single-choice questions; replace unsupported choices honestly. Each question has a stable server ID, exactly 4 distinct options, one correct option, explanation and validated citations. Reject duplicate/ambiguous questions in quality review; never fill missing items with generic sample questions.

Quiz definition is immutable and source-version bound. Public creation/job/status/result/cache/download APIs omit correct answers and grading explanations before submission. Submit question-ID/option-ID pairs; validate exact question membership, uniqueness and completeness. Grade from the stored definition, never client score. Idempotent repeat returns the same attempt; different answers under the same key are 409. Return correction/explanation/citations only for a submitted attempt; new retry attempts remain distinct with stable question identities. History is owner-filtered and paginated.

Exit: grades cannot be forged, answer keys do not leak through any pre-submission DTO, and attempts/history survive app/server restart.

### Unit 6 — Connect remaining screens and audit quality

Wire Summary, QuizSetup/Quiz/Results and QuizHistory to the real API. Keep current styling, quiz feedback personality and missed-question retry flow. Show saved results, source versions, quota/failure/cancellation states, app-background recovery and connection failures. Remove fake successful notifications/admin controls from connected screens.

Run complete deterministic checks, bounded live generation evaluation and the phone walkthrough. Fix accepted review findings and affected regressions; keep all quality statuses truthful. Stop temporary services afterward to preserve RAM. Community backend is a separate next track after this study journey passes.

## Validation and Acceptance — Testing Strategy Checkpoint

Touched surfaces: external generation protocol/errors, PostgreSQL schema/transactions/queue/cache, job/attempt/source authorization, token storage/refresh and account switches, private PDF renderer and development proxy, API DTOs, mobile navigation and study UI.

Risk class: security-critical (auth/data isolation/answer-key privacy); high (durable worker, provider boundary, migrations); product quality-sensitive (grounding, quizzes, PDF page navigation, approved UI).

Required validation:

- Floor: make format then make check; make backend-test with local database available; make bundle for changed mobile dependencies/routes; SDK 57 dependency check, Expo doctor, mobile lint/typecheck per AGENTS.md. If lint tooling is missing, add the minimal SDK-compatible setup rather than claim it ran.
- Real PostgreSQL WebApplicationFactory fixtures must retain the explicit disposable DB name assertion and replaced DB/storage/key services. Add migration tests and constrained concurrent queue/quota/claim tests; no EF InMemory substitution. Controlled clock/lease progression avoids slow sleeps.
- Worker cases: duplicate enqueue, key mismatch, pending/daily/global quota race, competing claims, crash before/after generation, lease expiry, stale worker fenced out, heartbeat during process-lock wait, cancellation during claim/generation, disable/membership revoke before publish, graceful restart, cache reuse and source-version change. Prove one published result/attempt; external call duplication after crash is explicitly possible.
- Provider fixtures: exact immutable helper request/hash checks; preserve retryability/status/Retry-After; empty 200, malformed nested data/JSON, 429, 5xx, network timeout, output overflow, local lease timeout, process crash/cancellation. No Gemini credentials or live-network dependency in deterministic tests.
- Two accounts + two courses: job status/artifact/history/quiz/source direct IDs cannot cross owner/course boundaries; forged source/version/page IDs reject; cache cannot bypass authorization; account disable/role changes use current permissions. Test logout/refresh rejection and account-switch cache clearing on mobile.
- Quiz security: pre-submit status/job/artifact/quiz DTOs never contain keys; forged/duplicate/out-of-range/foreign answers rejected; client score ignored; replay/concurrent submissions idempotent; correction only after valid submission.
- Preserve retrieval recall@5 >=10/12 against unchanged gold. Extend the same evaluation manifest with generated-answer/source validation and reviewed quality results, not replacement questions. Live evaluation: all 12 answerable cases either supported answers or justified abstentions; at least 10 useful supported answers, zero unsupported substantive claims in accepted artifacts, zero invalid citations, all 4 insufficient cases abstain, all 4 access scenarios enforced by code. Human source review is required; a model self-score or quote validator alone is insufficient.
- Summary quality: review complete scope/page coverage, meaningful key concepts, correct French/English language and source-linked claims for ML plus one chapter; no silent omissions. Quiz quality: review at least one 5-question English quiz and one 10-question French quiz for factual correctness, distinct answers, plausible distractors, scope coverage and explanations.
- Mobile deterministic tests: single-flight refresh, failed-refresh classification, auth/session isolation, no fake fallback, job polling stop/resume, immutable server IDs, attempt retries, routing params/page bounds, cache clearing, network/quota errors. Use behavior-focused component/client tests rather than duplicating implementation.
- Phone acceptance (user's Expo Go, never emulator): real confirmation/reset/login/logout; course join; PDF download/page navigation; answer and citation; close/reopen during a job; full summary; quiz/grade/history/missed retry; offline/backend/provider failures; switch accounts; preserve design, Android Back, keyboard, font scaling. Record device/version and actual results. Do not mark phone tests passed merely because a web export works.

Deferred validation: community/moderation/upload privacy, production SMTP/HTTPS/storage/backup/load/security review, cloud deployment, iOS release. Native acceptance remains pending if no phone evidence is available; complete independent code/runtime work first and provide the exact walkthrough.

Release smoke required: no release in scope. Before any later release, authenticated phone/API connection, delivered confirmation email, protected PDF/page, one actual persisted grounded job, worker recovery and quiz attempt must pass in that target environment.

## Runtime, Review, Autonomy and Stop Rules

Existing canonical commands from repo root: scripts below require the backend/ prefix:

```bash
python3 backend/scripts/dev.py up
python3 backend/scripts/dev.py migrate
python3 backend/scripts/dev.py serve
python3 backend/scripts/dev.py worker
# Mobile integration after its code exists: cd mobile && npm start
make format
make check
make backend-test
python3 backend/scripts/dev.py evaluate
make bundle
python3 backend/scripts/dev.py down
```

Worker is now supported by the canonical command and documented in backend/README.md. Stop/restart API and worker after rebuilds before trusting runtime evidence. Do not start unrelated containers, an emulator, or multiple embedding processes. If PostgreSQL reports recovery mode, wait for readiness and inspect the actual error; prior recovery was not proven OOM.

Required execution skills: $build-loop (checkpoint control), $execution-planning (living plan), $testing-strategy (risk-specific evidence), $code-review (material local units and remediation). Use $impeccable when making the planned UI adjustments to protect DESIGN.md's visual quality. $goal-design is setup only and must not recur during normal execution.

Autonomy: user authorized this local implementation with "do it". Within the active unit, normal reversible code edits, migrations on local project data, tests, local runtime bring-up/stop and documentation are within the scoped build. No branch/worktree moves, commits, pushes, PRs, merges, production deployment or external publication are authorized here. No tracker/landing work is required for local completion. Auth/security work already specified by this plan is ordinary authorized implementation; do not re-ask permission for each policy/test. Pause only for destructive changes to user data, new credentials/costs, external targets, or an unresolved material compatibility decision.

Claude posture: auto-on-trigger; one bounded review during setup for queue/idempotence/permission/mobile contract risks. Optional availability is not a completion gate. Record accepted/rejected/deferred findings here.

At each unit boundary record scope, exact checks/results, fix-now findings, limitations and next unit. Keep one active unit. Technical completion requires tests/contracts; operational validation requires real DB/provider/restart evidence; quality completion requires source review and phone evidence. Do not call the whole unit complete at the first passing build or Q&A HTTP response.

Planning finish-line audit: plan/objective are reviewable artifacts; no runtime behavior changed or newly validated this turn. When execution begins, start Unit 0 and keep the previous foundation baseline intact. The unit closes only after all required local review findings are resolved and technical/runtime/quality evidence is reconciled; pending phone evidence remains an explicit open item rather than a fabricated completion.

## Sources and Compatibility Notes

- Existing repo source and ../backend-ai/review-receipt.md are the implementation baseline.
- https://docs.expo.dev/versions/v57.0.0/sdk/webview/ confirms WebView is included in Expo Go; a working private PDF.js/page viewer is still an execution spike, not a proven result.
- https://docs.expo.dev/router/introduction/ documents Router; mobile/AGENTS.md makes it the local navigation requirement. Stay on SDK 57 for the installed Expo Go version.
- https://docs.expo.dev/versions/v57.0.0/sdk/securestore/ confirms encrypted key-value storage is included in Expo Go and documents native size/error and biometric-option constraints. Versioned docs were retrieved successfully on retry; recheck before implementation.

## Setup Review Receipt — 2026-10-21

Planning only. Inspected actual repo/worktree state, provider bridge, mobile state/types/navigation, existing checks and baseline evidence. Versioned Expo WebView/SecureStore and Router primary docs consulted; no application source, configuration, dependencies, database, running services or supplied helper were changed.

The proposed objective is 2,603 characters (under the 3,900 target), measured by the skill helper. No runtime objective created or existing mission replaced. Workspace choice was reused from the user's earlier instruction, not re-asked.

One optional ask-claude repo-inspect review was attempted with only sanitized plan/objective context and explicit secret/private-data exclusions. It reached the 120-second timeout without a completed assessment. No findings were accepted/rejected/deferred from that incomplete review; no fallback provider or second review attempt used. Continue from the local review rather than treat optional availability as a gate.

Local review verified ordering, immutable-provider rules, explicit authorization at publication/read/cache, fencing/idempotence, pre-submit quiz privacy, historical citation access, honest device acceptance, and scope/autonomy boundaries. Clarified empty/unindexed-source behavior and native secure-storage error handling. Required testing-strategy fields, real baseline paths, seven unit sections and objective length passed structural checks. Existing code tests were not rerun for documentation-only work; their last verified baseline is preserved, not newly claimed.

Finish-line audit: requested next-stage plan and proposed objective complete. Implementation not started in this planning turn. First execution checkpoint: Unit 0. Goal-design setup is complete; once this objective is adopted, continue with build-loop/execution-planning/testing-strategy and the plan's execution skills rather than reopening goal-design.

## Active implementation checkpoint

User authorized execution. Start Unit 0 compatibility plus Unit 1 queue foundation; continue through the defined units without re-opening goal-design or asking routine permission. Actual phone acceptance is pending the user device, not a reason to defer independent backend/mobile implementation. No runtime-tool goal was created.

### Execution checkpoint — 21 October 2026, connected implementation

Backend compilation passes with the new Worker executable and StudyEngine migration. PostgreSQL development migration applied. The first expanded real-PostgreSQL suite passed 21/21 tests, including private results, cached/idempotent acceptance, concurrent admission ceiling, retry in a fresh scope, permission revocation before publication, expired lease recovery/cancellation fencing, server grading and pre-submit answer-key exclusion. Direct source review found and fixed nullable Retry-After parsing, checkpoint write fencing and stale-snapshot detection for newly added sources. Further worker/provider/source and runtime checks remain active.

Implemented backend artifact storage as immutable validated JSON on the owning successful job, with checkpoint and attempt tables, rather than adding separate one-to-one artifact/question entities. The public artifact ID equals its job ID. This keeps publication atomic and ownership checks shared; public quiz DTOs explicitly project away grading keys and explanations. Successful cache fingerprints include the source snapshot, prompt version and model identity.

Expo Router now owns the connected application entry; existing prototype screens remain unreferenced source material rather than a sign-in fallback. New connected feature screens reuse the existing typography, mascot, tokens and components. Refresh tokens use SecureStore (memory-only in web preview), concurrent refresh is coalesced, logout fences late responses and serialized storage writes, and job polling stops on background/unmount/terminal states. PDF.js 5.7.284 is bundled locally into an isolated renderer; authenticated bytes are passed without bearer tokens. Native PDF/secure-store/transport acceptance remains pending a real phone, as required.

The Metro proxy is restricted to /api/v1 and the fixed loopback API target. Local Mailpit remains loopback-only; public confirmation links require setting Accounts__PublicBaseUrl to the current reachable tunnel origin before starting the API. No mail inbox is exposed by the proxy.

At that checkpoint, active unit: integration/quality validation across Units 0–6. Code for jobs, answers, summaries, quizzes and connected screens is present; do not equate code presence with all exit criteria passing. A bounded opt-in live study evaluation is running against the unchanged gold manifest, using an isolated temporary account and a private report. Android/web export, source review and final checks are still pending at this checkpoint.

### Runtime/quality checkpoint

Both Android and web exports pass with Expo Router SDK 57. The bundler caught the SDK 56+ restriction on application imports from external React Navigation; shared UI now imports the Expo Router entry point, without disabling the check. SDK 57 patch dependencies were aligned. PDF.js was upgraded from the first 5.x proof to patched 6.4.299 after its direct advisory was discovered (GHSA-hq66-cqwq-w95j); its renderer is isolated and scripting disabled. The rebuilt renderer again opens physical pages 9/10 in desktop Chromium without page errors. Native acceptance remains pending.

The expanded C# suite passed 28/28, including a stale worker losing its lease to a succeeding worker and graceful-shutdown recovery. A real worker restart exposed Development DI validation requiring a routing data source for shared Identity services; added AddRouting in the non-HTTP host and verified Development startup. Real pending jobs continued after restart. Summary generation processed 18/18 and 51/51 text pages, with 3 and 7 provider calls respectively. The initial live evaluation completed all 20 jobs and all four insufficient cases abstained. Source review found two retrieval-related abstentions and one partial answer. Expanding the selected retrieval pages' neighboring chunks fixes the scaling explanation; focused missing-value/lab questions honestly abstain. Combined reviewed answer set: 10 useful supported answers / 12, two retrieval-limited abstentions, no invalid citations.

Quiz source review exposed an avoidable answer-position pattern (all first options on one French model response). Server parsing now gives options opaque IDs and cryptographically shuffles display order while retaining the private correct ID; prompt also requires distractors from related concepts. Fresh 5-English/10-French quiz validation is in progress. The app web smoke uncovered empty-string JSX condition children producing native/web text-node warnings; explicit boolean guards fix those paths. These were found through runtime checks, beyond static type/lint/export checks.


### Local implementation finish-line — 21 October 2026

Final checks: make format and make check passed; make backend-test passed 33/33 on disposable real PostgreSQL; offline Python 9/9; mobile 9/9, format/lint/typecheck passed; make bundle exported Android/web; Expo doctor 21/21 and SDK dependency check passed. The final backend-only bridge timeout classification edit was followed by another complete format/check/backend-test pass. The final mobile export includes the patched bundled PDF renderer and missed-question retry. gemini.py's exact hash is unchanged.

Live source recall remains 10/12. Reviewed generated-answer quality reaches 10 useful supported answers, two retrieval-limited abstentions, four insufficient-evidence abstentions and zero invalid accepted citations; full 18-page document/51-page chapter summaries and fresh English-5/French-10 quizzes are reviewed. Actual web smokes pass real registration/confirmation/login/join, private original PDFs/pages, queued Q&A/citation navigation and quizzes/server corrections/missed retry with no page errors. A browser locator once matched a retained hidden stack screen; the harness was corrected to target the visible/latest result, and the full final flow passed. No application workaround was added for that harness issue.

Local code review completed with no unresolved fix-now findings after remediation. See review-receipt.md for source review, security changes, helper limitation (unborn HEAD), known retrieval limitations and the remaining 23 SDK/development dependency advisories. No forced incompatible dependency upgrade was applied. Private source/quality reports and screenshots remain ignored; temporary evaluation/web-smoke accounts were removed. No original user/course data was deleted.

Units 1/2/4/5 are locally implemented and validated. Units 0/3/6 retain their actual Expo Go acceptance gate: code, desktop and export proofs pass, but no physical phone evidence is available in this session. Remaining active unit is the user's native walkthrough in phone-checklist.md, including SecureStore, tunnel/WebView compatibility, keyboard/back/font scaling, offline/account switching and background recovery. Do not call this production-ready or mark the overall objective complete before that gate. Community/uploads remain a separate later track.

API, worker and Metro used for validation were stopped. This project's database/mail containers are stopped after final testing to preserve RAM; persistent local data/keys are retained. Startup commands and the connected app's behavior are documented in root README.md, backend/README.md and mobile/README.md. No emulator, commit, push, PR or deployment was performed.

## Follow-up: study experience polish and continuity (authorized)

User authorized fixing the web QA findings and improving summaries/related study screens while preserving the approved identity. Current tranche: (1) HTTP-only same-origin browser sessions with CSRF origin checks, native bearer/SecureStore retained; (2) account-scoped quiz drafts storing choice IDs, position and idempotency state, cleared on logout; (3) original PDF zoom/pan and compact reader controls; (4) optional grounded summary headings, readable notes/answers and compact citations; (5) contextual human-readable history/results.

Testing-strategy checkpoint: security-critical browser cookie/CSRF/session boundary and account isolation; high draft persistence/idempotence; UI/readability/renderer changes. Required: real PostgreSQL auth regressions, pure client/draft tests, format/lint/types, Android/web export, SDK check, browser refresh/logout/offline/draft/zoom/source/summary/quiz walkthroughs and rendered inspection at 320/390/desktop. Deferred: actual native phone (no emulator), production hosting and email. No deployment. Gemini helper remains byte-for-byte unchanged. User-owned test services may need one announced API/Metro restart to activate the session/proxy change, then remain running for the user. Preserve all real accounts/jobs; use and clean an isolated QA account.

Progress: implementation active. No database migration expected for cookie sessions or local draft IDs. Optional summary headings deserialize old artifacts; new prompt fingerprint prevents silently reusing earlier summaries for the changed schema. Impeccable's existing context and craft/harden/polish references govern refinement; no redesign or unrelated sidecar repair.

Follow-up implementation and review complete locally. Browser authentication now restores through an HTTP-only Strict application cookie (native SecureStore/bearer retained); cookie mutation Origin checks and stamp/disablement/lockout validation are tested against real PostgreSQL. Browser sign-out stores only a noncredential signed-out marker, attempts server logout even if local storage fails, and cannot be blocked by draft cleanup. Session mutations remain ordered. Quiz drafts persist choice IDs/position/submission key, restore after refresh, and are cleared on logout or accepted grading; corrupt/expired/foreign drafts are handled. Reader supports bounded zoom/pan/Fit width and fenced page/zoom messages. New summaries have optional grounded headings, readable notes and expandable source excerpts; history/results include real course/resource context. Empty-string JSX and missing web checked/expanded ARIA states discovered in runtime testing were fixed.

Validation: final real-PostgreSQL suite 41/41, offline Python 9/9, mobile 17/17, backend format/build and mobile format/type/lint checks pass. Expo dependency check is aligned; doctor 21/21. The browser walkthrough passes ten assertions covering refresh/cookie flags, 200% PDF zoom/pan/Fit width/page navigation, 18/18-page headed summary, expandable evidence, quiz choice/position recovery after refresh, server grading/missed retries, contextual history and logout. Additional live Q&A/source proof and offline/retry recovery pass with no page exceptions; a citation opens original physical page 9. Layouts inspected at 320/390/1366; actual rendered summary/results/history reviewed. Private evidence remains under backend/.local/evidence/polish. Isolated QA account and temporary local credentials were removed; user accounts/jobs and imported courses are preserved. gemini.py exact SHA-256 is unchanged. Final export outcome recorded below when finished.

API, worker, Metro/tunnel and database/mail are intentionally left running for the user's current testing (supersedes the earlier stopped-services historical checkpoint). No emulator, deployment, commit or push. Existing native acceptance checklist is still open; desktop success does not prove Samsung SecureStore/WebView/keyboard/background behavior. Existing retrieval limitations and SDK/development dependency advisories are unchanged.

Final export: make bundle passed for Android (Hermes bundle) and web after the final session/logout/ARIA edits. Latest mobile format/typecheck/lint and 17 tests pass. Browser Q&A plus offline/retry proof also ran after those edits. API readiness returns ready; worker and Metro remain active at the same localhost ports. All authorized local follow-up work is complete; physical-phone acceptance remains the explicitly unverified gate.
