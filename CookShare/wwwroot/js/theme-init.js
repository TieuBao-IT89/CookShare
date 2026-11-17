/* Runs before styles: never wait for DOMContentLoaded to select the theme. */
(function () {
    'use strict';
    const system = matchMedia('(prefers-color-scheme: dark)');
    let preference = 'system';
    try {
        const saved = localStorage.getItem('cookshare.theme');
        if (['light', 'dark', 'system'].includes(saved)) preference = saved;
    } catch { /* Storage restrictions leave a working system preference. */ }
    function apply() {
        const theme = preference === 'system' ? (system.matches ? 'dark' : 'light') : preference;
        document.documentElement.dataset.theme = theme;
        document.documentElement.dataset.themePreference = preference;
        document.querySelectorAll('[data-theme-select]').forEach(select => { select.value = preference; });
        document.querySelector('meta[name="theme-color"]')?.setAttribute('content', theme === 'dark' ? '#111815' : '#f7f6f2');
    }
    function setPreference(value, persist) {
        if (!['light', 'dark', 'system'].includes(value)) return;
        preference = value;
        if (persist) {
            try { localStorage.setItem('cookshare.theme', preference); } catch { /* Still apply for this page. */ }
        }
        apply();
    }
    apply();
    system.addEventListener('change', () => { if (preference === 'system') apply(); });
    addEventListener('storage', event => {
        if (event.key === 'cookshare.theme' || event.key === null) setPreference(event.newValue || 'system', false);
    });
    document.addEventListener('DOMContentLoaded', () => {
        apply();
        document.querySelectorAll('[data-theme-control]').forEach(control => { control.hidden = false; });
        document.querySelectorAll('[data-theme-select]').forEach(select => {
            select.addEventListener('change', () => {
                document.documentElement.classList.add('theme-transition');
                setPreference(select.value, true);
                const label = { light: 'Sáng', dark: 'Tối', system: 'Theo thiết bị' }[preference];
                const status = document.getElementById('themeStatus');
                if (status) status.textContent = `Giao diện: ${label}.`;
                setTimeout(() => document.documentElement.classList.remove('theme-transition'), 360);
            });
        });
    });
})();
