# Saved Recipes browser checks

From the repository root on Windows with Chrome or Edge installed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-SavedRecipeBrowserTests.ps1 -Viewport Desktop
powershell -NoProfile -ExecutionPolicy Bypass -File CookShare.Tests/Frontend/Run-SavedRecipeBrowserTests.ps1 -Viewport Mobile
```

Supply `-BrowserPath` when the browser is installed elsewhere. The script uses a new isolated browser profile and writes its DOM results/logs under ignored `artifacts/issue94/`. It does not open an application server or connect to a database. No npm packages are required. Headless Chrome uses a 390px iframe for the mobile case because its desktop window has a larger minimum width.

The tracked HTML harness executes the actual `recipe-saved.js` and existing CSS with controlled `fetch` responses. Forty assertions cover server state, Save/Unsave and CSRF, duplicate/pending requests, counts and empty collections, accessible focus/errors, network/database failures, malformed responses/XSS, independent Likes, login/session changes, bfcache/visibility refresh, and preservation of legacy storage. Loading the script twice must still issue one request.

This verifies module behavior and a small desktop/mobile CSS fixture. It does not replace an ASP.NET browser login flow, full page visual review, or PostgreSQL persistence/restart tests. API/controller coverage runs separately through `dotnet test CookShare.sln`.

Open `recipe-saved.browser.html?preview=1` for the static CSS fixture or add `&viewport=390` for the 390px preview.
