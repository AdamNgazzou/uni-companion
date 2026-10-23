# Uni Companion mobile prototype

## Purpose / Big Picture

Build the endorsed playful React Native app for one university, using local sample data and no backend. Students can study sample notes, complete quizzes, join or create courses, post discussions anonymously or with their profile, vote, and save content. Admin-curated courses and recognised student courses remain distinct.

This is the mission anchor. Current unit: SDK 57 compatibility migration complete; next checkpoint is the user's Expo Go phone walkthrough. Phone testing happens afterward with the user. Never start an emulator again for this unit.

## Context and Orientation

- Repository: `/home/lenovo/projects/mobile-project`; remote `https://github.com/AdamNgazzou/uni-companion.git`.
- New repository; no prior AGENTS.md, Makefile, tracker, PR policy, testing guide, or release hooks. The plan supplies lightweight checkpoint memory. No TeamBoost workstream governs this university project.
- Mobile application: `mobile/`, Expo SDK 57 (migrated from the initial SDK 58 template), React Navigation, AsyncStorage, Nunito, Lucide, Expo document picker/file system/sharing/haptics.
- Visual authority: `design/visuals-v2/` and `design/visuals-v3/`; original orange character asset in `mobile/assets/mascot.png`. Early ivory/serif concepts are rejected.
- Explicit boundary: no .NET implementation, live AI, actual account authentication, email delivery, remote file sharing, publishing, pushes, or PRs in this local prototype tranche.
- User asks for execution-planning, testing-strategy, and build-loop. Their TeamBoost/FastAPI specifics do not apply to this React Native repository. Adapt the validation floor to this stack using repo-owned make targets.

## Progress

- [x] Approved visual direction and screen inventory established.
- [x] Expo scaffold and mobile dependencies installed.
- [x] Theme, shared components, local storage, seeded course/post data added.
- [x] Account/onboarding forms, course creation, reader, summaries, sample Q&A, quizzes/results implemented in source (not yet validated).
- [x] Discussion feed, threads/replies, voting, anonymous composer.
- [x] Profile, saved items, settings, admin recognition preview.
- [x] Root stack/tab navigation, app configuration and handoff docs.
- [x] Finished implementation before running validation, per user instruction.
- [x] Format, TypeScript, state-transition tests, Expo compatibility and Android/web bundle checks.
- [x] Review and fix actionable code findings.
- [ ] Expo Go phone walkthrough with the user (deferred until code is complete and user is ready).

## Decision Log

- 2026-10-21: React Native/Expo frontend only; .NET integration deliberately deferred.
- Admin creates curated courses; students create labelled student courses; admin can recognise student courses without changing their authorship.
- Use persistent local data; never persist passwords. Account forms are explicit previews. Demo admin role is local UI exploration, not a security boundary.
- Imported PDFs are stored on Android; browser preview stores metadata only. Imported files receive no fabricated AI summaries. Prewritten sample material demonstrates study tools.
- No emulator: user requested it closed due to RAM. Do not wait for Expo Go to finish coding. Device testing remains a distinct later checkpoint.
- Plan-state decision: create, since previous design notes were not an execution plan.
- Claude posture: auto-on-trigger; no material unresolved architecture fork currently calls for a second opinion.

## Surprises & Discoveries

- create-expo-app currently scaffolds SDK 58 / React Native 0.88. Dependencies must match the installed Expo SDK and the phone's Expo Go support.
- Two overlapping dependency installs caused ENOTEMPTY. A subsequent sequential install completed. Run compatibility/dependency checks after implementation.
- Android emulator was available and started for verification, then explicitly stopped at user request. No screenshots from it establish app quality.
- TypeScript 6 required an explicit Node types environment for the test runner. Combining Node and native app environments overflowed the compiler stack; separating app/test tsconfigs resolved it. Both now pass.
- The code-review helper requires an existing HEAD, absent in this new repository. Direct source review was used and its findings/recheck recorded in `review-receipt.md`; no commit was invented solely to satisfy a helper.

## Plan of Work

1. Complete social interactions and personal/admin screens, keeping state in StoreProvider and static demo content separate.
2. Connect screens using native-stack and bottom tabs, add Nunito loading, native safe areas, keyboard handling, app branding, and local-data error states.
3. Add minimal runtime/setup docs, pinned regression cases, and Makefile wrappers for the actual frontend commands.
4. Once source is complete: run `make format`, `make check`, Expo dependency verification and bundle checks. Fix failures at their source.
5. Review changed code against local persistence, anonymity, navigation, and quiz state risks. Keep the source locally; no external publication is required.
6. Hand off one Expo Go startup command with explicit native testing limitations and the next manual phone checkpoint.

## Validation and Acceptance

### Testing-strategy checkpoint

- Touched surfaces: React Native screen/navigation code, local AsyncStorage state, imported Android PDF files, sample auth/AI UI, local course provenance/recognition, posts/votes/quizzes.
- Risk class: moderate local persistence/state/navigation; no production authentication or network permission boundary exists.
- Required validation after coding: formatting, TypeScript, meaningful vote/anonymity/quiz-state tests, dependency compatibility, Expo Android and web bundles, static/code review.
- Deferred validation: actual Expo Go phone walkthrough, Android file picker/share behaviour, keyboard/font-scale/gesture behaviour, native screenshots and visual parity. User explicitly deferred device testing.
- Release smoke required: no release/deploy in scope. Phone smoke is required before calling the app operationally or visually verified.

