"""Fetch the reviewed Lucide subset at an immutable commit; no runtime dependency."""
import concurrent.futures
import hashlib
import json
import re
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
COMMIT = 'f8feea7a02ea461c9f2a4b4d851512aae6ec933f'
ICONS = sorted(set('''house users message-circle share-2 chef-hat search arrow-right
plus heart bookmark clock leaf layout-grid menu user square-pen trash image check
eye sparkles lock x camera chevron-right arrow-down arrow-left arrow-up bell book-open
cooking-pot soup cake-slice calendar compass earth flag folder folder-open martini minus
send pencil pizza sliders-horizontal star pin user-check user-plus utensils sun moon
settings monitor external-link log-out'''.split()))


def fetch(name):
    path = 'LICENSE' if name == 'LICENSE' else f'icons/{name}.svg'
    try:
        with urllib.request.urlopen(f'https://raw.githubusercontent.com/lucide-icons/lucide/{COMMIT}/{path}', timeout=30) as response:
            return name, response.read()
    except Exception as error:
        raise RuntimeError(f'Could not fetch {path}') from error


def main():
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        files = dict(pool.map(fetch, ICONS + ['LICENSE']))
    symbols = []
    for name in ICONS:
        svg = files[name].decode('utf-8')
        contents = re.search(r'<svg\b[^>]*>([\s\S]+)</svg>', svg).group(1).strip()
        symbols.append(f'<symbol id="{name}" viewBox="0 0 24 24">{contents}</symbol>')
    target = ROOT / 'CookShare/wwwroot/img/icons.svg'
    target.write_text('<svg xmlns="http://www.w3.org/2000/svg">\n<!-- Lucide ISC / Feather MIT: /lib/lucide/LICENSE.txt -->\n' + '\n'.join(symbols) + '\n</svg>\n', encoding='utf-8', newline='\n')
    license_path = ROOT / 'CookShare/wwwroot/lib/lucide/LICENSE.txt'
    license_path.parent.mkdir(parents=True, exist_ok=True)
    license_path.write_bytes(files['LICENSE'])
    manifest = {'repository': 'https://github.com/lucide-icons/lucide', 'commit': COMMIT,
                'icons': {name: hashlib.sha256(files[name]).hexdigest() for name in ICONS},
                'spriteSha256': hashlib.sha256(target.read_bytes()).hexdigest()}
    (ROOT / 'tools/assets/icons.lock.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(f'Wrote {len(ICONS)} Lucide SVG symbols ({target.stat().st_size} bytes).')


if __name__ == '__main__':
    main()
