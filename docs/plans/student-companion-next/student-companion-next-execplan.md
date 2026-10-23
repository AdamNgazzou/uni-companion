# Student companion: practice, tutor and community activity

## Purpose / Big Picture

Make the existing app a place students return to: find a course, understand its materials, work through a new exercise or lab, ask follow-up questions, and return to useful discussions when someone responds.

Requested 23 October 2026 after the user tested the app and reported that it looks good. The user explicitly chose mixed written exercises and coding labs. The user subsequently authorized implementing this roadmap with execution-planning, testing-strategy and build-loop. All locally implementable units are active scope, executed sequentially; remote push delivery still requires its real build/credential prerequisites. SDK 58 compatibility is complete locally.

Preserve the approved cobalt/orange/Nunito/star identity. Keep Learn / Discuss / You; add contextual destinations instead of adding a tab for every feature. Success means a student can complete the journeys below, with reliable saved work, clear source scope, private accounts and honest AI feedback.

## Local overlays and constraints

- Skills: execution-planning and testing-strategy, adapted to this repo's .NET/PostgreSQL/Expo stack; Impeccable for the UX assessment. TeamBoost, FastAPI, Mongo and ARQ conventions do not apply. No root AGENTS.md or docs/guides/testing-strategy.md exists; mobile/AGENTS.md governs native code.
- Existing evidence: ../study-engine/study-engine-execplan.md and ../community/community-execplan.md. Retain those historical receipts.
- Initial baseline 96a68c7 was committed and pushed to main at the user’s request. Preserve private data; execute on feat/student-companion-next with local checkpoint commits. No force push, production deployment or emulator. The current build request authorizes the planned local implementation.
- Keep backend/ai/gemini.py byte-for-byte unchanged (SHA-256 cc6120d2bec42986ec13be5b92655bedcd35bf23ccade0f9a80b29ead8942630). Reuse the private bridge, worker, bounded prompts, source permissions and generation limits. No model replacement or additional provider credentials.
- Do not scrape the LMS again. Use the authorized local import. External push credentials/build accounts remain a prerequisite for real push delivery, not an assumed existing capability.
- The user reports successful phone testing, but supplied no device versions or itemized acceptance results. Record this as positive user feedback; do not mark every old native checklist item passed.

## Progress

- [x] Inspect connected courses, study, profile, forum, worker and generation contracts.
- [x] Confirm mixed written/coding practice preference.
- [x] Verify current Expo upgrade and notification constraints against official documentation.
- [x] Write prioritized feature scope, implementation seams, acceptance and test strategy.
- [x] Unit 0: SDK 58 migrated and locally verified; physical SDK 58 phone acceptance remains open.
- [x] Unit 1: course preview/join, sections/source scope, direct discussions, clear tools and real resume item; local validation passed, native walkthrough pending.
- [ ] Unit 2: My discussions, saved threads, drafts and basic community controls.
- [ ] Unit 3: durable in-app activity inbox and notification preferences.
- [ ] Unit 4: grounded written exercises and coding-lab briefs.
- [ ] Unit 5: saved multi-turn course tutor and practice feedback.
- [ ] Unit 6: learning continuity and opt-in revision reminders.
- [ ] Unit 7: actual remote push on a physical development build.

## Current truth and UX assessment

This is a source-based assessment, not a new screenshot or usability session. The user likes the current appearance; preserve it. Validate the proposed interaction changes with rendered web checks and a phone walkthrough during implementation.

