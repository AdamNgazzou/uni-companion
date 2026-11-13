# C# backend quality and reliability

## Purpose / Big Picture

Requested 13 November 2026: review and improve the C# backend so a recruiter can inspect sound engineering, not just a polished mobile demo. Deliver concrete reliability fixes, a readable and tested host lifecycle, enforceable dependency boundaries, reproducible CI and a short architecture walkthrough. Keep the existing modular monolith: Core, Infrastructure, API and Worker. No new framework, broker, generic repository or artificial Application layer is required for this tranche.

## Context and constraints

- Current branch feat/student-companion-next, HEAD f684276. The pre-existing dirty tree includes phone-push implementation, a migration, documentation and mobile work. Preserve it. Baseline status is in the frozen local review bundle under /tmp/skill-runs/code-review/mobile-project/local-feat-student-companion-next-20261113-220651Z. Review C# sources broadly; structured diff review remains limited to the frozen changes.
- Skills: code-review (assessment only, then exit to implementation), execution-planning and testing-strategy. No root/backend AGENTS.md, docs/code-review.md or repo testing guide exists. The TeamBoost/Python/Mongo conventions are inapplicable; use actual .NET 10/PostgreSQL/EF contracts. Mobile/AGENTS.md applies only if mobile changes become necessary.
- Preserve frontend/API contracts, private course/account data and the supplied Gemini helper SHA-256 cc6120d2bec42986ec13be5b92655bedcd35bf23ccade0f9a80b29ead8942630. No live AI calls, push delivery, emulator, deployment, commit or push. Existing physical-push prerequisites remain unproven.

## Review findings and decisions

1. PUSH-LEASE (high, fix now): PushProcessor invalidates a device on DeviceNotRegistered without checking that it still owns a live delivery lease. Finish checks LeaseId but not expiry. Make invalidation/publication atomic and owned by an unexpired lease; prove stale responses cannot change either row.
2. BRIDGE-OUTPUT (medium, broader baseline audit, fix now): PythonAiBridge awaits Task.WhenAll(exit/stdout/stderr). An output reader may fault at its size limit while the child keeps running, so the aggregate waits until the full deadline. Detect reader failure immediately, kill/reap the process and release both locks; prove oversized stderr/stdout cannot block a following call. gemini.py is unaffected.
3. Preserve Expo's receipt semantics. Initial suspicion about retrying MessageRateExceeded was rejected after checking official documentation: a confirmed failed delivery is retried; a failed receipt lookup retains the ticket. Add regression proofs of both paths rather than changing correct behavior.
4. Keep the worker host thin and give independent polling services one tested scope/cancellation/backoff implementation. No queue semantics move into the lifecycle helper; processors retain durable state machines.
5. Add architecture-boundary tests and a GitHub Actions backend gate with a real pgvector PostgreSQL service, locked restore, formatting, Release build and tests. Document reviewer entry points, failure semantics and limits. Do not claim the new workflow has run on GitHub before it is pushed.

## Plan of Work

- [x] Inspect current source/dirty state and local skills. Freeze and validate the initial structured review; inspect untracked push sources directly.
- [x] Write failing PostgreSQL lease and local subprocess-output regressions; record baseline failures.
- [x] Fix push fencing/atomicity and subprocess fail-fast cleanup; rerun focused proofs.
- [x] Extract scope-safe independent workers; add lightweight worker lifecycle and dependency-boundary tests.
- [x] Add CI and concise backend architecture/reviewer documentation; preserve existing learning guide.
- [x] Run required checks and a final scoped source review, record evidence and honest limits.

## Validation and Acceptance

Touched surfaces: push persistence/provider boundary, AI subprocess bridge, worker hosting/lifetimes, C# project boundaries, test/build workflow and documentation. Highest risk: durable worker ownership and device credential mutation; no auth protocol or DB schema change is planned.

Required: real PostgreSQL tests (expired/replaced lease, token rotation, concurrent claim/idempotency and existing account/content isolation); subprocess fixtures proving output failure, cancellation, process cleanup and lock recovery; worker tests for scope disposal, recovery and cancellation in work/idle/backoff; architecture reference guards; make format then make check; full .NET suite; locked Release build. CI YAML/config validated locally where tooling permits, without dispatching a remote job. External provider calls are faked in deterministic tests; real database remains real.

Deferred: physical phone/push credentials/production deployment, live provider quality evaluation, load testing and major application-layer redesign. Release smoke required: none (local source-only work); optionally check existing local readiness without interrupting user services. Never reset/clean the dirty tree or mutate real account/course data for tests. The test fixture creates disposable databases and private temp directories.

## Progress and discoveries

Initial assessment complete. Source review found meaningful worker defects, not a reason to rewrite every feature. Current analyzers already enforce nullable reference types and warnings-as-errors. Framework documentation checked for scoped hosted services and options; Expo docs explicitly support retrying confirmed MessageRateExceeded delivery failures. Formal PUSH-LEASE finding validated against the frozen local target; the bridge finding is deliberately recorded as a broader baseline audit outside that diff.

### Regression and implementation checkpoint

The first baseline attempt could not compile because this refreshed environment lacked cached NuGet artifacts and the private Python venv. Restored the existing locked package graph plus the new unit project's lockfile; existing dependency lockfiles are unchanged. For deterministic C# bridge fixtures, use the installed Python interpreter through Ai__Python (no model/runtime download required). Started only this project's PostgreSQL/Mailpit containers; unrelated existing containers/processes remain untouched.

