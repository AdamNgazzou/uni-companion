# Uni Companion mobile

The connected app uses Expo SDK 58 and Expo Router. Approved design tokens/components remain in src/theme.ts and src/ui.tsx. Connected code lives in src/api, src/auth, src/features and src/app. The old src/screens/store/data prototype is retained as reference source and is not a login fallback or part of the connected app's route tree.

From the repository root, start local infrastructure and migrate once:

```bash
python3 backend/scripts/dev.py up
python3 backend/scripts/dev.py migrate
```

Then use three terminals:

```bash
python3 backend/scripts/dev.py serve
python3 backend/scripts/dev.py worker
cd mobile && npm start
```

Scan the QR with Expo Go for SDK 58. No emulator is needed. npm start uses Expo’s --tunnel ngrok option, which preserves the existing ngrok connection without requiring an Expo account login. SDK 58’s default tunnel requires Expo sign-in. Metro proxies only /api/v1 to http://127.0.0.1:5080, so phone API calls travel through the same Expo tunnel. The app derives this origin from Expo's hostUri. For another trusted backend, set EXPO_PUBLIC_API_URL to its HTTP(S) origin before starting Expo; restart Metro after changing it. This development proxy is not a production hosting setup.

Development emails are delivered to http://localhost:8025 on the computer. Register in the app, open its confirmation mail there, follow its link, then sign in. For confirmation links that can open on the phone, set Accounts__PublicBaseUrl to the current reachable HTTPS Expo tunnel origin when starting/restarting the API. The mail inbox is never exposed through Metro. Password reset uses the actual code in the reset email; there is no fake 123456 code.

Native refresh tokens are stored only in SecureStore; access tokens and PDF bytes stay in current-session memory. Web uses a persistent HTTP-only, SameSite=Strict application cookie through the same-origin API proxy; refreshing restores the account without storing credentials in JavaScript. Cookie writes require an allowed Origin. Production requires HTTPS and Accounts__WebOrigin set to the web origin. A SecureStore failure is visible and never falls back to AsyncStorage. Server jobs continue when the app closes; Study history reloads them after sign-in. Signing out clears the current device session; the backend also supports explicit logout-all. Password changes invalidate existing sessions. Unfinished quiz choices and question position are saved on the device for up to seven days, scoped to the account and quiz/retry; signing out clears these drafts. Drafts contain no answer keys or credentials. Browser previews must use the same origin for the UI and /api/v1 proxy; EXPO_PUBLIC_API_URL is intended for native transport overrides.

The reader bundles patched PDF.js 6.4.299 locally in assets/pdf-viewer.html. It downloads private PDF bytes through the authenticated client, uses an isolated WebView, and selects physical PDF pages. No token appears in viewer URLs, messages or HTML. Extracted page text is available separately for accessibility. Rebuild the asset after renderer changes:

```bash
node scripts/build-pdf.mjs
npm run format
npm run typecheck
npm run lint
npm test
npx expo install --check
npx expo export --platform android --platform web --max-workers 1
```

Native phone acceptance remains required for SecureStore capacity, tunnel transport, WebView PDF rendering/page selection, Android Back, keyboard/font scaling and background/reopen recovery. Desktop web rendering and exported Android bundles do not prove those device behaviors. The Discuss tab connects to persisted threads/replies/votes with profile/anonymous posting and labelled fictional sample activity. Student uploads, reporting/moderation tools and notifications remain later features.

Study notes use topic headings (newly generated summaries), compact page citations and expandable supporting excerpts. History names the course and resource. The original PDF reader supports fit-width, 100–300% zoom and horizontal panning; source links still open physical PDF page numbers.

Discuss includes campus/course and kind filters, Recent/Top ordering, thread/reply screens and a composer with explicit identity choice. Anonymous posts/replies hide names and IDs from other students. OP markers never link anonymous content to named contributions. Votes are saved on the backend and clicking the same direction removes the vote. Only the development seed creates fictional users; your own contributions are real persisted content.

You → My discussions lists your threads and replies, including your own anonymous contributions privately. My replies names the conversation and opens the correct reply page. Bookmark threads/resources to find them under Saved items. The thread’s Follow/Mute controls govern its future activity alerts. Your own post options allow editing or confirmed deletion; deleted text becomes a placeholder while other replies remain. Posting identity stays fixed during editing. Drafts restore on the same device and are cleared on sign-out.

Post options also allow reports and hiding a participant. Named and anonymous contributions stay separate when hidden, preserving identity privacy. Undo hiding in Settings → Hidden participants. Administrators can review reports from their profile. Helpful reply labels mean the asker selected that reply; they are not independent factual verification.

Activity inbox: replies in followed threads and completed/failed study sessions appear under the bell on Learn and You. All/Unread, read state, live destinations and four saved in-app preferences are available; grouped upvotes are opt-in. The worker dispatches activity independently of generation. Phone push delivery is still pending its development build and credentials.

Written exercises & coding labs are available from a joined course or PDF. Choose an indexed PDF, written/coding/mixed, guidance, English/French and an estimated duration. Hints, explicit solution reveal, saved attempts and local drafts survive reopening. The first version supports reasoning/scenario writing and Python 3 standard-library labs run on your own computer; pasted code is stored as text, with no automatic or official grade.