| Existing behavior / evidence | Problem or likely confusion | Planned change |
| --- | --- | --- |
| courses.tsx says course discussions are coming later | Factually stale: persisted course-labelled discussions exist | Add a course Discussions entry with the course filter applied; keep upload availability separate |
| CoursesScreen.open joins any unjoined course immediately | Exploring and joining are one action | Allow a catalogue preview of public metadata with an explicit Join button; keep materials member-only |
| Resources are a flat list; dataset rows have no action | Lecture, exercise, lab and unsupported file are hard to distinguish | Add visible resource types, group by actual lesson metadata, and explain file availability; avoid guessing a type silently |
| Study setup says “The whole chapter” for a LessonId scope | Students cannot see what “chapter” includes | Show actual section title, included files, indexed/unsupported status and scope limits before generation |
| “The key ideas”, “Test what clicked”, “Ask your course companion” | Warm copy obscures tool names and the single-question limitation | Use Summary, Quiz and Ask AI as primary labels; retain playful supporting copy |
| Home hero links to saved sessions; no actual resume item | A large prominent area gives no specific next action | Show the most recent unfinished activity or running job with its course/resource; avoid invented progress |
| Profile only links study history and settings | No way to find one's posts/replies directly | Add My discussions, Saved items and Activity with bounded lists |
| Replies are one level; “OP” is unexplained | New students may not understand the label or who a reply addresses | Use “Author” where safe, retain mixed-anonymity privacy; direct reply targeting is a later explicit contract |
| Votes have a net score, not likes | “Someone liked your post” could misstate behavior | Call them upvotes; group activity, never notify about downvotes |
| No notification inbox or on-screen live update | Returning to a discussion requires manual searching | Add Activity and follow/mute controls; refresh on focus and manual pull-to-refresh, then bounded foreground polling |
| Forum UI has no edit/delete controls, despite a delete API | Ownership actions are unavailable to the student | Add confirmed own-content deletion and bounded editing with an edited label |

## Decisions and feature priorities

### Unit 0 — SDK 58 compatibility, now

Align Expo, React, React Native, Router, Expo modules and SDK-managed native dependencies using the SDK 58 compatibility manifest and Expo installer. Active target: Expo 58.0.17, React 19.3.0, React Native 0.88.0; Node 22.23.2 meets the package engine requirement. Repair concrete API/type/bundling differences. Preserve the API proxy, PDF renderer, sessions and native identity. Update current startup docs; historical SDK 57 receipts remain historical. Use Node 22 if it meets SDK 58's stated engine range; otherwise update the documented runtime prerequisite. Do not create native directories or start an emulator.

Acceptance: locked dependencies agree with SDK 58; format/check, doctor, dependency check and fresh Android/web exports pass. Start/reload on SDK 58 Expo Go for actual native confirmation. A previous SDK 57 phone run is not SDK 58 evidence.

### Unit 1 — clearer course and study navigation

Scope: fixes in the table, explicit course joining/preview, lesson/resource grouping, accessible type labels, indexed-state feedback and contextual actions. The Course screen exposes Materials, Practice, Ask AI and Discussions. A material exposes Read original, Summary, Quiz, Generate exercises and Ask AI; unsupported tools explain why. Keep source scope/language visible on generated results. Course-level practice must select a bounded section/material set instead of passing the whole corpus to the model.

Acceptance: discover a course without joining, intentionally join, open its real lab PDF, understand which resources generation will use, and reach that course's discussion feed in one contextual action. No contradictory “coming later” notice remains for implemented features.

### Unit 2 — personal discussions and community essentials

- You → My discussions: Threads and Replies, with recent activity and links to the actual reply page/anchor. Include the owner's anonymous posts privately; never expose an author filter allowing others to enumerate anonymous content.
- Saved threads/resources, backed by account-owned server bookmarks; course filter and remove control. Bookmarking and automatically saved generated sessions are distinct.
- Follow/unfollow a thread. Automatically follow threads one creates; replying follows by default with an explicit opt-out. Author reply alerts follow the same mute controls.
- Edit/delete own posts and replies, clear edited/deleted states, account-scoped composer drafts and discard confirmation. Preserve anonymity mode on edits to prevent accidental retrospective identity disclosure. Explain deletion effects on replies before confirmation.
- Mark a question resolved / select a helpful reply, controlled by its author; label the outcome as the asker's selection, not factual verification.
- Basic report, block and admin review/remove workflow before broader student rollout. Blocking hides content/alerts without changing author identity projections. This is a scoped moderation requirement, not an unrelated administration dashboard.

Acceptance: find an old anonymous reply under one's own account, follow its thread, save it, edit/delete one's own content, and switch accounts without seeing private drafts, subscriptions or bookmarks. A stranger cannot modify it or discover anonymous authorship.

### Unit 3 — in-app activity inbox first

