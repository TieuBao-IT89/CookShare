"""Capture CookShare's local UI. Install Playwright separately; use installed Chrome.

python -m pip install --target artifacts/qa-deps playwright
python tools/ui_review.py before
python tools/ui_review.py after
"""

import json
import re
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "artifacts" / "qa-deps"))
from playwright.sync_api import sync_playwright, expect


def capture(stage):
    output = ROOT / "docs" / "ui" / stage
    output.mkdir(parents=True, exist_ok=True)
    errors = []
    results = []
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(channel="chrome", headless=True)
        context = browser.new_context(viewport={"width": 1440, "height": 1000}, locale="vi-VN")
        page = context.new_page()
        page.on("pageerror", lambda error: errors.append(str(error)))
        page.on("response", lambda response: errors.append(f"{response.status}: {response.url}")
                if response.status >= 400 and response.request.resource_type in ["script", "stylesheet"] else None)
        for name, route in [
            ("home", "/"), ("explore", "/Recipe"),
            ("details", "/Recipe/Details/1"), ("create", "/Recipe/Create"),
            ("saved", "/Recipe/Saved"), ("profile", "/Profile"),
            ("login", "/Home/Login"), ("register", "/Home/Register"),
        ]:
            response = page.goto("http://127.0.0.1:5088" + route)
            page.evaluate("document.fonts.ready")
            page.locator("img").evaluate_all(
                "images => { images.forEach(img => img.loading = 'eager'); "
                "return Promise.all(images.map(img => img.decode().catch(() => {}))); }"
            )
            page.evaluate("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))")
            if stage == "after":
                assert page.locator("img").evaluate_all("images => images.every(img => img.naturalWidth > 0)"), (route, "image failed to decode")
            page.screenshot(path=str(output / f"{name}-desktop.png"), animations="disabled")
            results.append({"route": route, "status": response.status,
                            "overflow": page.evaluate("document.documentElement.scrollWidth > innerWidth")})
            page.set_viewport_size({"width": 390, "height": 844})
            page.screenshot(path=str(output / f"{name}-mobile.png"), animations="disabled")
            results.append({"route": route, "width": 390,
                            "overflow": page.evaluate("document.documentElement.scrollWidth > innerWidth")})
            page.set_viewport_size({"width": 1440, "height": 1000})
        if stage == "after":
            page.evaluate("localStorage.setItem('cookshare_saved_recipes_v1', '[1,4]')")
            page.goto("http://127.0.0.1:5088/Recipe/Saved")
            expect(page.locator("#savedRecipeGrid .recipe-card")).to_have_count(2)
            page.screenshot(path=str(output / "saved-populated-desktop.png"), animations="disabled")
            page.set_viewport_size({"width": 390, "height": 844})
            page.screenshot(path=str(output / "saved-populated-mobile.png"), animations="disabled")
            page.set_viewport_size({"width": 1440, "height": 1000})
            page.goto("http://127.0.0.1:5088/Recipe/Details/1")
            page.screenshot(path=str(output / "details-full.png"), animations="disabled", full_page=True)
            page.goto("http://127.0.0.1:5088/Recipe/Create")
            page.screenshot(path=str(output / "create-full.png"), animations="disabled", full_page=True)
        browser.close()
    report = {"pages": results, "javascript_errors": errors}
    (output / "capture-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=True, indent=2))
    if stage == "after":
        assert not errors, errors
        assert not any(item["overflow"] for item in results), results
        assert all(item.get("status", 200) == 200 for item in results), results


def verify():
    """Behavior checks in a temporary browser profile, never the user's storage."""
    checks = []
    errors = []
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(channel="chrome", headless=True)
        context = browser.new_context(viewport={"width": 1440, "height": 1000}, locale="vi-VN")
        page = context.new_page()
        page.on("pageerror", lambda error: errors.append(str(error)))
        page.on("response", lambda response: errors.append(f"{response.status}: {response.url}")
                if response.status >= 400 and response.request.resource_type in ["script", "stylesheet"] else None)
        base = "http://127.0.0.1:5088"

        page.goto(base + "/")
        expect(page.locator("#homeFeaturedGrid .recipe-card")).to_have_count(3)
        page.locator("#headerGlobalSearch").fill("Phở")
        page.locator("#headerGlobalSearch").press("Enter")
        expect(page.locator("#recipeList .recipe-card")).to_have_count(1)
        page.locator("#searchRecipe").fill("")
        expect(page.locator("#recipeList .recipe-card")).to_have_count(6)
        page.locator("#searchRecipe").fill("khong-co-mon-nay-123")
        expect(page.locator("#emptyRecipe")).to_be_visible()
        page.locator("#resetRecipeFilters").click()
        expect(page.locator("#recipeList .recipe-card")).to_have_count(6)
        page.goto(base + "/")
        page.locator('.category-tile[href*="chay"]').click()
        expect(page.locator("#categoryFilter")).to_have_value("Món chay")
        expect(page.locator("#recipeList .recipe-card")).to_have_count(1)
        page.locator('.category-pill[data-category="Món Việt"]').click()
        expect(page.locator("#recipeList .recipe-card")).to_have_count(2)
        checks.append("Header search, clear search, category links/pills, empty results and reset")

        page.goto(base + "/Recipe")
        first = page.locator('#recipeList .recipe-card[data-id="1"]')
        first.locator(".recipe-like-btn").click()
        expect(first.locator(".recipe-like-btn")).to_have_attribute("aria-pressed", "true")
        first.locator(".card-save").click()
        page.goto(base + "/Recipe/Details/1")
        expect(page.locator(".recipe-like-btn")).to_have_attribute("aria-pressed", "true")
        expect(page.locator(".btn-details-save")).to_have_attribute("aria-pressed", "true")
        page.locator(".ingredient-checkbox").first.check()
        expect(page.locator("#ingredientCheckedCount")).to_have_text("1")
        page.locator(".ingredient-checkbox").first.uncheck()
        expect(page.locator("#ingredientCheckedCount")).to_have_text("0")
        page.locator('.star-btn[data-star="4"]').click()
        expect(page.locator('.star-btn[data-star="4"]')).to_have_attribute("aria-checked", "true")
        expect(page.locator("#detailsAverageRatingDisplay")).to_contain_text("4.0")
        page.reload()
        expect(page.locator('.star-btn[data-star="4"]')).to_have_attribute("aria-checked", "true")
        page.goto(base + "/")
        expect(page.locator('#homeFeaturedGrid .recipe-card[data-id="1"] .recipe-rating-display')).to_have_text("★ 4.0")
        checks.append("Star ratings persist after reload and synchronize with Home cards")
        page.goto(base + "/Recipe/Saved")
        expect(page.locator("#savedRecipeCount")).to_have_text("1")
        expect(page.locator("#savedRecipeGrid .recipe-like-btn")).to_have_attribute("aria-pressed", "true")
        page.locator(".saved-card-unsave-btn").click()
        expect(page.locator("#savedEmptyState")).to_be_visible()
        expect(page.locator("#savedRecipeCount")).to_have_text("0")
        other_tab = context.new_page()
        other_tab.goto(base + "/Recipe")
        other_tab.locator('#recipeList .recipe-card[data-id="1"] .card-save').click()
        expect(page.locator("#savedRecipeCount")).to_have_text("1")
        expect(page.locator("#savedRecipeGrid .card-save")).to_have_attribute("aria-pressed", "true")
        other_tab.locator('#recipeList .recipe-card[data-id="1"] .card-save').click()
        expect(page.locator("#savedEmptyState")).to_be_visible()
        expect(page.locator("#savedRecipeCount")).to_have_text("0")
        other_tab.close()
        checks.append("Likes/saves persist between pages, saved removal/count/empty state, ingredient checklist")

        page.goto(base + "/Recipe/Details/1")
        page.locator("#commentNewTextarea").fill("Món ngon, cách làm rõ ràng!")
        page.locator(".btn-submit-comment").click()
        expect(page.locator(".comment-content-text")).to_contain_text("Món ngon, cách làm rõ ràng!")
        page.reload()
        expect(page.locator(".comment-content-text")).to_contain_text("Món ngon, cách làm rõ ràng!")
        checks.append("Add comment and retain after reload")

        page.goto(base + "/")
        expect(page.locator("#homeCommunityUsers .user-card")).to_have_count(3)
        member = page.locator('#homeCommunityUsers .user-card[data-user-id="demo-tran-minh-anh"]')
        member.locator(".btn-follow-action").click()
        expect(member.locator(".btn-follow-action")).to_have_attribute("aria-pressed", "true")
        member.locator(".user-card-name a").click()
        expect(page.locator(".profile-name")).to_have_text("Trần Minh Anh")
        expect(page.locator("#profileHeaderFollowBtn .btn-follow-action")).to_have_attribute("aria-pressed", "true")
        page.locator("#profileHeaderFollowBtn .btn-follow-action").click()
        expect(page.locator(".profile-stats .stat").first.locator("strong")).to_have_text("0")
        page.goto(base + "/Profile")
        page.locator("#profileFollowingTab").click()
        expect(page.locator("#profileFollowingContainer .user-card")).to_have_count(1)
        page.locator("#profileFollowingContainer .btn-follow-action").click()
        expect(page.locator("#profileFollowingContainer .follow-empty-state")).to_be_visible()
        page.locator("#profileFollowersTab").click()
        expect(page.locator("#profileFollowersContainer .user-card")).to_have_count(2)
        page.locator("#profileFollowersTab").focus()
        page.keyboard.press("Home")
        expect(page.locator("#profileRecipesTab")).to_have_attribute("aria-selected", "true")
        expect(page.locator("#profileRecipeGrid")).to_be_visible()
        checks.append("Home follow links/buttons, target Profile, counters, followers/following lists and keyboard tabs")

        page.goto(base + "/Recipe/Create")
        page.locator('[type="submit"]').last.click()
        expect(page.locator(".form-error.visible")).to_have_count(5)
        expect(page.locator("#recipeName")).to_be_focused()
        page.locator("#recipeName").fill('Món thử UI <b>an toàn</b>')
        page.locator("#recipeCategory").select_option("Món Việt")
        page.locator("#recipeDescription").fill("Mô tả món thử từ kiểm tra giao diện.")
        page.locator("#recipeIngredients").fill("200g rau\n1 chút muối")
        page.locator("#recipeInstructions").fill("Bước 1: Rửa rau.\nBước 2: Nấu chín.")
        page.locator("#recipeImage").set_input_files({"name": "invalid.txt", "mimeType": "text/plain", "buffer": b"not an image"})
        expect(page.locator("#uiToast")).to_contain_text("JPG")
        import base64
        image = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aYioAAAAASUVORK5CYII=")
        page.locator("#recipeImage").set_input_files({"name": "test.png", "mimeType": "image/png", "buffer": image})
        expect(page.locator("#imagePreviewBox")).to_be_visible()
        page.locator('#createRecipeForm [type="submit"]').click()
        page.wait_for_url(base + "/Recipe")
        expect(page.locator("#recipeList .recipe-card")).to_have_count(7)
        new_id = page.evaluate("getRecipes().find(r => r.name.startsWith('Món thử UI')).id")
        page.goto(base + f"/Recipe/Edit/{new_id}")
        expect(page.locator("#recipeName")).to_have_value('Món thử UI <b>an toàn</b>')
        expect(page.locator("#imagePreviewBox img")).to_have_attribute("src", re.compile(r"^data:image/png"))
        page.locator("#recipeName").fill("Món thử UI đã chỉnh sửa")
        page.locator('#editRecipeForm [type="submit"]').click()
        page.wait_for_url(base + f"/Recipe/Details/{new_id}")
        expect(page.locator(".details-title")).to_have_text("Món thử UI đã chỉnh sửa")
        page.locator("[data-delete-details]").click()
        expect(page.locator("#confirmDialog")).to_be_visible()
        expect(page.locator('#confirmDialog [value="cancel"]')).to_be_focused()
        page.keyboard.press("Escape")
        expect(page.locator("#confirmDialog")).not_to_be_visible()
        page.locator("[data-delete-details]").click()
        page.locator('#confirmDialog [value="confirm"]').click()
        page.wait_for_url(base + "/Recipe")
        expect(page.locator("#recipeList .recipe-card")).to_have_count(6)
        checks.append("Create/edit/upload/validation, escaped content, delete confirmation with cancel/Escape/confirm")

        page.goto(base + "/Home/Register")
        page.locator("#registerPassword").fill("password123")
        page.locator("#confirmPassword").fill("different123")
        assert not page.locator("#confirmPassword").evaluate("input => input.validity.valid")
        page.locator('[data-password-target="registerPassword"]').click()
        expect(page.locator("#registerPassword")).to_have_attribute("type", "text")
        page.locator("#registerLastName").fill("Nguyễn")
        page.locator("#registerFirstName").fill("Hải")
        page.locator("#registerUsername").fill("testuser")
        page.locator("#registerEmail").fill("test@example.com")
        page.locator("#confirmPassword").fill("password123")
        page.locator('.auth-form [type="submit"]').click()
        expect(page.locator(".auth-status")).to_be_visible()
        expect(page.locator(".auth-status")).to_contain_text("chưa tạo tài khoản")
        checks.append("Password visibility/mismatch validation, truthful registration status")

        for width in [320, 390, 768, 1024, 1440]:
            page.set_viewport_size({"width": width, "height": 900})
            for route in ["/", "/Recipe", "/Recipe/Details/1", "/Recipe/Create", "/Recipe/Edit/1", "/Recipe/Saved", "/Profile", "/Home/Login", "/Home/Register", "/Home/Error"]:
                response = page.goto(base + route)
                assert response.status == 200, (route, response.status)
                assert not page.evaluate("document.documentElement.scrollWidth > innerWidth"), (route, width, "overflow")
            if width < 720:
                page.locator(".menu-btn").click()
                expect(page.locator("#primaryNav")).to_be_visible()
                expect(page.locator(".menu-btn")).to_have_attribute("aria-expanded", "true")
                page.keyboard.press("Escape")
                expect(page.locator(".menu-btn")).to_have_attribute("aria-expanded", "false")
        checks.append("10 routes × 5 viewport widths; mobile menu open and Escape; no horizontal overflow")

        page.goto(base + "/Recipe/Details/999999")
        expect(page.locator(".recipe-empty-container h1")).to_contain_text("Không tìm thấy")
        page.evaluate("localStorage.setItem('cookshare_recipes', '[]')")
        page.goto(base + "/")
        expect(page.locator(".collection-empty")).to_have_count(2)
        page.evaluate("localStorage.setItem('cookshare_recipes', '{broken-json')")
        page.goto(base + "/Recipe")
        expect(page.locator("#recipeDataError")).to_be_visible()
        assert page.evaluate("localStorage.getItem('cookshare_recipes')") == "{broken-json"
        assert not page.evaluate("saveRecipes([])")
        assert page.evaluate("localStorage.getItem('cookshare_recipes')") == "{broken-json"
        page.evaluate("localStorage.removeItem('cookshare_recipes')")
        page.reload()
        page.evaluate("() => { Storage.prototype.setItem = function() { throw new DOMException('Quota exceeded', 'QuotaExceededError'); }; }")
        assert not page.evaluate("saveRecipes([])")
        expect(page.locator("#recipeDataError")).to_be_visible()
        checks.append("Missing recipe, empty Home, corrupted data retained, write failure reported")
        browser.close()
    assert not errors, errors
    report = {"checks": checks, "javascript_errors": errors, "result": "passed"}
    destination = ROOT / "docs" / "ui" / "verification.json"
    destination.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=True, indent=2))


if __name__ == "__main__":
    stage = sys.argv[1] if len(sys.argv) > 1 else "after"
    verify() if stage == "verify" else capture(stage)
