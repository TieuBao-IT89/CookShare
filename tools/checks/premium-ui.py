"""Browser QA on an explicitly supplied loopback TEST fixture; never seeds data.

Requires Playwright Python + local Chrome. Sign-in values belong to the existing
disposable Social V2 fixture, not application accounts. Results stay in artifacts.
"""
import argparse
import json
import sys
from pathlib import Path
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parents[2]
if (ROOT / 'artifacts/qa-deps').exists():
    sys.path.insert(0, str(ROOT / 'artifacts/qa-deps'))
from playwright.sync_api import sync_playwright, expect

parser = argparse.ArgumentParser()
parser.add_argument('--base', required=True)
parser.add_argument('--out', default='artifacts/premium-v2')
args = parser.parse_args()
assert urlparse(args.base).hostname in ('localhost', '127.0.0.1'), 'Only loopback test fixtures are allowed'
OUT = ROOT / args.out
OUT.mkdir(parents=True, exist_ok=True)
checks, screens, errors = [], [], []


def check(value, name):
    assert value, name
    checks.append(name)
    print('PASS', name, flush=True)


def set_theme(page, theme):
    if theme == 'system':
        page.locator('[data-theme-system]').click()
    elif page.locator('html').get_attribute('data-theme') != theme:
        page.get_by_role('switch', name='Giao diện tối').click()