Provide a bell/unread indicator from Learn and You, with Activity → All / Unread, individual read state, mark all read, and contextual destinations. Trigger durable events for replies to one's threads, new replies in followed threads and completed/failed study generations. Deduplicate the same reply qualifying through multiple subscriptions. Exclude one's own actions, disabled/sample recipients and muted/blocked sources. Do not generate retroactive alerts for the sample seed.

Upvotes: optional grouped in-app activity (for example, “Your discussion received upvotes”), disabled for push by default. Preserve voter privacy: no voter names, actor IDs or downvote alerts. Repeated upvote/remove/flip operations must not farm alerts; use a unique voter/post positive-event identity and aggregate public counts. Remove/deleted content gives a safe unavailable destination rather than opening cached private content.

Preferences: replies, followed-thread activity, study completion/failure, grouped upvotes; per-thread mute. In-app inbox works even when OS notification permission is denied. Store event/outbox and inbox recipient changes transactionally with the originating write; consume outbox in the existing worker with short independent dispatch so AI generation does not block activity. Use durable retry/backoff and unique delivery keys. Promise a single inbox item per event/recipient; remote delivery remains at-least-once with duplicate suppression, not a claim of exactly-once push.

Acceptance: another real test user replies; the owner gets one unread item after reload; tapping opens the correct thread and reply page. Mark-read persists. Anonymous actor text stays anonymous, including notification payloads. Retry/crash between commit and dispatch loses no durable activity or duplicates inbox rows.

### Unit 4 — generate written exercises and coding labs

Feasibility: yes, by extending the current generator. Today it accepts answer/summary/quiz only; it does not already implement labs. Extracted PDF text can supply concepts and patterns from lecture notes and existing exercise/lab sheets. Figures, scans, ZIPs, notebooks, code files and dataset contents are not automatically understood by the current pipeline. Inventory actual supported assets first; show unsupported inputs. Begin with text-based sources and runnable self-contained examples, or a verified authorized dataset reference. Never invent a dataset download or claim to inspect unparsed files.

Student flow: course → Practice → choose theory/exercise/lab sources → written / coding / mixed → topic, guided/standard/challenge, approximate duration, English/French → generate → work through tasks → request graduated hints → submit an attempt or reveal solution → save/revisit. Start with a small bounded set (3 written tasks or one 3–5-step lab); expose estimates as estimates.

Each artifact contains objectives, prerequisites, selected immutable source versions, tasks, given data, expected deliverables, hints, private worked solutions, rubric and citations for the concepts used. Clearly label new examples and synthetic datasets as generated; citations support taught methods, not a claim that invented examples appeared in the source. Do not simply copy a supplied exercise sheet or its answer key. New variation must be solvable from the supplied facts and prerequisites.

Written practice: short answer, numerical worked problems, explain/compare, and scenario tasks. Use deterministic grading only for an explicitly supported answer format/tolerance with a validated key. Other answers receive labelled AI formative feedback against a rubric, with uncertainty and source links; no claim of official course marks.

Coding labs: brief, starter code, tasks, tiny labelled synthetic data where suitable, sample input/output, local run instructions and suggested checks. The first version runs on the student's own machine; accept bounded pasted code/output for feedback. Do not execute arbitrary student code in the API, worker or bridge. Automated execution is a separately scoped future sandbox with resource/network/filesystem isolation. Untested generated code must be labelled; independent execution/review of representative generated labs is an acceptance gate.

Server keeps worked solutions and full rubrics private until explicit reveal or accepted submission, using separate public DTOs/endpoints as with quiz grading. Hint/reveal actions and attempts are owner-bound, retry-safe and survive reopen. Revealing a solution is allowed but changes the activity to “solution viewed”; it is not counted as unaided success. Do not weaken pre-submit quiz-key privacy to implement hints.

Quality spike before full UI: generate and independently review representative English/French written/coding tasks from at least three available course subjects. Freeze evaluation cases before prompt tuning. Launch gate: zero unsupported source claims, zero leaked private solutions, all accepted task sets have sufficient inputs and solvable reference solutions, and reviewed code examples run in their documented local environment. Fix or narrow unsupported task types instead of hiding failures behind good average scores.

### Unit 5 — course tutor, with real follow-up context

