# Proposed implementation objective

Status: implementation authorized 2026-10-21; durable mission anchor, not a runtime-tool goal. Follow the existing plan in the current checkout.

Build the local Uni Companion backend and connect the existing mobile app according to docs/plans/backend-ai/backend-ai-execplan.md. Deliver real accounts, authorized courses/materials, grounded cited study assistance, summaries/quizzes/attempts, and course communities with anonymous posting, votes, and moderation. Preserve the approved mobile design.

Workspace: current checkout at /home/lenovo/projects/mobile-project; one repository with mobile/ and backend/. Preserve existing uncommitted files and the ignored university dataset. No separate worktree or emulator.

Use $build-loop, $execution-planning, $testing-strategy, and $code-review. Work phase by phase, keeping the plan and validation receipts current. Start with the provider/toolchain proof, then foundation, real auth, idempotent import, evaluated retrieval, study features, community, and mobile integration.

Keep backend/ai/gemini.py exactly as supplied. Add no Gemini API key, cookie, upstream session, or replacement provider. Put parsing/error handling outside the helper. Separate app authentication from the upstream generation protocol. Use .NET 10, Identity, real PostgreSQL integration tests, private files, and server-side authorization. Imported Python files are data only.

Done means the plan's auth/isolation, import/versioning, worker recovery, bridge-contract, anonymity, and fixed retrieval evaluation checks pass; the 12-course/35-attachment import is reproducible; and real Expo Go phone flows preserve the approved design. Record actual commands and human/runtime evidence. Explicitly retain pending phone/provider validation rather than calling an unproven integration complete.

Autonomy: this draft anchors the future local build, not deployment or publication. Once the user starts implementation, carry out necessary reversible local edits/checks without repeated routine permission requests. Pushes, PRs, merges, and deployments are outside the current request. If a required provider behavior fails, report concrete evidence without changing its locked credential constraints. Preserve progress and name the next useful checkpoint when external evidence is pending.
