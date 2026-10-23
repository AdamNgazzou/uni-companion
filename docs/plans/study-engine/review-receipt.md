# Connected study engine — local review and evidence

21 October 2026. Scope: API/worker, durable jobs, generation/retrieval boundaries, sources, grading, connected Expo Router/auth/study screens and PDF rendering. This is local development; physical phone acceptance remains open.

## Review target and disposition

The checkout has no commits/HEAD and all project source is untracked. The structured code-review helper could not freeze a git target (`git rev-parse HEAD` failed). Reviewed the actual changed source/contracts and runtime evidence directly instead; no commit/worktree was created to satisfy tooling. Applied the code-review security/contract/validation checklist with mobile/AGENTS.md and the study-engine plan. No unresolved fix-now issue remains in this local milestone after remediation below; this is not production or phone acceptance.

| Finding | Evidence / fix | Disposition |
| --- | --- | --- |
| Incorrect business ProblemDetails | HTTP 400/429 used a problem body hardcoded to 500. Status/code now reflect the actual error; regression asserts both. | Fixed |
| Cross-worker checkpoint/publication races | Ownership/fencing/lease checks cover terminal and progress writes; checkpoint CTE locks its job row; permission rows are locked and checked at publication. Real PostgreSQL stale-owner/cancel/revoke/restart tests pass. | Fixed |
| Provider null/retry/protocol handling | Null Retry-After is safe; outer malformed protocol is permanent; local lease contention is ai_busy rather than an upstream HTTP failure; deadlines/call budgets stay bounded. | Fixed |
| Incomplete page excerpts | Answers include neighboring chunks on selected pages, bounded to 24 excerpts. Focused scaling answer now supported. Two known retrieval misses abstain. | Fixed within scope; retrieval limitations retained |
| Predictable quiz positions | Model returned all correct options first in one run. Server uses opaque option IDs and cryptographically shuffles options while preserving the private correct ID. Fresh English/French quizzes reviewed. | Fixed |
| Browser fetch receiver | Passing global fetch as an instance method caused browser Illegal invocation. Default adapter invokes globalThis.fetch; browser and focused regression pass. | Fixed |
| JSX text-node warnings | Empty-string logical guards and an inter-fragment space caused native/web warnings. Boolean guards and removal of stray fragment text resolve these; web smokes have no page errors. | Fixed |
| PDF library advisory | First proof used affected PDF.js 5.x. Replaced with patched 6.4.299; rebuilt local renderer, disabled scripting/evaluation, restricted navigation/network and retested actual pages. | Fixed |
| Account cache/session races | Serialized secure-store writes and epoch checks prevent late logout/refresh results from restoring another session. Root protected routes are keyed by account. | Fixed/tested |
| Remaining dependency audit | npm reports 23 advisories through the SDK/development tree: braces, decode-uri-component, node-forge and uuid chains. PDF.js is clear. Suggested aggregate fixes include incompatible SDK/React Native changes; no forced downgrade/SDK upgrade was applied. | Deferred to compatible dependency updates; not a production approval |

## Live source review

Private reports: backend/.local/study-evaluation.json (initial 20 jobs), study-evaluation-context.json (three focused answer reruns), study-evaluation-focused.json (fresh 5-English/10-French quizzes). Reports contain course excerpts/quiz keys and remain ignored. The fixed gold manifest is unchanged.

| Gold case | Reviewed outcome |
| --- | --- |
| ml-classification | Supported category versus numeric output; original introduction p9. |
| ml-workflow | Supported six-step workflow; introduction pp13/18. |
| ml-samples | Supported housing examples, sample/features/target; introduction pp8/17/18. |
| ml-missing | Focused run abstains: the needed percentage example was not retrieved. Initial partial response excluded from useful-answer score. |
| ml-categorical | Supported nominal one-hot / ordered ordinal encoding; preprocessing pp9/12. |
| ml-scaling | Focused run supported distance/gradient sensitivity and tree exception; preprocessing p13. |
| mobile-sdk | Supported SDK tools/API/emulator; Android presentation p33. |
| mobile-service | Supported Activity view versus Service without view; presentation p41. |
| mobile-resources | Supported generated integer R identifiers/classes; resources pp8/33/34 and presentation p45. |
| mobile-folders | Supported res/layout and res/menu; resources pp5/44. |
| mobile-qualifiers | Supported directory suffixes and configuration qualifiers; resources p17. |
| mobile-lab | Abstains: exchange-rate example not retrieved. |
| insufficient-1 through -4 | All abstain; no password, fees, private grades or match outcome invented. |

Result: 10/12 useful supported answers, two retrieval-limited abstentions, zero invalid accepted citations and no unsupported substantive claims found in the bounded reviewed set. This is a manually inspected sample, not a guarantee for every future response. Source recall@5 separately remains 10/12, with unchanged missing-value/lab misses. Four access categories are covered in authenticated integration tests: anonymous, non-member, other-owner/course and obsolete/forged source IDs. Authorized historical versions intentionally remain readable for old artifact citations; new retrieval excludes obsolete versions.

