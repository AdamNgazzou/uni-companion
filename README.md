# Uni Companion

A university course companion built with React Native / Expo SDK 58, a .NET 10 API and worker, PostgreSQL/pgvector and a private Python AI bridge. The connected app provides real accounts, course membership, private original PDFs, grounded Q&A, saved summaries, generated quizzes, server grading and attempt history. Its approved cobalt/orange identity, Nunito typography and star mascot remain in the shared native components.

## Run locally

Backend prerequisites and first-time setup: [backend/README.md](backend/README.md). The authorized course snapshot and private configuration are ignored by git and already present in this workspace.

From the repository root:

```bash
python3 backend/scripts/dev.py up
python3 backend/scripts/dev.py migrate
```

Then run these in separate terminals:

```bash
python3 backend/scripts/dev.py serve
python3 backend/scripts/dev.py worker
cd mobile && npm start
```

Use Node.js 22.13+ and the local `npx expo` CLI; install mobile packages with `npm ci` on a fresh checkout. Scan the QR with Expo Go for SDK 58. No emulator is needed. Metro's ngrok tunnel also forwards the app's /api/v1 requests to the fixed loopback API. Development confirmation/reset emails arrive at http://localhost:8025 on the computer. Register, follow the real confirmation link, then sign in. See [mobile/README.md](mobile/README.md) for phone-reachable confirmation links, API origin configuration and device checks.

Study jobs require the separate worker. Closing the app leaves jobs running; reopen them through Study history. Saved artifacts remain tied to the original source versions, with an earlier-version label when materials change. Correct quiz choices and explanations are only returned after the server accepts an attempt. Tokens are not shared with the AI helper; native refresh tokens stay in encrypted SecureStore. Web refresh restores an HTTP-only cookie session. Unfinished quiz choices resume on the same device and are cleared on sign-out.

## Current product scope

- Learn: discover/join courses, read original PDFs, ask questions, summarise a document/chapter and practise 5/10-question English/French quizzes.
- Study: readable notes with topic headings, expandable source excerpts, contextual history, and PDF zoom/pan.
- You: edit your name/password, revisit study sessions and quiz corrections, retry missed questions, find your threads/replies and bookmarked resources/discussions.
- Discuss: persisted campus/course threads, replies, up/down/remove votes, Recent/Top and kind/course filters, plus profile or anonymous posting. Fictional sample content is labelled. Follow/mute threads, edit/delete your contributions, select a helpful reply, report content and hide participants. Basic report review is admin-only. Uploads and notifications remain later work.
- Admin: course import/bootstrap and recognition are backend operations. There is no connected admin impersonation switch, fake verification code or demo-content fallback.

Admin curated identifies admin-created courses. Recognised identifies admin-reviewed student content while retaining authorship; it does not mean university accreditation. Initial registration assigns Horizon University without enrollment verification.

The original prototype source is retained under mobile/src/screens, store.tsx and data.ts for reference. Expo Router's connected route tree does not use it.

## Layout

```text
mobile/src/app/              Expo Router routes and protected layouts
mobile/src/features/         Connected account, course and study screens
mobile/src/api/              Typed client, session fencing and polling
mobile/src/auth/             Real session provider
mobile/src/pdf/              Isolated original-PDF rendering
mobile/src/ui.tsx            Approved native components
mobile/src/theme.ts          Design tokens
backend/src/UniCompanion.Api/ HTTP contracts and account endpoints
backend/src/UniCompanion.Core/ Domain records and generation ports
backend/src/UniCompanion.Infrastructure/ PostgreSQL, jobs, retrieval, storage and adapters
backend/src/UniCompanion.Worker/ Durable generation executable
backend/ai/                  Immutable supplied gemini.py and private wrappers
backend/tests/               PostgreSQL/API and offline Python tests
docs/plans/study-engine/     Current execution plan and validation evidence
design/                     Endorsed visual references
```

## Validation

```bash
make format
make check
make backend-test
make bundle
```

On this RAM-limited local database, use `DOTNET_PROCESSOR_COUNT=1 make backend-test` to avoid overlapping fixture migrations; explicit concurrency tests still exercise simultaneous requests.

Checks include locked .NET restore/build/format, offline Python contracts, mobile format/lint/types and focused client tests. Backend integration tests use disposable real PostgreSQL databases; they never migrate the development database. Bundle exports Android/web without launching a device. `npx expo-doctor` and `npx expo install --check` validate SDK dependencies.

Real generation/source review and desktop web smoke are separate from deterministic tests. Physical Expo Go acceptance remains required for native SecureStore, tunnel/PDF compatibility, keyboard, Android Back, font scaling and background/reopen behavior. There is no production deployment. Stop API/worker/Metro with Ctrl+C and this project's containers with `python3 backend/scripts/dev.py down` to free RAM without deleting data.

For the local sample community (already seeded in the current development database):

```bash
python3 backend/scripts/dev.py migrate
python3 backend/scripts/dev.py seed-community
```

The development-only command adds eight disabled, passwordless fictional student identities, ten sample threads, 26 replies and 178 votes. It is repeatable and leaves real users/content unchanged. Open Discuss while signed in to explore or add real replies/posts. Sample identities are not login accounts.

Activity inbox: replies in followed threads and completed/failed study sessions appear under the bell on Learn and You. All/Unread, read state, live destinations and four saved in-app preferences are available; grouped upvotes are opt-in. The worker dispatches activity independently of generation. Phone push delivery is still pending its development build and credentials.
