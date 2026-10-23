# Community demo and connected forum

## Purpose and scope
User requested sample users and posts to simulate an active forum. Discuss currently shows a placeholder; old unconnected prototype has the approved post/vote/avatar design. Implement a persisted campus feed, thread/replies, up/down/remove votes, profile/anonymous posting, and an explicit development seed with fictional users/content. Keep mobile/ and backend/ in this repo, existing blue/orange/Nunito identity, SDK 57, authentication and immutable Gemini helper. No emulator, production deployment, signup email spam or real-user impersonation.

## Decisions
- All authenticated accounts currently belong to Horizon; community discussions are campus-visible, including course labels. This does not grant access to private course materials.
- Fictional identities are disabled, unconfirmed passwordless Identity rows. No public demo passwords. Seed posts/replies carry a visible Sample badge and the feed labels sample activity. New real-user contributions are not marked sample.
- A single post model supports top-level threads and one-level replies. Votes are per-account/per-post, unique, atomically set to -1/0/1. Anonymous DTOs never contain author IDs/names; original-author replies get an OP label only within that thread.
- Seed command is development-only, deterministic/idempotent, adds content without changing real accounts/posts or imported courses.

## Plan of work
1. Add community entities, indexed migration, authenticated bounded API and deterministic development seed command.
2. Replace Discuss placeholder using existing visual vocabulary: course filter, Recent/Top, post kinds, compact identity/meta, thread text and vote/reply bar. Add thread and composer routes, anonymous mode, empty/loading/error states.
3. Run PostgreSQL regression tests for anonymous projection, ownership/auth, vote replacement/concurrency, content validation/idempotence and seed repeatability. Apply migration and seed local database; restart only this API as needed.
4. Run format/check, backend tests, Android/web exports; inspect real rendered feed/thread/composer at 320/390/desktop, test posting/replying/voting/filtering/persistence and remove only QA contributions. Leave user test services running.

## Validation checkpoint
Touched surfaces: persistence, API/authenticated DTOs, seed identities, Expo Router and connected UI. Highest risk: identity privacy and persisted writes. Required: real PostgreSQL tests including refusal/anonymous identity non-disclosure, unique atomic votes, authorized deletion, bounded validation, repeat-safe creation/seeding; format/build/lint/types; web interaction and rendered checks; exports. Deferred: physical Samsung/native font/keyboard/back behavior, moderation/reporting, nested replies, attachments and notifications. No release smoke/deployment is requested.

## Progress
Discovery complete. Skills: execution-planning/testing-strategy; Impeccable for extending the approved forum surface (existing prototype and design/visuals-v2/03-community.png authority). Existing session context already loaded; preserve design system. SDK57 and Router navigation docs checked. Implementation active.

Implementation checkpoint: Community migration applied locally; development seed succeeded with 8 disabled/passwordless/unconfirmed fictional identities, 10 threads, 26 replies and 178 votes. Sample badge/banner separate seed content from real contributions. API and Expo Router feed/thread/composer are connected; existing shared visual components/tokens and original forum prototype vocabulary are retained. Existing API was restarted to expose routes; worker/Metro remain running. No emulator.

Real PostgreSQL suite initially found DTO projection ordering that EF cannot translate; ordering/pagination now operate on the entity query before the public projection. A nested-reply refusal test initially omitted the required idempotency header; fixed its request so it tests the intended parent refusal. Subsequent suite passed 46/46. Added further course/kind/sort/pagination and private-material isolation checks, concurrent creation and mixed-anonymity OP regression proofs; final rerun pending below.

Browser QA passed with isolated real signup/confirmation/login: seed feed, Recent/Top, combined ML/kind filters, original thread/replies, vote persistence/flip/removal, a real anonymous reply, a profile post surviving refresh, and anonymous OP replies. No page exceptions. Feed inspected at 320/390/1366 with no horizontal overflow; thread/composer/sample identity states inspected. One harness locator selected a retained hidden feed's anonymous label; selecting the current thread's latest match fixed the harness, with no app workaround. Screenshots/report remain private under backend/.local/evidence/community. Finish source review found OP badges could link anonymous and named contributions; projection now emits OP only when root/reply anonymity mode agrees.

## Final review and validation
Scoped source review followed the code-review skill: entities/migration, public projection, authenticated reads/writes, creator/owner controls, atomic votes, repeat-safe creation, deterministic disabled identities, seed command guard, mobile routes/forms and cleanup. The review helper cannot freeze a git target because the repository still has an unborn HEAD; recorded its git rev-parse HEAD failure rather than inventing a baseline. Reviewed the changed source files directly. EF ordering and mixed-mode OP linkage findings are remediated and regression-tested. No unresolved fix-now finding in this scoped local demo. This is not a claim of production moderation readiness.

Final real-PostgreSQL suite passed 47/47, including concurrent idempotent creation, atomic votes/flip/remove, content limits, anonymous projection and mixed-mode OP privacy, auth/ownership/refusal, course/kind/sort/pagination, unchanged private-material access and repeatable seed identities. make format/make check pass (backend build/format, 9 Python tests, mobile format/types/lint and 17 mobile tests). Live browser flow passed seven scenario groups with zero page exceptions. Sample counts verified directly after QA cleanup: 8 users, 10 threads, 26 replies, 178 votes. Deleted only isolated forum-qa account and its temporary posts/reply/votes; removed its private credential file. User accounts/study jobs/imports remain untouched. Gemini helper hash remains cc6120d2bec42986ec13be5b92655bedcd35bf23ccade0f9a80b29ead8942630.

API was restarted on localhost:5080 with the final privacy projection; readiness is healthy. Metro on localhost:8081 and worker/database/mail stay running. Android/web exports pending completion in the last line below. Actual Samsung keyboard/back/font scaling/native acceptance, moderation/reporting tools, nested conversations and attachments remain deferred. No emulator, commit, push or deployment.

Final export complete: make bundle successfully exported Android/Hermes and web after the final UI changes. All authorized local community/demo work is complete. Refresh the running app and open Discuss; no new startup command is needed in this session. Native device acceptance remains unverified.