with sync_playwright() as p:
    browser = p.chromium.launch(channel='chrome', headless=True)
    context = browser.new_context(locale='vi-VN', viewport={'width': 1366, 'height': 1000})
    page = context.new_page()
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.goto(args.base + '/Account/Login')
    page.locator('[name=UserNameOrEmail]').fill('lanbep')
    page.locator('[name=Password]').fill('SocialDemo123!')
    page.locator('form.auth-form button[type=submit]').click()
    page.wait_for_url(args.base + '/')
    routes = [('/', 'home'), ('/Recipe', 'discovery'), ('/Recipe/Details/1', 'details'),
              ('/Profile?userId=social-b', 'profile'), ('/Recipe/Create', 'create'),
              ('/Recipe/Saved', 'saved'), ('/Notifications', 'notifications'),
              ('/Profile/Edit', 'profile-edit'), ('/Account/Settings', 'settings'),
              ('/Collections', 'collections'), ('/Collections/Create', 'collection-create')]
    for theme in ('light', 'dark'):
        set_theme(page, theme)
        for width in (360, 390, 768, 960, 1024, 1366, 1920):
            page.set_viewport_size({'width': width, 'height': 900})
            for route, name in routes:
                response = page.goto(args.base + route)
                page.evaluate('document.fonts.ready')
                if width in (390, 1366):
                    page.locator('img').evaluate_all("images => { images.forEach(img => img.loading = 'eager'); return Promise.all(images.map(img => img.decode().catch(() => {}))); }")
                result = page.evaluate('''() => ({
                    theme: document.documentElement.dataset.theme,
                    background: getComputedStyle(document.body).backgroundColor,
                    overflow: document.documentElement.scrollWidth > innerWidth + 1,
                    fontIcons: document.querySelectorAll('i[class*="fa-"]').length,
                    missingIcons: [...document.querySelectorAll('svg use')].filter(use => !use.getAttribute('href')?.startsWith('/img/icons.svg#')).length
                })''')
                assert response.status == 200, (route, response.status)
                assert result['theme'] == theme, (route, result)
                assert result['background'] == ('rgb(16, 27, 32)' if theme == 'dark' else 'rgb(248, 245, 239)'), (route, result)
                assert not result['overflow'], (route, width, theme, 'overflow')
                assert result['fontIcons'] == 0 and result['missingIcons'] == 0, (route, result)
                screens.append({'route': route, 'theme': theme, 'width': width, **result})
                if width in (390, 1366) and name in ('home', 'discovery', 'details', 'profile', 'create', 'notifications'):
                    page.screenshot(path=str(OUT / f'{name}-{theme}-{width}.png'), animations='disabled')
            print(f'PASS {theme} {width}px: {len(routes)} routes, no overflow, SVG icons', flush=True)
    check(len(screens) == 154, '154 authenticated viewport/theme/page combinations')
    check(not errors, 'No uncaught JavaScript errors')

    page.goto(args.base + '/')
    check(page.locator('.feed-post').count() > 0, 'Real fixture feed is populated')
    # Read SVG symbols through the actual server, including all legacy alias uses.
    missing = page.evaluate('''async () => {
        const svg = new DOMParser().parseFromString(await (await fetch('/img/icons.svg')).text(), 'image/svg+xml');
        return [...document.querySelectorAll('svg use')].map(use => use.getAttribute('href').split('#')[1]).filter(id => !svg.getElementById(id));
    }''')
    check(not missing, 'Every rendered SVG use resolves to a local symbol')
    page.set_viewport_size({'width': 390, 'height': 844})
    set_theme(page, 'light')
    switch = page.get_by_role('switch', name='Giao diện tối')
    switch.focus()
    check(switch.evaluate('(el) => el === document.activeElement'), 'Mobile theme switch is keyboard focusable')
    page.keyboard.press('Space')
    expect(switch).to_have_attribute('aria-checked', 'true')
    expect(page.locator('html')).to_have_attribute('data-theme', 'dark')
    page.keyboard.press('Enter')
    expect(switch).to_have_attribute('aria-checked', 'false')
    expect(page.locator('html')).to_have_attribute('data-theme', 'light')
    check(True, 'Space and Enter toggle theme and accessible switch state')
    for control in (switch, page.locator('[data-theme-system]')):
        box = control.bounding_box()
        assert box['width'] >= 44 and box['height'] >= 44, box
    check(True, 'Both mobile theme controls have at least 44px touch targets')
    page.reload()
    check(page.locator('html').get_attribute('data-theme') == 'light', 'Explicit theme survives reload')
    second = context.new_page()
    second.goto(args.base + '/Recipe')
    set_theme(page, 'dark')
    expect(second.locator('html')).to_have_attribute('data-theme', 'dark')
    expect(second.get_by_role('switch')).to_have_attribute('aria-checked', 'true')
    check(True, 'Theme preference synchronizes across tabs')
    set_theme(page, 'system')
    expect(page.locator('[data-theme-system]')).to_have_attribute('aria-pressed', 'true')
    page.emulate_media(color_scheme='dark')
    expect(page.locator('html')).to_have_attribute('data-theme', 'dark')
    page.emulate_media(color_scheme='light')
    expect(page.locator('html')).to_have_attribute('data-theme', 'light')
    expect(switch).to_have_attribute('aria-checked', 'false')
    check(True, 'System preference follows operating system changes')
    switch.click()
    page.emulate_media(color_scheme='dark')
    page.emulate_media(color_scheme='light')
    expect(page.locator('html')).to_have_attribute('data-theme', 'dark')
    expect(page.locator('[data-theme-system]')).to_have_attribute('aria-pressed', 'false')
    check(True, 'Switch overrides system preference until device button is selected')
    second.evaluate("localStorage.setItem('cookshare.theme', 'invalid')")
    expect(page.locator('html')).to_have_attribute('data-theme-preference', 'system')
    expect(switch).to_have_attribute('aria-checked', 'false')
    check(True, 'Invalid cross-tab preference returns to system theme')
    set_theme(page, 'dark')
    second.evaluate("localStorage.removeItem('cookshare.theme')")
    expect(page.locator('html')).to_have_attribute('data-theme-preference', 'system')
    check(True, 'Removing saved preference restores system theme across tabs')
    page.emulate_media(reduced_motion='reduce')
    check(page.locator('.feed-post').first.evaluate('(el) => getComputedStyle(el).animationName') == 'none', 'Reduced motion suppresses feed animation')
    check(page.locator('.theme-switch-thumb').evaluate('(el) => getComputedStyle(el).transitionDuration') == '0s', 'Reduced motion suppresses switch animation')
    page.set_viewport_size({'width': 1366, 'height': 900})
    page.locator('[data-account-menu] summary').click()
    page.keyboard.press('Escape')
    check(page.locator('[data-account-menu]').get_attribute('open') is None, 'Account menu closes with Escape')

    for theme in ('light', 'dark'):
        guest = browser.new_context(color_scheme=theme, viewport={'width': 360, 'height': 844})
        guest.add_init_script("Storage.prototype.getItem = () => { throw new Error('blocked'); }; Storage.prototype.setItem = () => { throw new Error('blocked'); };")
        g = guest.new_page()
        for route in ('/Account/Login', '/Account/Register', '/Account/ForgotPassword', '/'):
            response = g.goto(args.base + route)
            assert response.status == 200
            assert g.locator('html').get_attribute('data-theme') == theme
            assert g.evaluate('document.documentElement.scrollWidth <= innerWidth + 1')
        check(True, f'{theme}: auth/guest pages and blocked storage fallback')
        g.get_by_role('switch').click()
        expect(g.locator('html')).to_have_attribute('data-theme', 'light' if theme == 'dark' else 'dark')
        g.locator('[data-theme-system]').click()
        expect(g.locator('html')).to_have_attribute('data-theme', theme)
        check(True, f'{theme}: switch and device controls work when storage is blocked')
        guest.close()
    nojs = browser.new_context(java_script_enabled=False, color_scheme='dark', viewport={'width': 390, 'height': 844})
    g = nojs.new_page()
    g.goto(args.base + '/Recipe')
    check(g.locator('body').evaluate('(el) => getComputedStyle(el).backgroundColor') == 'rgb(16, 27, 32)', 'No-JavaScript dark system fallback')
    check(not g.locator('[data-theme-control]').is_visible(), 'Theme controls stay hidden without JavaScript')
    check(g.locator('form[method=get] button[type=submit]').count() > 0, 'Native discovery form remains available without JavaScript')
    browser.close()

(OUT / 'results.json').write_text(json.dumps({'checks': checks, 'screens': screens, 'errors': errors}, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'COMPLETE {len(checks)} behavioral checks and {len(screens)} page combinations', flush=True)
