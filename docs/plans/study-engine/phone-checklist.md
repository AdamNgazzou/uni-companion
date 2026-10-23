# Expo Go acceptance — pending physical device

Use the user's Samsung with Expo Go for SDK 58. No emulator. Record the phone/Android/Expo Go versions and actual results; desktop web evidence does not check these boxes.

From /home/lenovo/projects/mobile-project:

```bash
python3 backend/scripts/dev.py up
python3 backend/scripts/dev.py migrate
```

Keep three terminals open:

```bash
python3 backend/scripts/dev.py serve
python3 backend/scripts/dev.py worker
cd mobile && npm start
```

Scan the QR. Keep worker/API running during generation. Confirmation/reset mail is at http://localhost:8025 on the computer. Open the actual confirmation link there; for phone-openable email links, restart the API with Accounts__PublicBaseUrl set to the current HTTPS Expo tunnel origin. See mobile/README.md for connection options.

- [ ] Register a new account with a 12+ character upper/lowercase/number/symbol password. Follow the real confirmation email; sign in. No demo code is accepted.
- [ ] Close/reopen the app. The encrypted refresh token restores the same account, without requiring another login or displaying another account's data.
- [ ] Learn → Discover → Machine Learning. Joining opens its actual imported resources. Return to My courses and verify membership persists.
- [ ] Introduction to ML → Read the original. Verify physical pages 1/2 and Next/Previous; try extracted page text and return to the original PDF.
- [ ] Ask: “What is the difference between classification and regression?” Open a citation and confirm the actual PDF page supports the answer.
- [ ] Start a summary, close the app before completion, reopen → You → Study history. The same job/result resumes; opening a saved result does not regenerate it.
- [ ] Generate five English questions, answer all, submit and inspect the server's grade, explanations and original-page links. Retry what you missed; original correct answers carry forward and the final score covers the full quiz.
- [ ] Generate ten French questions and review their language/layout. Verify longer options wrap and font scaling stays usable.
- [ ] Android Back/tab navigation, keyboard avoiding fields, scrolling, reading, small display and larger system font sizes remain comfortable with the approved design.
- [ ] With internet/backend unavailable, errors and retry controls are visible. A temporary refresh transport failure must not discard saved credentials; no fabricated demo result appears.
- [ ] Cancel a running session; it remains cancelled in history. Stop/restart the worker during another session; the same job recovers without duplicate published results.
- [ ] Sign out; register/sign in as a second account. First account's histories/results/PDF state are absent. Sign back in to the first account and recover its own server history.
- [ ] Exercise password reset with the real emailed code and password change. Old sessions are invalidated, and the new password works.
- [ ] Discuss/uploads show their real unavailable state and do not pretend that a post/upload succeeded.

Stop API, worker and Metro with Ctrl+C when finished. Stop this project's containers without deleting data:

```bash
python3 backend/scripts/dev.py down
```

Follow-up study polish acceptance (physical phone still pending):

- [ ] Zoom a PDF to 200%, pan sideways, return to Fit width and follow a citation to its original page.
- [ ] Choose answers on two quiz questions, close/reopen and verify choices/position restore; complete and retry missed questions.
- [ ] Sign out/switch accounts: no prior account draft or study artifact appears.
- [ ] Read a new headed summary, expand supporting excerpts, and start practice using the same resource/language. Check 320px-equivalent layout and larger system font settings.

Browser-only follow-up: refresh keeps the cookie session and unfinished quiz position. Logout must prevent restoration. The browser session uses the same-origin API proxy and never exposes an auth token to JavaScript storage.

Community follow-up (local backend seeded; browser verified):

- [ ] Open Discuss, filter a course and Question/Study tip, switch Recent/Top, and read a sample thread.
- [ ] Upvote/downvote/remove a vote, leave/reopen the screen and verify its state.
- [ ] Write a profile post and an anonymous reply. Check keyboard, submit button, Back, longer messages and larger system fonts on Samsung.
- [ ] Anonymous content hides names; OP labels only connect contributions with matching anonymity modes. Sample labels identify fictional activity; your own contributions are persisted real content.