Evolve the current one-question Q&A into saved conversations tied to a course and optional material/section. Example: “Explain gradient descent” → “Show a small example” → “Ask me a question” → “Help me understand my mistake.” Offer Explain, Hint and Check my reasoning actions without cluttering every response.

Store owner-bound conversations and ordered turns. Serialize accepted turns per conversation, link each generated turn to its durable job, and use a stable request key for retry. Retrieve authorized evidence for each new turn; bounded prior messages provide context, never permission or evidence by themselves. Cite course claims and abstain when evidence is insufficient. Keep sources inspectable, and label constructed examples. Changing course starts a new scoped conversation; no silent cross-course context. Session recovery works after closing the app. Add conversation deletion and clear history controls.

Keep the existing job UX while generating; the bridge is request/response, not token streaming. Do not promise instant streaming or permanent model memory. Retain rate limits and bounded recent context. A hint action must not receive unrevealed private quiz/lab answers through public client data. Tutor messages, retrieved documents and pasted student code remain untrusted input.

Acceptance: resolve “why?” against the immediately preceding explanation, recover a conversation after reopening, inspect original cited pages, and fail safely on permission revocation/provider outage/concurrent duplicate sends. Another account cannot access turns or embedded sources.

### Unit 6 — useful learning continuity

Show Continue studying, Saved items, quizzes/labs attempted, completed tasks and topics to revisit from actual activity. Separate self-marked completion, AI feedback and deterministic scores. Add simple topic tagging and student corrections before claiming topic mastery. Offer a small revision queue from missed quiz items and explicitly saved concepts; allow dismiss/snooze. Flashcards can reuse validated summaries as a later addition, with editable prompts and sources.

Opt-in study reminders use a student-selected time, quiet hours and timezone. No inferred exam dates, fake streaks or guilt-based alerts. An exam planner can follow once students can explicitly enter dates and choose material. Course search and searchable saved study/discussion history belong here if the real library warrants them.

### Unit 7 — remote push delivery

Build on the durable inbox; push is a transport, not the source of truth. Configure expo-notifications for SDK 58, Android channels, project/device token registration, rotation, disabled-token cleanup and ticket/receipt retry handling. Associate registrations with the authenticated account and installation; revoke association on sign-out/account switch. Push payloads contain an opaque destination and generic preview by default, never private PDF excerpts, answer keys or hidden author identities. Resolve and authorize destinations after sign-in; do not trust payload-provided content or arbitrary URLs. Fetch unread counts from the server.

Request OS permission after the student opts into a useful notification type. Denial leaves Activity fully usable. Respect thread mute, categories and quiet hours at dispatch. Replies and finished study jobs can push by default after consent; upvotes remain grouped/optional. Test warm/background/terminated app opens on a physical phone.

Android remote notifications require a development build; Expo Go is insufficient. Prerequisites are an Expo project identity, Android push credentials and a phone development build, plus a backend/worker running to send actual events. Prepare and validate local code before any external build/configuration action requiring missing credentials. No production deployment is implied. With the developer laptop/backend stopped, this local environment cannot send new server-side replies/completion notifications.

## Implementation map and dependency order

Existing mobile seams: mobile/src/features/{courses,community,study,personal}.tsx; src/api/{types,client,runtime}; src/auth/session.tsx; src/app/ and src/app/(tabs)/_layout.tsx; ui.tsx/theme.ts; pdf/. Create focused feature modules for tutor, practice and activity instead of further enlarging study.tsx. Keep routes thin and non-route logic outside src/app.

Existing backend seams: Core/{Courses/Course.cs,Community/CommunityPost.cs,Study/StudyJob.cs}; Infrastructure/{Persistence/AppDbContext.cs,Study/{StudyJobs,StudyComposer,StudyProcessor,StudyAccess}.cs}; Api/Features/{Courses,Community,Study}; Worker/Program.cs; private Ai/PythonAiBridge.cs. Add focused domain/services/endpoints and reviewed EF migrations for bookmarks/subscriptions, activity/outbox/preferences, practice attempts/reveals and tutor turns. Plan indexes on owner+created time, recipient+unread+time, subscription thread+owner, and unique event/recipient and conversation/turn keys. Preserve source-version access at generation, publication and every read.

