# University Course Companion

<!-- impeccable:product-schema 1 -->

## Platform

android

Android is the initial planning assumption from the school project brief; iOS support remains open.

## Stack

- React Native mobile application.
- .NET backend.
- Backend uses PostgreSQL and .NET 10; hosting remains undecided.
- AI generation must use the supplied backend/ai/gemini.py unchanged; see docs/plans/backend-ai/backend-ai-execplan.md.

## Users

Students at the user's university. The first version serves that university only.

## Product Purpose

Combine course study tools with course discussions: students can study summaries, take quizzes, and discuss course material together.

## Capabilities and Constraints

- Course companion with summaries and quizzes.
- Reddit-style threads, comments, upvotes, and downvotes.
- Users can participate with their profile or anonymously.
- Built by one developer, with no fixed deadline.
- React Native implementation is authorized. The mobile app is now connected to the local backend; phone testing happens in Expo Go after coding. Do not start an emulator because of RAM constraints.
- AI-backed study sessions provide grounded answers, headed summaries and server-graded quizzes with original-page citations.
- The admin curates university courses. Students may create labelled courses with visible authorship; the admin can recognise those courses without changing who created them.
- Community now persists campus/course threads, one-level replies and votes. Server responses hide anonymous names/IDs and avoid OP identity linkage across anonymity modes. Development-only sample activity is explicitly labelled; own-content history, bookmarks, edit/delete, follow/mute, helpful replies, scoped hiding and basic reporting/admin review are implemented locally. Enrollment verification, uploads and notifications remain later work.

## Brand Commitments

The mobile interface must be carefully designed and polished. Generated visual concepts should precede implementation discussions. No final name or visual identity has been selected.

The user rejected the initial ivory, forest-green, serif editorial mockups as generic and AI-looking. Do not carry those forward. The user endorsed the subsequent playful direction and authorized expanding it across the app: cobalt blue, orange star character, white backgrounds, rounded sans-serif typography, tactile buttons, and expressive quiz feedback. Duolingo is a quality and personality reference; Uni Companion has its own identity. Individual layouts remain exploratory.

## Evidence on Hand

The Expo SDK 58 mobile app and playful design are approved and connected to the .NET backend. A private import has 12 courses and 35 attachments, including 27 PDFs, 398 text pages and 814 indexed passages. Retrieval source recall remains 10/12 with honest abstentions. Real accounts, private materials, durable study sessions, quizzes and community persistence are implemented locally; production hosting and physical-phone acceptance remain pending. The community development seed adds eight disabled fictional identities, ten threads, 26 replies and 178 votes without modifying real users. Current execution evidence: docs/plans/study-engine/study-engine-execplan.md and docs/plans/community/community-execplan.md.

## Product Principles

- Connect studying and discussions around courses.
- Keep the scope practical for a solo developer.
- Treat design quality as a core requirement.
