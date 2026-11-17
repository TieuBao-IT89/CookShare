# UI refinement evidence — #120

Baseline: `224f9af` (`develop`), 2026-10-24. Changes follow the [audit](../../UI_AUDIT_120.md); Home, Profile and Details retain their existing composition, fonts, palette and interactions. Tool setup is reviewed separately in [PR #124](https://github.com/TieuBao-IT89/CookShare/pull/124).

## Before / After

Public pages use the same existing development data. Form, notification and owner Profile captures use dedicated preview users in a separate local PostgreSQL fixture. Existing placeholder recipe artwork is a dataset condition. The screenshots below are viewport captures, with Home scrolled to the first post in both versions.

| Surface | Viewport | Before | After | Targeted improvement |
| --- | --- | --- | --- | --- |
| Home post | 1440 × 1000 | [Before](home-desktop-before.png) | [After](home-desktop-after.png) | Readable metadata/actions and sidebar contrast. |
| Home post | 360 × 844 | [Before](home-mobile-before.png) | [After](home-mobile-after.png) | Readable labels and 44px actions in two columns. |
| Discovery | 1440 × 1000 | [Before](discovery-desktop-before.png) | [After](discovery-desktop-after.png) | One results band, search action grouped with its fields. |
| Discovery | 360 × 844 | [Before](discovery-mobile-before.png) | [After](discovery-mobile-after.png) | One category control; recipes appear earlier. |
| Recipe Details | 360 × 844 | [Before](details-mobile-before.png) | [After](details-mobile-after.png) | Existing cooking shortcuts reachable before the hero. |
| Create Recipe | 360 × 844 | [Before](form-mobile-before.png) | [After](form-mobile-after.png) | No empty error box; 16px fields and associated hints/errors. |
| Notifications | 360 × 844 | [Before](notifications-mobile-before.png) | [After](notifications-mobile-after.png) | Larger Open/mark-read targets. |
| Owner Profile | 1440 × 1000 | [Before](profile-desktop-before.png) | [After](profile-desktop-after.png) | Readable accent text and shared card metadata; layout retained. |

## Verification

- Viewports: 360 × 844, 768 × 1024 and 1440 × 1000 on eight native routes (24 combinations), without horizontal page overflow or JavaScript errors. A 99-character unbroken notification actor name also fits.
- Keyboard: skip link, menu Enter/Escape with focus restoration, Profile ArrowRight tabs, ingredient Space, and cooking jump-link Enter/Tab. Jump targets remain below the sticky header.
- New frontend harness: **20 desktop + 20 mobile checks**. Existing Home Feed harness: **28 desktop + 28 mobile checks**.
- [Native fixture results](native-verification.json): **16 checks**, including real server validation, Create/Edit/Delete, Vietnamese/newline preservation, Cancel, notification read persistence and destination navigation.
- [Final visual measurements](final-visual.json): final Home/Discovery/Profile and Create/Edit/owner Profile recaptures after residual CSS fixes.
- After rebasing onto `9d8842b`, Release build/publish and `dotnet format CookShare.sln --verify-no-changes --no-restore --severity warn` pass. The solution reports **494 passed, 0 failed, 1 opt-in browser skipped**; running that Account Settings browser test separately with `COOKSHARE_RUN_BROWSER_TESTS=1` reports **1 passed, 0 skipped**. PostgreSQL integration tests use the local test database.
- [Integrated publish measurements](integration-visual.json): five public routes at all three widths (**15 checks**) after rebase, without overflow or JavaScript errors. Emulated reduced motion confirms `scroll-behavior: auto` and no post animation. GitHub CI is linked in the UI PR.

## Manual review

Run `dotnet run --project CookShare/CookShare.csproj` with the database configured according to the project README, then use browser responsive mode at 360px, 768px and 1440px.

1. Open Home and Discovery: inspect post actions, category selection, query submission, empty results and clearing filters.
2. Open a recipe: activate the cooking shortcuts with Enter, Tab to an ingredient, and toggle it with Space. Check Like/Save/Comment with your own test account.
3. Create/Edit a recipe: submit an empty required name, correct it, and try an unavailable image URL followed by a valid image. Check the feedback, preserved text, Save and Cancel.
4. With existing notifications, switch All/Unread, mark one as read and open its destination. Inspect Profile tabs and readable card metadata. Enable reduced motion in browser rendering tools and repeat navigation.

This is targeted verification, not a complete WCAG certification, physical iOS test or performance benchmark. Detector occurrences include repeated elements and intentional style warnings. Source-only `.cshtml` scanning does not resolve the rendered CSS; independent browser evidence and native forms supplement it. Secondary Profile metadata and brand micro-labels can still be smaller than the improved primary controls. Ingredient checklist persistence remains outside this change.

Raw browser profiles, image failures used as test stimuli, detector overlays/runtime tokens and fixture credentials are intentionally kept in ignored local artifacts. The application acquires no design-tool dependency, hook, database migration or new backend behavior.
