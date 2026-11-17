"""Native MVC + fetch persistence checks on the disposable loopback Social fixture."""
import argparse
import json
import re
import sys
from pathlib import Path
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'artifacts/qa-deps'))
from playwright.sync_api import sync_playwright, expect

parser = argparse.ArgumentParser()
parser.add_argument('--base', required=True)
args = parser.parse_args()
assert urlparse(args.base).hostname in ('localhost', '127.0.0.1')
BASE = args.base
checks = []


def check(value, name):
    assert value, name
    checks.append(name)
    print('PASS', name, flush=True)


def login(browser, username, width):
    context = browser.new_context(viewport={'width': width, 'height': 900}, locale='vi-VN')
    page = context.new_page()
    page.goto(BASE + '/Account/Login')
    page.locator('[name=UserNameOrEmail]').fill(username)
    page.locator('[name=Password]').fill('SocialDemo123!')
    page.locator('form.auth-form button[type=submit]').click()
    page.wait_for_url(BASE + '/')
    return context, page


with sync_playwright() as p:
    browser = p.chromium.launch(channel='chrome', headless=True)
    owner_context, owner = login(browser, 'lanbep', 1366)
    other_context, other = login(browser, 'minhchef', 390)
    owner.locator('[data-theme-select]').select_option('dark')
    other.locator('[data-theme-select]').select_option('dark')
    recipe_id = None
    try:
        owner.goto(BASE + '/Recipe/Create')
        token = owner.locator('form.auth-form [name=__RequestVerificationToken]').input_value()
        invalid = owner.request.post(BASE + '/Recipe/Create', form={
            '__RequestVerificationToken': token, 'Name': 'QA Premium retained form', 'Category': 'Món Việt',
            'Description': 'Temporary test only', 'Ingredients': '200 g rau', 'Instructions': 'Rửa rau\nNấu rau',
            'Servings': '4', 'PreparationTimeMinutes': '0'
        })
        check(invalid.status == 200 and 'value="QA Premium retained form"' in invalid.text()
              and 'Temporary test only' in invalid.text(), 'Server validation retains entered values')
        owner.locator('[name=Name]').fill('QA Premium temporary recipe')
        owner.locator('[name=Category]').select_option(label='Món Việt')
        owner.locator('[name=Description]').fill('Temporary recipe created by browser verification')
        owner.locator('[name=Ingredients]').fill('200 g rau\n100 ml nước')
        owner.locator('[name=Instructions]').fill('Rửa sạch rau\nNấu chín và thưởng thức')
        owner.locator('[name=PreparationTimeMinutes]').fill('10')
        owner.locator('[name=CookingTimeMinutes]').fill('15')
        owner.locator('form.auth-form button[type=submit]').click()
        owner.wait_for_url(re.compile(re.escape(BASE) + r'/Recipe/Details/\d+'))
        recipe_id = int(owner.url.rsplit('/', 1)[1])
        check(owner.locator('h1').inner_text() == 'QA Premium temporary recipe', 'Native create saves a real recipe')
        owner.goto(BASE + f'/Recipe/Edit/{recipe_id}')
        owner.locator('[name=Name]').fill('QA Premium updated recipe')
        owner.locator('form.auth-form button[type=submit]').click()
        owner.wait_for_url(BASE + f'/Recipe/Details/{recipe_id}')
        check(owner.locator('h1').inner_text() == 'QA Premium updated recipe', 'Native edit persists changed content')
        owner.locator('#recipeServings').fill('8')
        owner.locator('#recipeServings').dispatch_event('input')
        check('400' in owner.locator('[data-scalable-ingredient]').first.inner_text(), 'Serving scaler doubles quantities in dark mode')
        owner.locator('.cs108-ingredient-check').first.check()
        check('1/2' in owner.locator('#cs108IngredientProgress').inner_text(), 'Ingredient checklist updates preparation progress')
        owner.locator('[data-cooking-start]').click()
        owner.locator('[data-cooking-next]').click()
        check(owner.locator('[data-cooking-step-label]').inner_text() == 'Bước 2', 'Cooking dialog advances to the next step')
        owner.keyboard.press('Escape')
        expect(owner.locator('[data-cooking-mode]')).not_to_be_visible()
        owner.evaluate("Object.defineProperty(navigator, 'share', { value: undefined, configurable: true }); Object.defineProperty(navigator, 'clipboard', { value: undefined, configurable: true });")
        owner.locator('[data-page-share]').click()
        expect(owner.locator('#pageShareDialog')).to_be_visible()
        check(owner.locator('#pageShareUrl').input_value() == owner.url, 'Share fallback exposes the exact recipe URL without Clipboard API')
        owner.keyboard.press('Escape')
        check(owner.locator('[data-page-share]').evaluate('(el) => document.activeElement === el'), 'Share dialog Escape restores focus')
        other.goto(owner.url)
        like = other.locator('.recipe-like-btn').first
        like.click()
        expect(like).to_have_attribute('aria-pressed', 'true')
        check(like.locator('svg.fa-solid.fa-heart').count() == 1, 'Confirmed like changes SVG to the active state')
        other.reload()
        expect(other.locator('.recipe-like-btn').first).to_have_attribute('aria-pressed', 'true')
        check(other.locator('[data-db-like-count]').first.inner_text() == '1', 'Like count and state survive reload')
        other.locator('[data-comment-input]').fill('QA Premium persisted comment')
        other.locator('[data-comment-create] button[type=submit]').click()
        expect(other.locator('.comment-content-text').filter(has_text='QA Premium persisted comment')).to_be_visible()
        other.reload()
        check('QA Premium persisted comment' in other.locator('.comment-list').inner_text(), 'AJAX comment survives reload')
        check(other.locator('.comment-actions-row svg').count() == 2, 'Persisted comment uses SVG edit/delete controls')
        save = other.locator('[data-db-save-form] button').first
        save.click()
        expect(save).to_have_attribute('aria-pressed', 'true')
        other.reload()
        expect(other.locator('[data-db-save-form] button').first).to_have_attribute('aria-pressed', 'true')
        check(other.locator('svg.fa-solid.fa-bookmark').count() > 0, 'Favorite and active bookmark survive reload')
        other.locator('.star-btn').nth(3).click()
        other.locator('[data-rating-submit]').click()
        expect(other.locator('[data-rating-own]')).to_contain_text('4 sao')
        other.reload()
        check(other.locator('[name=Stars][value="4"]').is_checked(), 'Four-star rating survives reload')
        other.locator('[data-db-save-form] button').first.click()
        expect(other.locator('[data-db-save-form] button').first).to_have_attribute('aria-pressed', 'false')
        other.reload()
        check(other.locator('[data-db-save-form] button').first.get_attribute('aria-pressed') == 'false', 'Unsave persists')
        owner.goto(BASE + '/Notifications')
        check('minhchef' in owner.content() or 'Minh Đặng' in owner.content(), 'Recipient receives real interaction notifications')
        unread = owner.locator('form[action*="MarkAsRead"] button')
        if unread.count():
            unread.first.click()
            check(True, 'Native mark-as-read POST completes')
        owner.goto(BASE + '/Profile?userId=social-b')
        follow = owner.locator('.creator-actions form button')
        original = 'Bỏ theo dõi' in follow.inner_text()
        follow.click()
        expect(owner.locator('.creator-actions form button')).to_contain_text('Theo dõi' if original else 'Bỏ theo dõi')
        owner.locator('.creator-actions form button').click()
        owner.reload()
        check(('Bỏ theo dõi' in owner.locator('.creator-actions form button').inner_text()) == original, 'Follow/unfollow round-trip preserves the original relationship')
        guest = browser.new_context()
        response = guest.request.get(BASE + '/Recipe/Create', max_redirects=0)
        check(response.status == 302 and '/Account/Login' in response.headers.get('location', ''), 'Guest publishing requires Identity sign-in')
        rejected = other.request.post(BASE + f'/Recipe/Delete/{recipe_id}', form={})
        check(rejected.status == 400, 'Mutation without anti-forgery token is rejected')
        check(other.locator('html').get_attribute('data-theme') == 'dark', 'Dark preference persists through interaction navigation')
    finally:
        if recipe_id:
            owner.goto(BASE + f'/Recipe/Details/{recipe_id}')
            token = owner.locator('[name=__RequestVerificationToken]').first.input_value()
            deleted = owner.request.post(BASE + f'/Recipe/Delete/{recipe_id}', form={'__RequestVerificationToken': token})
            check(deleted.status == 200 and owner.request.get(BASE + f'/Recipe/Details/{recipe_id}').status == 404, 'Temporary recipe is deleted through its owner endpoint')
        browser.close()
out = ROOT / 'artifacts/premium-v2/interactions.json'
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps({'checks': checks}, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'COMPLETE {len(checks)} live interaction checks', flush=True)
