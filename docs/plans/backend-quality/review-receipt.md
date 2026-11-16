# C# quality review receipt — 13 November 2026

Scope: existing C# request/identity/study/practice/tutor/learning seams and the changed worker, bridge and phone-push paths on feat/student-companion-next at HEAD f684276. Preserved the pre-existing dirty tree. This is a scoped engineering review, not a claim of production readiness or physical-phone push acceptance.

## Findings and decisions

| Finding | Severity | Evidence | Disposition |
| --- | --- | --- | --- |
| PUSH-LEASE: stale push response changes device registration | High | DeviceNotRegistered retired a binding without verifying delivery ownership; Finish checked ID but omitted expiry. Both expired/replaced receipt tests failed, and an expired send published its ticket. | Fixed: device invalidation and publication are one transaction, gated by sending state, lease ID and expiry. Token binding remains a separate guard. |
| BRIDGE-OUTPUT: output cap waits behind a live child | Medium | stdout/stderr limit faults were held by WhenAll until caller cancellation. Both baseline regressions failed after ten seconds. | Fixed: observe I/O completions individually, terminate/reap, consume cancelled pipe tasks and release locks. Same-instance recovery is tested. |
| BRIDGE-INPUT: blocked prompt write hides output failure | Medium | The additional no-stdin-reader/large-Unicode-prompt fixture failed after the initial output fix (one failed, two passed). | Fixed: supervise the writer, readers and process exit together. Final suite proves overflow/cancellation/recovery. |
| Receipt lookup vs confirmed delivery failure | Reviewed hypothesis | Expo documents resending a confirmed MessageRateExceeded delivery failure; the existing behavior was correct. | Preserve, with a regression covering transport retry retaining its ticket and confirmed failure retrying the send. |
| Quiet-hours fixture depends on UTC hour | Test defect | Its synthetic 23:00 clock could precede a real queued timestamp, causing a false no-work result. | Fixed: fixture creation/due times match the synthetic clock. |

Worker hosting now uses five focused source files and a thin composition root. One scope-safe lifecycle implementation handles cancellation, idle and backoff; durable state remains in processors. Eleven fast unit tests cover scope disposal, recovery, stop behavior in work/idle/backoff, independent loops and assembly dependency boundaries. No new runtime framework or generic repository layer was added.

## Validation

- Initial five failure regressions reproduced against the original compiled source; focused push/bridge run then passed 25/25.
- Final complete run: 100 PostgreSQL integration tests + 11 unit tests, all passing, none skipped.
- make format and make check pass; final backend format/check pass after the last change. Nine offline Python and 29 mobile tests pass.
- Locked Release build succeeds for all six projects with zero warnings/errors.
- New CI workflow passes actionlint; official action release commit pins and pgvector image digest are fixed. No remote workflow execution is claimed.
- Final helper-validated source review: pass, no remaining fix-now findings. Existing unrelated mobile changes were preserved; phone acceptance is not inferred from source tests.
- git diff --check passes. Gemini helper hash is unchanged. No provider call, main-database migration or real account mutation.

Review helper artifacts: initial target /tmp/skill-runs/code-review/mobile-project/local-feat-student-companion-next-20261113-220651Z, final target 223932Z, validated final result 224238Z. These are local evidence, not published links. Durable implementation details are in backend-quality-execplan.md and backend/ARCHITECTURE.md.

CI is source-ready and remains local until explicitly published. Tests used disposable databases; test-only project containers are stopped after validation with volumes retained. No emulator, commit, push or deployment.
