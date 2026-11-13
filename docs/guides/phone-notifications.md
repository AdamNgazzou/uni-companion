# Phone notifications

The QR workflow stays available: run `cd mobile && npm start` and scan with SDK 58 Expo Go. Courses, original PDFs, discussions, Activity, tutor, practice and revision work in Expo Go. Phone push is a separate, optional setup.

## Current state

Local push registration, private previews, account/installation fencing, token rotation, logout unlink, quiet hours, durable retry and ticket/receipt handling are implemented. Sending starts disabled. No Expo project, Firebase credentials, cloud build or real phone delivery was created or verified in this workstream.

On Android, remote push requires our own development build. Expo Go does not support this transport. Google delivers Android notifications through Firebase Cloud Messaging (FCM). The Expo project ID identifies this app to Expo’s push service. Neither is a course ID or the GitHub repository ID.

References: [SDK 58 Notifications](https://docs.expo.dev/versions/v58.0.0/sdk/notifications/), [Expo push setup](https://docs.expo.dev/push-notifications/push-notifications-setup/), [sending and receipts](https://docs.expo.dev/push-notifications/sending-notifications/).

## One-time Android setup

Use the actual owner’s Expo account and project. Do not invent a project UUID or paste private keys into chat.

1. Sign into Expo/EAS in your terminal and select or create the intended Uni Companion project. Set its actual UUID in `mobile/app.json` at `expo.extra.eas.projectId`.
2. Register the Android app package `com.adamngazzou.unicompanion` in the intended Firebase project. Download its client configuration as ignored `mobile/google-services.json`; add `expo.android.googleServicesFile` pointing to `./google-services.json` only after that file exists.
3. Configure the matching FCM v1 service-account credential through the EAS credential workflow described in the setup guide. Keep service-account keys out of Git. The client config and uploaded server credential must use the same Firebase project/sender.
4. Set these values in ignored `backend/.env`: `Push__Enabled=true` and `Push__ProjectId=` followed by the same actual Expo project UUID. If enhanced push security is enabled in the Expo project, also set its server-only `Push__AccessToken` there. Restart API and worker with the normal `dev.py serve` / `dev.py worker` commands.
5. Build the prepared development profile from `mobile`: `npx eas-cli@latest build --profile development --platform android`. This is an external build; it was not run automatically. Install the resulting development app on the physical phone.
6. Stop the Expo Go Metro session and run `npm run start:dev`. Open its QR in the development app. In You → Account settings → Phone notifications, explicitly enable and grant OS permission. Grouped upvote push starts off.

Use `npm start` for Expo Go again. Changing push configuration never requires replacing the existing backend database or login system.

## Delivery behavior

The worker and backend must be online to send new notifications. The durable Activity inbox remains authoritative. Permission denial, Expo Go, web, missing configuration or push outages leave Activity usable.

The phone payload contains a generic preview and an opaque activity ID. Opening it fetches an authorized destination from the server. It does not contain PDF excerpts, answers, author identities or an arbitrary URL. Foreground Activity uses its existing unread indicator and refresh; foreground OS banners are suppressed.

Device proof uses SecureStore. The server hashes the installation secret and DataProtects the Expo token. Authenticated installation initialization and persisted command revisions reject late registration after logout and late unlink after a newer account association. If unlink cannot reach the server while offline, the app persists it for retry and displays the pending state; generic alerts may still arrive until unlink succeeds.

Registration expires for sending after 30 days without a foreground refresh. Enabling/rebinding does not replay historical inbox items. Dispatch rechecks access, active account/security stamp, preferences, thread mute and identity-mode blocks. Quiet hours use the saved timezone/window, or device timezone with 22:00–07:00 defaults. Upvotes are independently opt-in and delayed for grouping. Daily reminders remain off until selected.

Send attempts are bounded to 3, with a 2-minute lease and 24-hour delivery budget. Provider receipt checks start after 15 minutes and are bounded; a successful receipt means provider handoff, not proof that a phone displayed it. DeviceNotRegistered retires only the matching token binding. Lost network responses can duplicate an OS alert; delivery is not exactly-once.

## Physical acceptance still required

Use two controlled accounts on the development build. Check reply and completed-study alerts, opt-out/mute/quiet hours, token changes and account switching. Open notifications while the app is foregrounded, backgrounded and terminated. Verify sign-out/pending offline unlink, wrong-account destination refusal, unread counts and permission denial. Confirm actual receipt outcomes without logging tokens or private content. Expo Go/browser tests and Android exports do not prove this gate.