Suggested new API groups (proposals, finalize per unit): /accounts/me/discussions, /bookmarks, /community/posts/{id}/subscription, /activity with read/preferences, /practice and attempts/hints/reveal, /tutor/conversations and turns, /devices/push. Own-content discovery must derive the owner from authentication, not an arbitrary user ID. Paginate bounded lists and index the exact ordering; test equal timestamps.

Order: 0 → 1 → 2 → 3; practice 4 → tutor integration 5 → continuity 6. Push 7 depends on 3 and its external build prerequisites, and may run after 3 when those are available. An initial usable tranche is Units 1–3; practice/tutor is the next independent product tranche. Avoid a single all-features release.

## Validation and Acceptance

Testing-strategy checkpoint:

- Touched surfaces: SDK dependencies/runtime; mobile navigation/accessibility/drafts; public API contracts; PostgreSQL migrations and ownership; durable worker/outbox; AI prompts/artifacts/attempts; push-device associations and notification destinations.
- Risk class: security-critical ownership/anonymity/device association, high persistence/concurrency and generated-content quality, moderate navigation/readability. Use the highest touched risk for each unit.
- Required validation: make format then make check; real PostgreSQL integration tests for persistence/authorization/concurrency; Android/web exports for native/dependency changes; SDK doctor/dependency checks for Unit 0 and notification integration; bounded live provider quality review for new generation kinds; browser flows and rendered checks plus actual phone checks for native behavior. Tests do not call the live provider by default.
- Deferred validation: production load/delivery, production SMTP, full iOS support, sandboxed code execution, OCR/notebook/ZIP ingestion and institution-grade marking. Push is explicitly unverified until real credentials, development build and device delivery exist. Deferral cannot be presented as completion of the affected feature.
- Release smoke required: before a later release, actual sign-in/session restore; authorized PDF; recoverable study job; mixed-anonymity forum reply; one durable inbox event; physical-device notification-to-authorized-destination; sign-out/account-switch device isolation; worker restart/backlog recovery. Release/deployment is outside this planning request.

Risk-specific proofs:

| Unit | Required evidence beyond builds |
| --- | --- |
| 0 | Expo compatibility/doctor, fresh Android+web export; SDK 58 physical phone restore/PDF/quiz smoke still explicitly pending until performed |
| 1 | Browse vs join; member-only PDF refusal; actual section/file scope; course-filtered discussion navigation; screen reader labels and large fonts |
| 2 | Owner-only anonymous history and editing, anonymous public non-disclosure, bookmark/subscription uniqueness, drafts cleared on switch/logout, deleted-thread handling |
| 3 | Transaction rollback emits no event; duplicate write/retry emits one inbox row; worker crash recovery; aggregation without vote farming; no self/muted/blocked/sample alerts; read counts and paging; no actor identity leakage |
| 4 | Version-bound sources; invalid IDs/quotes rejected; private solutions absent from setup/hint/public result; reveal/attempt isolation and idempotence; numeric tolerances tested if enabled; independent written solutions and runnable reviewed labs; unsupported files visibly excluded |
| 5 | Context resolution, concurrent send ordering, duplicate retry, prompt size limit, provider failures, history deletion, permission revocation, cross-account refusal, prompt-injection cases and honest abstention |
| 6 | Counts derived from real attempts; assisted/self-marked work separated; timezone/quiet-hour scheduling and reminder opt-out |
| 7 | Owned token registration, token rotation/revocation, account switch, retry and invalid receipt handling; denied permissions; foreground/background/terminated tap; deleted/unauthorized target refusal; generic lock-screen content |

Commands from repo root: make format; make check; make backend-test for backend changes; make bundle for mobile/native changes. From mobile/: npx expo install --check and npx expo-doctor after SDK/native dependency changes. Follow mobile/AGENTS.md and the target SDK docs before editing any Expo API. New PostgreSQL tests extend backend/tests/UniCompanion.IntegrationTests; pure client/state tests extend mobile/tests. Reuse disposable databases and clean only isolated QA accounts. UI checks cover compact phone widths, large system text, keyboard/Back, empty/loading/failure/success/offline and interrupted/reopened work. Never start an emulator.

