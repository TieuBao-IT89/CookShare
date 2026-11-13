# CookShare Social 2.0 — UI audit and reference study

Research date: 9 November 2026. This document records the evidence used before
changing the UI. It distinguishes source/documentation review from rendered
browser verification; the latter is recorded separately with the local captures.

## Reference study

Only interaction patterns and content hierarchy are reused. No third-party code,
photography, branding or other application assets are copied from these sources.

| Source and reviewed material | Pattern | CookShare application | Verification |
| --- | --- | --- | --- |
| [Pinterest: Save Pins to a board](https://help.pinterest.com/en-gb/article/save-pins-on-pinterest), [Manage your boards](https://help.pinterest.com/en-gb/article/boards) | Desktop and mobile instructions describe Save → choose an existing board or create a board. Public and private boards have distinct visibility. | A visible, keyboard-operable collection action next to the independent Favorite state; a chooser with a create-and-add flow. Discovery keeps DOM reading order in CSS Grid. | Native links with JavaScript disabled; keyboard focus/Escape/return focus if enhanced to a dialog; owner/other-user direct URL tests; persistence after reload. |
| [Bon Appétit: Recipes](https://www.bonappetit.com/recipes) | Food images, a clear title hierarchy and cooking-need groups such as meals in 30 minutes or less. | A compact discovery introduction, prominent search and real filter shortcuts; feed retains its denser continuous reading layout. | First result position at 360/390px; real filter result counts; food/fallback image crops; no fabricated recommendations. |
| [Culinary Wonderland: For Good Design Lab case study](https://www.forgooddesignlab.com/work/culinary-wonderland), [live home](https://www.culinarywonderland.com/) | Unified food/editorial identity, chef attribution and topical discovery. The live page exposes recipe times, chef links and community activity. | Preserve Be Vietnam Pro / Playfair Display and Warm & Fresh tokens; retain author attribution and image-led recipe and collection cards. | Same-fixture screenshot comparison, long Vietnamese titles and names, author link and topic-query verification. |
| [Khufu’s: MONARQ case study](https://monarqeg.com/case-studies/khufus/) | Restrained typography, imagery and spacing; mobile content order supports immediate practical actions. | Recipe title, author, timing, servings and cooking action precede the cooking content; one main recipe image, followed by the full story and ingredients/instructions. | First-viewport cooking action on mobile; image dimensions and priority; native jump links and cooking dialog focus; no video dependency. |
| [Cookpad community guidelines](https://cookpad.com/us/community), [official Cooksnaps explanation](https://blog.cookpad.es/2025/04/que-es-un-cooksnap-y-por-que-compartirlo-transforma-tu-forma-de-cocinar.html) | A cooksnap is the cook’s own photo of a recipe they tried, attached to its original recipe. | Explicit “Công thức” and “Đã nấu thử” composer choices; a distinct finished-dish card, original recipe link and recipe-page community gallery. | Original recipe reachable from every public finished-dish card; owner edit/delete and hidden-parent checks; upload error/input-retention review. |
| [Mealie GitHub](https://github.com/mealie-recipes/mealie), [official documentation and feature screenshots](https://mealie.io/), [demo](https://demo.mealie.io/) | The documented recipe screen groups photo, cooking times, ingredients and ordered instructions; editor/meal-planning screens use structured sections. | Retain serving scaler, ingredient checklist and cooking mode; improve the recipe’s initial hierarchy and keep form groups and validation. | Serving-scaler and UI-refinement harnesses; native recipe form validation and retained values; desktop two-column ingredients/instructions. |
| [Pixelfed GitHub README and light-mode UI screenshot](https://github.com/pixelfed/pixelfed) | User photography is central, with attribution and surrounding post context. | Finished-dish images anchor their feed cards, with cook identity, note and original recipe in the same article. | Distinct recipe/finished-dish labels; image crop, alt text, long notes and missing-image tests; stable mixed-feed cursor checks. |
| [Mastodon GitHub](https://github.com/mastodon/mastodon), [official moderation documentation](https://docs.joinmastodon.org/admin/moderation/) | A following timeline and explicit moderation actions/visibility consequences. | Preserve Following as a separate feed; reports lead to an authorized queue with recorded actions. Hidden posts and media follow one server visibility policy. | Follow/unfollow feed checks; direct moderator POST rejection for ordinary users; report, hide, restore and direct-media checks. |

### Access limitations

- Pinterest’s live `/ideas/` returned HTTP 403 in the web reader. An initial
  `/en/article/save-pins` address returned 404. The working official `en-gb`
  articles above provide separate desktop and mobile interaction instructions.
- The [Awwwards Khufu’s listing](https://www.awwwards.com/sites/khufus) timed out.
  The creator’s primary case study was readable and identifies the Honorable
  Mention; the award is not independently reverified from the unavailable listing.
- Mealie’s demo returned an HTML shell without readable content in the web
  reader. The official documentation’s labeled recipe/editor screenshots and
  feature descriptions were used instead.
- The web reader exposes content and image references, not a controllable desktop
  or mobile browser. External-site viewport interactions are therefore not claimed
  as tested. CookShare’s own screenshots were inspected as images, and local
  rendered viewport testing is required after implementation.

## Existing UI evidence

Reviewed source includes `_Layout`, `_RecipeHeader`, `_FeedPost`, `_RecipeCard`,
Home/Index, Recipe/Index and Recipe/Details, their CSS and the existing navigation,
feed and cooking scripts. Existing route names, form actions, anti-forgery tokens,
IDs, data attributes and event handlers are preservation constraints.

The following committed screenshots were inspected directly:

- [Home desktop](ui/issue120/home-desktop-after.png) and
  [Home mobile](ui/issue120/home-mobile-after.png).
- [Discovery desktop](ui/issue120/discovery-desktop-after.png) and
  [Discovery mobile](ui/issue120/discovery-mobile-after.png).
- [Recipe details mobile](ui/issue120/details-mobile-after.png).

These are historical issue #120 captures, not fresh Social 2.0 measurements. The
Home captures are scrolled; they do not establish the initial composer/feed
position. Fresh before/after captures must use the same fixture, authentication
state, route and viewport.

| Finding | Evidence and effect | Targeted change | Verification |
| --- | --- | --- | --- |
| S1: primary mobile destinations require the menu | The header screenshot shows only the logo and hamburger; source has no bottom navigation. Notifications are inside the hidden mobile navigation. | Five labeled mobile destinations and a top-bar notification control; retain the account menu. | 360/390/768px, active route semantics, 44px controls, safe-area and bottom-focus clearance, no-JavaScript navigation. |
| S2: desktop search is visually narrow and disappears below 1200px | Shared CSS sets a 170px, 11px search and hides it at 1199px. | A clearer desktop search surface; tablet/mobile keep a direct discovery destination. | Header overflow at 768/1440px, long signed-in names and search submit. |
| D1: discovery exposes every filter before results on narrow screens | Current markup has search, category, difficulty and maximum-time controls in one grid; mobile CSS makes every control a row. Historical capture predates some controls and already places the first card around y=530. | Prominent search plus a collapsible mobile filter surface with Apply/Clear, active removable chips and URL-based sort/paging. | All GET values round-trip, Back restores filters, no-JavaScript filtering, first-card position and bounded server results. |
| R1: mobile recipe metadata/actions and description delay the main photo and cooking action | In the mobile details capture, the first 844px contain only the hero copy, attribution and early social actions. The cooking button follows the full summary/action block in source. | Compact title/author/metadata with a clear primary cooking action; main photo before the full story and retained secondary actions. | Real long-description fixture, first cooking action and photo positions, servings/checklist and cooking dialog regressions. |
| I1: placeholder-heavy fixture masks the food-image hierarchy | Historical Home and Discovery captures contain many legitimate fallback illustrations. | Use existing licensed/local food images in an isolated demo/test fixture, with separate empty/broken/crop cases. | Same food fixture before/after; valid portrait/landscape images, missing images and failed image requests. |

## Boundaries and acceptance

The established three-column feed, real community suggestions, serif recipe titles,
recipe checklist/scaler, comments/ratings, profile identity and notification states
are retained. UI changes use screen-scoped CSS and the existing Razor/vanilla
stack. New business rules and migrations belong to their own functional slices.

Check 360, 390, 768 and 1440px without horizontal overflow; keyboard focus and Tab
order; disclosure/menu Escape and focus restoration; empty/error/loading states;
long Vietnamese content; guest/authenticated behavior; image fallbacks; reduced
motion; native GET/POST/link behavior when JavaScript is disabled. Physical iOS
and Safari checks must be recorded as untested unless actually available.

Implementation and command results are recorded in the Social 2.0 delivery notes
after the corresponding functional slice is complete. This audit alone does not
claim that the new screens or features pass acceptance.