### Pinned regression journey

Explore demo -> Database Systems -> Normalization summary -> page citation -> quiz -> correct/incorrect feedback -> results -> missed-question retry. Create a student course -> inspect Student-created label -> enable local admin preview -> recognise course -> inspect retained author label. Community -> thread -> anonymous reply -> vote up/down/remove -> save -> saved list -> restart -> confirm persisted changes. Register/verify uses displayed sample code; no credentials leave the app.

### Done model and stop rule

- Technically complete: all scoped screens are connected, local interactions work by code/build evidence, and required code checks pass.
- Operationally validated: remains pending until a phone walkthrough succeeds.
- Quality-bar complete: remains pending until rendered native screens are inspected against the endorsed visual concepts.
- Stop after a technically complete, reviewed local handoff, with phone verification explicitly pending. Do not deploy, publish, push, create a PR, start an emulator, or claim native visual verification.

## Runtime contract

From `mobile/`, install with `npm ci`, then run `npm start` (ngrok tunnel) when ready to pair the phone. Use Expo Go's QR scanner. Tunnel is the intended WSL-to-phone path; keep Metro stopped while only writing code. No API process or environment variables are needed. `README.md` records the setup path and Expo SDK 57 client requirement and cache-clear instructions.

## Receipts

- Implementation checkpoint: auth, courses, study source added; social/personal/navigation still missing. No validation claim. Dependency install repaired. Emulator closure issued; user instructed no further emulator activity.
- Implementation complete: account/onboarding, courses/provenance/recognition, imports/reader/study previews, quizzes, social interactions, profile/saved/settings and root navigation are written. No backend connected.
- Validation: `make format` and `make check` pass, including both TypeScript environments and 3/3 state-transition tests. `npx expo install --check` reports compatible dependencies. `npx expo-doctor` passes 20/20 checks. Final Android/web export exits 0; Android Hermes bundle 4.5 MB and web bundle 3.3 MB in ignored `mobile/dist/`. Only the four used Nunito font weights are bundled.
- Local review: findings and recheck at `review-receipt.md`. Nested press propagation fixed; no further concrete fix-now issue identified in direct source review. Screenshots/device review not performed.
- Handoff: setup and limits at `README.md`; current-good code baseline is this local tree after the final checks/export. No commit, push, PR, deploy, emulator or long-lived Metro process is part of the handoff.

## Outcomes & Retrospective

Technically complete as a local prototype by source, type/test and bundle evidence. The next checkpoint is the user's Expo Go phone walkthrough using the pinned journey above. Operational and native visual acceptance remain pending. Do not equate bundle success with a working device test. No additional feature unit is opened; no remote delivery is required for this tranche.

Backend follow-ups are intentionally separate: authentication and university membership enforcement, document processing, actual PDF rendering strategy, source-grounded AI, remote course/material permissions, accountable anonymity, reports/moderation, notifications and dark theme. Dependency installation also reported inherited npm audit findings; a release-specific dependency/security pass remains outside this local-only prototype acceptance.

### Phone setup checkpoint

- User reached Metro through `npx expo start --tunnel ngrok`; default signed tunnel requires an Expo login. Updated npm start to use the proven ngrok path.
- Fresh store Expo Go rejects SDK 58. Official SDK 58 APK was identified, but the user prefers SDK 57 compatibility with their installed client. Supersede the APK setup path: migrate the project and matching dependencies to SDK 57.
- Phone app rendering remains unverified until that client opens the project.

### SDK 57 compatibility unit

- Scope: package manifest/lock, any source changes required by SDK 57, npm startup command, README, and this plan. Preserve the endorsed screens and local-only behaviour.
- Risk class: runtime/dependency compatibility; no new backend or production security boundary.
- Required validation: make format/check, Expo dependency check/doctor, Android and web exports. Phone acceptance remains deferred.
- Sequence: read SDK 57 bundled dependency versions, install the complete aligned set together, repair concrete API/type issues if any, validate, update handoff.
- Stop condition: verified local SDK 57 package/bundle baseline; no emulator, APK sideload requirement, publishing or backend work.

### SDK 57 completion receipt

- Installed Expo 57.0.30, React/React DOM 19.2.3, React Native 0.86.3 and SDK-compatible native packages/React types. No screen source changes were required.
- `make format` / `make check`: pass, including both typechecks and 3/3 tests. `expo install --check`: compatible. Expo Doctor: 21/21 pass. Lockfile matches the manifest.
- Fresh SDK 57 Android/web exports pass; bundles are 4.4 MB / 3.2 MB. Reviewed dependency and CLI changes; no fix-now finding remains in this scope.
- SDK 57 CLI uses standard ngrok `--tunnel`; canonical restart is `cd mobile && npm start -- --clear`. README updated; SDK 58 sideload instructions superseded.
- Current-good code baseline is this SDK 57 local tree. No emulator, backend or long-lived Metro server was started. Native operational/visual acceptance is still pending the phone scan.