Document summary: 18/18 text pages, three calls. Chapter summary: both documents / 51 of 51 text pages, seven calls. Reviewed major concepts/procedures and cited original pages, including ML families/workflow/evaluation and Python/Pandas/visualisation/library topics. All declared pages were processed; coverage is processing coverage, not a claim that a concise summary repeats every detail.

Fresh quizzes: five English and ten French, each distinct within its scope, four distinct choices, one source-supported correct choice and an explanation. Checked source-backed facts and plausible related-concept distractors. Display positions vary after server shuffle; raw provider positions/answer keys do not appear in public pre-submit DTOs. Grading and replay use immutable server IDs.

## Runtime / UI evidence

Actual desktop Chromium at 390×844, not an emulator or phone:

- Real registration, confirmation through Metro proxy, login, course listing/join/detail.
- Authenticated original-PDF fetch, physical pages 1/2, and independent bundled-renderer proof at pages 9/10.
- Real queued answer, server polling, persisted result, citation opening original PDF page 9.
- Real five-question quiz, all answers, server grading/corrections, second attempt retrying missed questions with original IDs and carried earlier correct answers.
- Final smoke captures no page errors. Private screenshots remain in backend/.local/evidence.

The real worker was stopped/restarted during live evaluation; remaining persisted jobs completed. Controlled real-PostgreSQL tests additionally prove expired-lease takeover, stale-owner fencing, cancellation, graceful requeue, retry in a fresh scope, membership revocation, quotas, history/source isolation and idempotent grades. The Development worker startup path is checked, including its shared Identity routing dependency.

Phone acceptance pending: SDK 57 Expo Go on the user's Samsung, encrypted token size/write/delete behavior, actual tunnel/confirmation transport, WebView PDF rendering and cited page, keyboard/Android Back/font scaling, close/reopen while pending, offline/provider errors and account switching. Production SMTP/HTTPS/storage, community and uploads remain outside this milestone.

## Validation

Final exact command outcomes are recorded in the execution plan after completion. No Gemini cookie/key/upstream session/provider substitute was added. gemini.py SHA-256 remains cc6120d2bec42986ec13be5b92655bedcd35bf23ccade0f9a80b29ead8942630.

## Authorized study polish follow-up — 21 October 2026

Reviewed the new browser authentication boundary, session ordering, drafts, summary DTO/prompt compatibility, metadata projection, and PDF message/render sequencing. Native bearer/refresh behavior remains covered by the original suite. Browser auth uses an encrypted HTTP-only Strict application cookie, production Secure, explicit configured Origin checks for login/logout and cookie mutations, and per-request security-stamp/active/lockout validation. A revoked cookie does not prevent a fresh sign-in. No tokens are put in browser JavaScript storage; only a noncredential signed-out marker and account-scoped choice drafts are stored. This application cookie is separate from the immutable AI helper.

Fixes found during follow-up review/runtime: empty-string route values rendered text children under native View; explicit boolean guards fix this. React Native Web does not forward accessibilityState.checked/expanded in this installed version, so explicit aria-checked/aria-expanded accompany the native state. Draft pruning now tolerates corrupt old local entries. Logout attempts server-cookie clearing even when local storage fails, and draft clearing cannot block credential logout. Session mutations are serialized so delayed logout cannot overwrite a subsequent sign-in.

Summary headings are optional for existing artifacts, bounded on parse and source-grounded in the revised prompt fingerprint. Human-readable labels are batched from authorized job course/material IDs. Attempts apply course filtering before pagination. Correct answers remain server-private before an accepted grade. Quiz drafts contain only opaque selected IDs, position and idempotency state, are validated against the quiz, expire after seven days, cap at thirty, and are cleared on sign-out/account change. Completed attempts remove their own draft.

The PDF renderer retains CSP isolation, disabled document scripting/XFA and no remote network/token embedding. Zoom is bounded to 100–300%; raster output has pixel limits; rendering coalesces pending navigation and messages are fenced by request ID. Source page numbers remain physical PDF page numbers.

Rendered review confirms the existing blue/orange/Nunito/star identity, readable headed notes, compact page links, expandable quoted evidence, real-score correction panels, contextual history, and no horizontal overflow at tested 320/390/1366 widths. Automated browser flow verifies refresh session, PDF zoom/pan/fit/page navigation, full 18-page headed summary, expanded evidence, refresh-restored quiz choices/position, grading/missed retries, friendly history and cookie logout; no page exceptions. Screenshots and raw source evidence remain private/ignored. Physical-phone acceptance is still pending; exports and desktop testing do not establish native behavior.

Final follow-up validation: 41 C# integration tests, 9 offline Python tests, 17 mobile tests; formatting, lint, types and builds pass. Android/web export passes after the last edits; SDK dependency check and Expo doctor 21/21 pass. Additional live answer/citation-to-page-9 and offline/retry recovery pass without page exceptions. QA credentials/account removed. API, worker, Metro and database/mail remain running for user testing. No unresolved fix-now finding in this scoped follow-up; device acceptance remains pending.
