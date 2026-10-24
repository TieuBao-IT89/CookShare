# UI/UX Audit — Issue #120

Method: dual-agent (A: `/root/ui_visual_review` · B: `/root/ui_detector_review`), followed by an authenticated fixture review. Assessment A finished before the parent read detector findings. Baseline: `224f9af`, 2026-10-24. Scope: refinement of the existing Razor UI, with the Warm & Fresh identity retained.

## Findings and implementation scope

| ID | Priority | Verified finding | Implemented targeted correction |
| --- | --- | --- | --- |
| C1 | P1 | Header/form placeholders have 3.6–3.8:1 contrast; small terracotta category text 3.8:1. Some sidebar labels are 3.4–3.9:1; muted footer text on sage is a 4.3:1 advisory. | A readable accent text token, shared placeholder/muted text colors, and affected sidebar colors; preserve decorative accent and palette identity. |
| T1 | P2 | Feed metadata/auto-loading/actions use 8–10px on mobile; repeated Recipe Card metadata is 9–10px. Home category targets are 30px; Discovery pills 38px. | Readable functional small text and 44px category/primary control hit areas; retain display type and layout. |
| D1 | P2 | Discovery repeats category select and ten pills; at 360px the first card title starts at y=1001. It also repeats results bands and skips h2 before card h3 headings. | One category treatment on mobile, one results band and a proper results h2; preserve GET parameters and desktop shortcuts. |
| R1 | P2 | Recipe cooking shortcuts occur after the entire hero: y=1040 mobile/y=1243 desktop in the sampled recipe. | Move the existing jump navigation before the hero; retain hero, ingredients, timeline and interactions. |
| F1 | P2 | All six Recipe Form fields have labels but lack associations to existing hints/validation. A broken photo silently becomes the default image without error feedback. Mobile field font is 14px. A pristine form renders an empty red validation box. | Add described-by/required/error semantics, visible preview status and 16px mobile inputs; hide only the empty valid summary; keep values, validation rules, form actions and CSRF. |
| N1 | P2 | Notification Open buttons are 40px high; mark-read action is smaller than the 44px ergonomic target. | Increase hit areas and use the existing status message for filter feedback; keep the cards/read-state/filter/backend behavior. |

These are distinct verified problem groups, not the number of repeated detector occurrences. The 44px target is an ergonomic goal, not a claim that every smaller target violates WCAG's separate minimum-size criterion. No physical iOS test was run; 16px input sizing addresses the known mobile zoom risk inferred from computed 14px text.

## What is retained

Home Feed structure, sidebars, author attribution, photography, recipe hero, ingredient checklist, numbered steps, comments/social interactions, Profile cover/Bio/highlights/tabs, form grouping and notification cards already express the product well. All 12 public and 12 authenticated baseline route/viewport combinations fit without horizontal page overflow. A 99-character unbroken notification actor name also fits at all three widths. These areas do not need replacement.

Working keyboard foundations were exercised: skip link, menu Enter/Escape with focus restoration, native checkbox Space, and Profile ArrowRight tab navigation. Empty search retains query and offers recovery. Existing fonts, palette, route names, payloads and anti-forgery contracts remain binding constraints.

## Technical audit health

| Dimension | Baseline /4 | Evidence and limits |
| --- | ---: | --- |
| Accessibility | 2 | Confirmed contrast gaps and missing form hint associations; semantic controls and focus foundations present. No complete assistive-technology conformance audit. |
| Performance | 3 | Existing lazy cards, image dimensions, CSS transitions and no new runtime package needed; no lab performance benchmark claimed. |
| Theming | 3 | Shared palette/type tokens exist; some text colors bypass readable roles. Light theme is intentional, dark mode is outside scope. |
| Responsive | 3 | 360/768/1440 fit; tiny type/targets and repeated mobile filters impair use. Viewport/touch emulation is not physical-device testing. |
| Implementation integrity | 3 | Product-specific UI and real server data; narrow hierarchy/state defects, no framework/schema change needed. |
| Total | **14/20** | Good foundation, address the identified weak points. |

## Independent design critique

Assessment A: **28/40, Good**, based on live public routes and source review of private routes. The parent's later authenticated checks supplement evidence, without rewriting A's independent scores.

| Nielsen heuristic | /4 | Key evidence |
| --- | ---: | --- |
| Visibility of system status | 3 | Active tabs, real counts and ingredient progress; image failure feedback needs work. |
| Match with real world | 4 | Vietnamese cooking terminology, food/author relationships and cooking sequence. |
| User control and freedom | 3 | Breadcrumbs, clear filters, menu Escape, Cancel and safe delete default. |
| Consistency and standards | 3 | Shared fonts/palette; functional small text and targets vary. |
| Error prevention | 2 | Existing validation/CSRF/duplicate-submit controls; A did not submit authenticated forms. |
| Recognition rather than recall | 3 | Named controls/categories; tiny type and delayed shortcuts reduce recognition. |
| Flexibility and efficiency | 2 | Keyboard works; filtering repeats decisions and cooking shortcuts arrive late. |
| Aesthetic/minimal design | 3 | Coherent identity, with redundant pre-results content. |
| Error recovery | 3 | Useful empty-search recovery; form preview gives no failure explanation. |
| Help/documentation | 2 | Cooking examples and field hints exist; hint associations need work. |

