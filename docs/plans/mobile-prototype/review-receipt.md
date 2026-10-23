# Local code review receipt

Target: uncommitted initial mobile implementation, including untracked source. No commits or PR exist. Reviewed against the accepted local-only prototype scope and the testing-strategy checkpoint in the execution plan.

The shared structured-review helper could not freeze an unborn Git HEAD (`git rev-parse HEAD`). Review proceeded directly against the current source with the shared code-review contract. No external review or native screenshot review is claimed.

## Findings and disposition

1. **Medium / bug / fix now:** nested bookmark controls could propagate press events into saved-item navigation. `mobile/src/ui.tsx` now stops propagation in IconButton before invoking the action. This prevents unsaving a material from also opening its reader.
2. **Medium / test gap / fix now:** newly added test files were not covered by an appropriate Node type environment. Separate app and Node-test tsconfigs now pass. Tests exercise vote direction/removal, anonymous original-author labels, and original-question ids through quiz retries.
3. **Accepted scope limitation:** account forms, admin permissions, email, reports, and AI are clearly local previews. They do not claim a production authentication/privacy boundary. No network API is integrated.
4. **Deferred to the explicit phone checkpoint:** native layout, keyboard behaviour, screen-reader interaction, file picker/sharing, gesture navigation and font scaling. Code/bundle success does not establish these properties.

## Recheck

The changed event handler, vote transitions, anonymous label handling, course origin/recognition distinction, quiz result storage/retry ids, native stack/tab registration, PDF path scope and local-data reset were reviewed after fixes. No additional concrete fix-now issue was identified in this code pass. Phone acceptance remains open.

Formatting, app/test TypeScript and all three focused tests pass. Expo Doctor reports 20/20 checks and the dependency version check reports compatible packages. Android/web bundle evidence is recorded in the execution plan after the final export completes.

## SDK 57 compatibility review

Reviewed the dependency-only migration against SDK 57 bundled package metadata. Expo, React/React Native, native Expo modules, native navigation support packages and React types now agree with SDK 57. The npm lock matches the manifest. CLI help confirms SDK 57 uses the standard `--tunnel` argument for ngrok; the SDK 58 provider argument was removed. No screen implementation or local-data contract changed. Formatting, both typechecks, 3 tests, compatibility check and 21/21 Expo Doctor checks pass. Native phone rendering remains a separate acceptance checkpoint.