Baseline red proof: five selected regressions failed on the original compiled code: both stdout/stderr overflow cases hit caller cancellation after ten seconds instead of reporting the permanent size failure; expired send published a receipt; expired/replaced receipt workers disabled the device. Subsequent focused PushTests/BridgeTests pass 25/25 after remediation. Worker/architecture suite passes 11/11 without a database.

Push publication now checks ID, sending state, lease ID and expiry, and atomically publishes device invalidation only when its delivery update succeeds. Quiet-hour attempt refunds are part of the same fenced publication. A final ownership check occurs before provider I/O. Correct confirmed-delivery resend behavior is retained and separately tested against receipt HTTP outages.

The C# bridge observes individual I/O completions rather than delaying reader failures behind Task.WhenAll, cancels outstanding I/O, terminates/reaps the child and observes pipe tasks before releasing locks. Overflow tests prove another call recovers using the same instance. Worker Program is now composition only; all four independent loops use ScopedPollingWorker with async scopes and cancellation-aware idle/backoff. Unit tests prove scope disposal/recovery/shutdown in every phase and cross-loop isolation.

Added architecture/reviewer guide and a commit-pinned GitHub backend workflow, with a pinned pgvector service and ephemeral fixture credentials. Action release SHAs were resolved from the official actions repositories; no remote workflow was dispatched. Full validation and final review remain active.

### Build checkpoint

make format and make check pass, including all 29 existing mobile tests and offline Python contracts. The locked Release build succeeds for all six projects with zero warnings/errors. actionlint v1.7.12 validates the new workflow; its downloaded binary was checksum-verified against the official release and stays outside the repo. No existing package lockfile changed; the new UnitTests project has its own lockfile.

The quiet-hours fixture was adjusted so its due/creation times match its synthetic clock, avoiding an incidental failure when CI runs after 23:00 UTC. Cancellation fixture readiness uses an atomic rename so the test never reads a partially written PID. A full C# run against disposable PostgreSQL databases is active with the local Python interpreter; no real AI/Expo call or account mutation is involved.


## Outcomes and final audit

All authorized work in this quality tranche is complete locally. Final source review passes with no remaining fix-now findings, validated by the code-review helper against HEAD f684276 and the preserved working tree. See review-receipt.md for the durable summary; frozen source and structured review artifacts are in /tmp/skill-runs/code-review/mobile-project/local-feat-student-companion-next-20261113-223932Z (validation run 224238Z).

A second bridge regression reproduced stdin backpressure: a child emitting oversized stderr without reading a 48,000-character Unicode prompt still blocked the initial fix at the prompt write (1 failed, 2 passed). The final bridge supervises the input writer alongside process exit and both output readers. This is now included in the full green suite, alongside explicit cancellation/reaping and same-instance recovery.

Final evidence:
- make format and make check pass; existing mobile lint/types/format and all 29 mobile tests pass.
- Final make backend-format and make backend-check pass after the last bridge/fixture changes; locked restore unchanged and all 9 offline Python contracts pass.
- Final Release build: six projects, zero warnings/errors.
- Final real PostgreSQL integration suite: 100/100; unit/architecture/lifecycle suite: 11/11. No skipped tests.
- actionlint v1.7.12 passes; git diff --check passes. No existing lockfile was modified; UnitTests has a new locked graph.
- gemini.py SHA-256 remains cc6120d2bec42986ec13be5b92655bedcd35bf23ccade0f9a80b29ead8942630. No live AI or Expo request was sent.

The developer's pre-existing mobile/push work remains in place, and no real course/account data was changed. No migration was added by this tranche or applied to the main database. No emulator, commit, push or deployment. CI is prepared and statically validated, not claimed to have run on GitHub. Physical phone/push credentials, production hosting and broader load/quality evaluation remain separate work.

The project database/mail containers were started only for tests (both were stopped at intake). Restore them to stopped after the tests, retaining their volumes and leaving unrelated containers/processes untouched. No API, worker or Metro server was started in this tranche.

## PR packaging — explicit user authorization, 14 November 2026

The user explicitly requested "go on make a pr" after the quality pass. This supersedes the prior source-only stop for ordinary packaging actions. Planned writes: commit the pending phone transport/SDK preparation together with the backend quality changes as "Complete phone notification transport and harden backend workers"; push feat/student-companion-next normally; create a ready (non-draft) PR against the repository default main titled "Add practice, tutor, activity and backend reliability improvements". No TeamBoost task applies: this is the user's personal university project. No existing PR was found, and fetched main has no commits beyond the current feature branch's merge base.

PR scope includes all six previously published feature commits (course/community/activity/practice/tutor/learning) and the current pending completion; its description must cover that complete diff, rather than claiming to be a backend-only patch. No merge, deployment, force push, production credentials, Expo/Firebase project creation or remote mobile build is authorized. GitHub CI is an expected consequence of publishing the PR. Existing local test/review receipts remain the evidence; additional export/startup smoke evidence is recorded below as it finishes.

The packaging file scan found zero private/generated Git-visible paths and zero credential-pattern matches. Local university snapshots, .env/.local, native build output and QA credentials remain ignored. No PR template or repo-owned PR-body validator exists in this checkout; use the exact reviewed body file with gh --body-file.
