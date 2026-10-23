# Uni Companion design system

The endorsed visual authority is `design/visuals-v2/` and `design/visuals-v3/`. The initial serif/ivory concepts are rejected. The app implements the endorsed identity through native components, not flattened screenshot backgrounds.

## Tokens

Source: `mobile/src/theme.ts`.

| Role | Value |
| --- | --- |
| Primary | `#075BFF` |
| Pressed depth | `#0343C2` |
| Primary container | `#ECF2FF` |
| Ink | `#10153D` |
| Secondary text | `#626D8C` |
| Divider | `#E5EAF3` |
| Canvas | `#F8FAFF` |
| Character accent | `#FF982E` |
| Success / error | `#14754D` / `#BD354A` |

Nunito is bundled locally: regular body, semibold, extra-bold labels, black display. Body 16/24; secondary 13/19; labels 16/23; titles 21/28; headings 27/35; feature headings 32/36. Native font scaling stays enabled.

## Components and behaviour

Shared implementation: `mobile/src/ui.tsx`. Buttons are at least 56 high, icon controls at least 48. Primary buttons have a shallow darker bottom edge and compress on press. Rounded corners 14–23 depending on component role. Layout uses 22-point horizontal gutters, 18-point main gaps, and clear grouping.

Original orange star art is a transparent raster asset. It appears in welcome, onboarding and study encouragement; document reading and discussion bodies keep decoration out of the text. Icons use Lucide consistently. Auth forms are labelled, scrollable and keyboard-aware. Focused study sessions use stack navigation; Learn/Discuss/You are the main tabs.

## Product distinctions

Admin curated: admin-created course. Student-created: a community course with visible creator. Recognised: a student course reviewed by admin, with creator retained. Recognition is not a claim of university accreditation. Anonymous content uses a generic avatar and hides author display names in public views.

## Current limits

Light theme and compact portrait phones are the implemented target. Responsive web is a convenience preview. Native visual, font-scale, keyboard and gesture acceptance awaits the user's Expo Go walkthrough; no emulator should be started. Generated screen images remain references rather than pixel-level specifications.
