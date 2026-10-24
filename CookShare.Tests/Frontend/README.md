# Saved Recipes browser checks

## Creator Profile

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-CreatorProfileBrowserTests.ps1 -Viewport Desktop
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-CreatorProfileBrowserTests.ps1 -Viewport Mobile
```

The creator harness runs production `creator-profile.js` and CSS in an isolated browser, without an application database or CDN dependency. Its 32 checks cover keyboard tabs/ARIA, safe Bio/name previews, the 300-character counter, image validation/fallback/stale loads, highlight counts/selection/order/unpin, native save and CSRF fields, initialization twice, and overflow. Mobile uses a real 360px iframe. Python and Chrome/Edge are required; supply `-BrowserPath` if needed. Results are written under ignored `artifacts/issue117/`.

See [CREATOR_PROFILE.md](../../docs/CREATOR_PROFILE.md) for native application form flows, ownership tests and PostgreSQL migration/concurrency checks, and [UI evidence](../../docs/ui/issue117/README.md) for the full-page 360px/768px/1440px review.

## Persistent Recipe Ratings

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-RecipeRatingBrowserTests.ps1 -Viewport Desktop
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-RecipeRatingBrowserTests.ps1 -Viewport Mobile
```

The rating harness runs the production `recipe-ratings.js` with controlled responses and the actual rating styles, without an application database or CDN dependency. It verifies confirmed state, updates, CSRF, duplicate requests, errors, session changes, bfcache refresh, and desktop/mobile overflow. See [RECIPE_RATINGS.md](../../docs/RECIPE_RATINGS.md) for the live-page and PostgreSQL checks.

## Home / Following Feed

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-HomeFeedBrowserTests.ps1 -Viewport Desktop
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-HomeFeedBrowserTests.ps1 -Viewport Mobile
```

The feed harness needs Python and Chrome/Edge. It starts an isolated loopback static server, uses a fresh browser profile, and stops its server afterward. Artifacts are written under ignored `artifacts/issue98/`. Mobile uses a real 390px iframe viewport.

It executes the production feed, Favorites, and Like/Comment scripts with controlled responses. Checks cover pagination, deduplication, simultaneous requests, native fallback URLs, CSRF on appended posts, Font Awesome outline/solid state changes for Like and Save, comments, sharing, automatic-load limits, network and malformed-response errors, expired sessions/account changes, and horizontal overflow. It never contacts the application database. Icon class changes are tested without fetching CDN assets; check actual font loading on the running website.

## Saved Recipes

From the repository root on Windows with Chrome or Edge installed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-SavedRecipeBrowserTests.ps1 -Viewport Desktop
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-SavedRecipeBrowserTests.ps1 -Viewport Mobile
```

Supply `-BrowserPath` when the browser is installed elsewhere. The script uses a new isolated browser profile and writes its DOM results/logs under ignored `artifacts/issue94/`. It does not open an application server or connect to a database. No npm packages are required. Headless Chrome uses a 390px iframe for the mobile case because its desktop window has a larger minimum width.

The tracked HTML harness executes the actual `recipe-saved.js` and existing CSS with controlled `fetch` responses. Forty assertions cover server state, Save/Unsave and CSRF, duplicate/pending requests, counts and empty collections, accessible focus/errors, network/database failures, malformed responses/XSS, independent Likes, login/session changes, bfcache/visibility refresh, and preservation of legacy storage. Loading the script twice must still issue one request.

This verifies module behavior and a small desktop/mobile CSS fixture. It does not replace an ASP.NET browser login flow, full page visual review, or PostgreSQL persistence/restart tests. API/controller coverage runs separately through `dotnet test CookShare.sln`.

Open `recipe-saved.browser.html?preview=1` for the static CSS fixture or add `&viewport=390` for the 390px preview.