Persona risks: a distracted mobile cook must aim at small categories and read 8px metadata; a newcomer scans two category controls; a keyboard/screen-reader user needs field errors tied to the right input. The design-specificity verdict is positive: green/cream, the font pairing, kitchen language and cooking controls are coherent CookShare choices. Cognitive load is concentrated in duplicated discovery choices, not a reason for a new page composition. The emotional valley is effort before reaching recipes or ingredients; targeted consolidation and earlier shortcuts address it.

Questions skipped: the user has already specified the design identity, two-PR split, and scope of confirmed audit fixes. No new visual direction or backend feature decision is required.

## Detector evidence and false positives

Impeccable 4.7.0 / engine 0.1.16 source scan on `CookShare/Views`: exit 0, `[]`. A controlled probe showed that this engine treats `.cshtml` as regex source and does not fully resolve linked styles. That empty result is not proof of rendered accessibility.

Assessment B confirmed mutable script injection and detector overlay execution in three fresh isolated browser tabs. It removed overlay DOM before taking canonical measurements, then stopped its live server with `--keep-inject`, without editing application HTML. Canonical baseline counts:

| Route | Affected elements | Primary rule findings | Advisories |
| --- | ---: | ---: | ---: |
| Home | 101 | 103 | 5 |
| Discovery | 166 | 162 | 6 |
| Login | 12 | 11 | 3 |

Most occurrences are repeated `undersized-ui-text` (85/156/5) or overlapping `tiny-text` (11/1/2), rather than independent bugs. Confirmed contrast and hierarchy findings inform C1/D1. The detector's font-size threshold is a quality heuristic, not a universal WCAG minimum font size.

Intentional/dismissed findings: existing eyebrows are a brand convention; decorative “CS” initials do not carry the separate visible author name; short final words in user-authored descriptions do not justify rewriting content. Overlay-generated text-occlusion and expanded mobile layout metrics disappear in clean tabs and are tool artifacts. Placeholder food imagery is a known dataset state, not evidence of failed image loading. Do not ship raw detector runtime tokens or claim an overlay is open in the user's own browser.

Deferred: ingredient checkmarks reset on reload. The current checklist is a temporary cooking aid; persistence is a separate interaction contract and is not added as part of this UI refinement. General help/search features, dark mode, new photography, and a full redesign are outside this issue's targeted corrections.

## Verification record

Selected [Before/After evidence and verification results](ui/issue120/README.md) are committed separately from ignored browser profiles, raw detector logs and local runtime metadata.

The independent After detector checkpoint reduced primary occurrences on the comparable Home/Discovery/Login routes from 276 to 59, with no remaining contrast or skipped-heading findings on those three routes. This checkpoint still exposed small Home image/composer labels and a Profile section color. The final changes raise those Home labels to 12px and apply accent ink to Profile section/highlight labels, confirmed by computed browser measurements (including an owner Profile with highlights). A subsequent narrow 390px URL scan emitted findings for Home and Profile without a low-contrast occurrence, but returned exit 1; it is treated as an incomplete diagnostic, not a passing scan. Its viewport/scope also differs from the independent comparison. Smaller Profile counts/date labels and brand micro-labels remain documented limitations, rather than a claim that every text size is now 12px.

Public Home, Discovery, Details and Profile, plus authenticated Create, Edit, Notifications and owner Profile, were reviewed at 360/768/1440px. All 24 route/viewport combinations fit horizontally. The final narrow recapture also checks Home/Discovery/Profile and Create/Edit/owner Profile after the residual CSS fixes. Keyboard jump links land below the sticky header and leave the next ingredient checkbox reachable with Tab. Existing reduced-motion rules are retained; no animation or runtime dependency is added.

Native fixture verification passes 16 checks: cookie login, six field associations, empty-summary visibility, mobile sizing, real server validation/input retention, Create/Edit with Vietnamese/newlines, safe Cancel and confirmed Delete, notification target sizes, All/Unread, persisted MarkAsRead, native Open and absence of JavaScript errors. The isolated frontend refinement harness passes 20 checks on desktop and 20 on mobile; the existing Home Feed harness passes 28 on each. Full solution and formatting results are recorded with the screenshots. Fixtures use a separate local PostgreSQL database and are removed after verification.

Integration against updated `develop` (`9d8842b`, including merged setup PR #124 and Account Settings PR #125) is conflict-free. The final Release publish passes 15 further public route/viewport checks and reduced-motion emulation. Solution PostgreSQL tests pass 494 cases; the one opt-in Account Settings browser case also passes when enabled separately. Formatting, JavaScript syntax and whitespace checks pass. The original baseline scores are retained as audit evidence, not presented as fresh scores for the integrated application.