## Discoveries and decision log

- 2026-10-23: User prefers both written exercises and coding labs. Plan a shared practice artifact with distinct task kinds; avoid two independent generation platforms.
- 2026-10-23: Current generation schemas only support answer/summary/quiz, PDF text and bounded evidence. Lab capability requires implementation and quality proof, not merely a new button or prompt.
- 2026-10-23: Forum supports anonymous projections, idempotent writes and deletion, but no own-content endpoint, subscriptions or activity events. Preserve the mixed-anonymity OP rule across notifications.
- 2026-10-23: In-app inbox before push is a recommended delivery sequence. Official SDK 58 notification docs confirm Android remote push requires a development build.
- 2026-10-23: User supersedes the earlier SDK 57 preference and explicitly requests SDK 58. Unit 0 is authorized now; other units remain a proposed roadmap.

## Sources

Official documentation checked 23 October 2026: [Expo SDK upgrade](https://docs.expo.dev/workflow/upgrading-expo-sdk-walkthrough/), [SDK 58 reference](https://docs.expo.dev/versions/v58.0.0/), [SDK 58 notifications](https://docs.expo.dev/versions/v58.0.0/sdk/notifications/), [push overview](https://docs.expo.dev/push-notifications/overview/). Source inspection supplies current app facts; recommendations are product judgments, not claims of observed usability failure.

## Completion rule and next checkpoint

Planning is complete when this artifact covers the user's requested AI practice/tutor, notifications, own discussions and current UX gaps with implementable boundaries and evidence requirements. No runtime goal or broad feature implementation is created by writing it. Unit 0 completion requires recorded compatibility/check/export evidence and an honest remaining native gate. At each later authorized unit, update Progress, actual contracts, decisions and validation results here. No estimates or checklist ticks substitute for tested behavior.

Current next checkpoint: user opens the upgraded app with SDK 58 Expo Go and confirms the native smoke. Unit 0 local verification is complete; implement the first selected feature tranche when authorized.

## SDK 58 implementation receipt — 23 October 2026

Installed Expo 58.0.17, React/React DOM 19.3.0, React Native 0.88.0, Router 58.0.25 and matching Expo/native dependencies. The initial in-place npm alignment failed resolving the old Router tree; preserved its lockfile/node_modules in ignored backend/.local/sdk58-upgrade-backup and regenerated a coherent dependency tree without forced or legacy peer resolution. Manifest and lockfile agree. Current Node 22.23.2 satisfies the SDK engine range.

SDK 58 changes the default tunnel to require Expo sign-in. npm start now explicitly uses --tunnel ngrok to preserve the current anonymous tunnel workflow. Root/mobile setup docs and the active phone checklist now target SDK 58; historical SDK 57 receipts are retained. WebView 14 changed its declaration generic default from an empty object to undefined, collapsing component props to never. PdfCanvas now explicitly supplies the object generic for the component and ref; no cast, suppression, or viewer behavior change.

After that fix, make format and make check pass: .NET locked restore/format/build, 9 offline Python tests, mobile formatting/types/lint and 17 mobile tests. Expo install --check reports aligned dependencies; doctor passes 20/20. make bundle succeeds for Android/Hermes and web, including the rebuilt PDF renderer (Android bundle 5.1 MB). Local/tunnel API proxy returns expected 401 for anonymous course reads; direct API readiness returns ready. Backend source and Gemini helper remain unchanged. Backend integration tests were not rerun because no backend behavior/schema changed.

Physical SDK 58 Expo Go session restoration, WebView/PDF, keyboard/Back and quiz acceptance are still pending. User's positive pre-upgrade phone feedback is retained but is not evidence for the upgraded runtime. SDK/development dependency audit reports 21 advisories; no incompatible forced audit fix is part of this compatibility unit. New roadmap features remain planned, not implemented. Browser startup outcome and final service state follow below.

Final browser proof: Chromium at 390×844 loaded the SDK 58 app with HTTP 200 and zero page exceptions; welcome → sign-in navigation reached /auth with both inputs present and zero page exceptions. No account was created or user content changed. This is a startup/navigation smoke, not a repeat of the full authenticated study/forum QA or physical-phone acceptance. The verification Metro server was stopped for the user to run npm start in their own terminal; API/AI worker/database/mail remain running. No emulator, commit, push or deployment.

## Active build-loop — 23 October 2026

Plan state: usable_current_plan → update authorization and unit detail. Baseline 96a68c7 on main/origin/main; working branch feat/student-companion-next. Hook inventory: mobile/AGENTS.md governs Expo; root Makefile owns format/check/backend-test/bundle; root/mobile/backend READMEs own runtime; this plan owns checkpoints. No TeamBoost project, root AGENTS, PR template, mandatory PR/release workflow or production delivery hook governs this personal repo. Use local source review per meaningful unit and preserve current services; no production deployment or emulator. Ordinary local implementation and validation are authorized. Claude posture auto-on-trigger; optional availability does not gate progress.

Mission anchor: complete the scoped local student journeys in Units 1–6, and prepare Unit 7 as far as actual credentials/build capability allows. Preserve authentication, privacy, source access, SDK 58 and approved identity. The source plan and this checkpoint own durable memory; no separate board or runtime goal needed. A unit passes technical checks, runtime checks and rendered quality checks separately. Native device evidence is requested only where the user device is necessary; continue independent work.

Active unit 1: explicit metadata-only course preview and intentional joining; lesson grouping and source/index scope; direct course discussions and clear tool labels; real resume card. Allowed surfaces: courses API DTOs and tests; connected course/study/forum route state and types. Keep current protected course detail/PDF semantics; add authenticated preview metadata only. Do not expose raw source metadata in previews. Validation: real PostgreSQL preview/join/isolation checks; pure grouping/scope tests where meaningful; make format/check; exports; web preview/join/section/filter smoke and rendered inspection. Backend rebuild requires API restart before runtime verification. Worker need not restart for API/UI-only changes.

Pinned regression: outsider can see catalogue preview but cannot fetch detail/PDF; intentional join unlocks original course; chapter scope names included files; unsupported files have an explanation; course Discuss opens filtered feed; existing auth, original PDF, saved study/quiz and anonymous forum regressions remain protected. Unit 1 stop conditions: irreversible/external action or actual source/credential ambiguity; do not pause for routine implementation decisions. Next units follow this document after unit validation/review receipt.

### Unit 1 receipt and transition

Done locally: intentional metadata preview/join, normalized chapter names with upstream private metadata omitted, indexed/page-count resource labels, actual file scope in study setup, course-filtered Discuss, familiar Summary/Quiz/Ask AI labels and a real last/running-session card. Tests: 47/47 real PostgreSQL; make format and final make check pass (9 Python/17 mobile tests, build/types/lint); fresh Android/web export passes. First parallel .NET runs collided on a shared generated manifest; serialized subsequent runs. First lint rejected effect-driven route state; replaced it with a route-driven keyed feed. An export exited 143 without a compiler error; the final rerun passed.

Source review applied code-review to frozen local target; remediated stale null-path resource data and route state. Validated result at /tmp/skill-runs/code-review/mobile-project/local-feat-student-companion-next-20261023-101850Z. No unresolved fix-now finding. Chromium flows passed after the final route edit for explicit join, chapter projection, course discussions and source scope; inspected 390/320 screenshots, no overflow/page exceptions. Private evidence backend/.local/evidence/next. API restarted on 5080, worker and existing user Metro preserved. Two isolated QA accounts remain private for later unit validation and will be cleaned at closeout. No user data changed. Native acceptance remains open.

Active Unit 2: My discussions/replies, owner-scoped bookmarks, thread following, edit/delete/resolved, drafts and basic moderation/blocking. Persistence/API ownership/anonymity is highest risk. Add indexed migrations and real PostgreSQL tests for own-anonymous discovery/non-disclosure, source access, edit/deletion idempotence, blocked visibility and report/admin refusal. Keep creation hashes independent of later edits/deletion; backfill old rows before cutover. Use tombstones for reply continuity. Next Unit 3 adds transactionally committed activity events, an independent dispatcher and preferences; do not publish notification side effects from uncommitted writes. Optional bounded Claude contract check is running.
