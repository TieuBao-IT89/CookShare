/* Runs before styles: never wait for DOMContentLoaded to select the theme. */
(function () {
    'use strict';
    const system = matchMedia('(prefers-color-scheme: dark)');
    let storage;
    let preference = 'system';
    let transitionTimer;
    try {
        storage = localStorage;
        const saved = storage.getItem('cookshare.theme');
        if (['light', 'dark', 'system'].includes(saved)) preference = saved;
    } catch { /* Storage restrictions leave a working system preference. */ }
    function apply() {
        const theme = preference === 'system' ? (system.matches ? 'dark' : 'light') : preference;
        document.documentElement.dataset.theme = theme;
        document.documentElement.dataset.themePreference = preference;
        document.querySelectorAll('[data-theme-switch]').forEach(control => {
            control.setAttribute('aria-checked', String(theme === 'dark'));
            control.title = theme === 'dark' ? 'Chuyển sang giao diện sáng' : 'Chuyển sang giao diện tối';
        });
        document.querySelectorAll('[data-theme-system]').forEach(control => {
            control.setAttribute('aria-pressed', String(preference === 'system'));
        });
        document.querySelector('meta[name="theme-color"]')?.setAttribute('content', theme === 'dark' ? '#101b20' : '#f8f5ef');
    }
    function setPreference(value, persist) {
        if (!['light', 'dark', 'system'].includes(value)) return;
        preference = value;
        if (persist) {
            try { storage?.setItem('cookshare.theme', preference); } catch { /* Still apply for this page. */ }
        }
        apply();
    }
    function choose(value) {
        clearTimeout(transitionTimer);
        document.documentElement.classList.add('theme-transition');
        setPreference(value, true);
        const label = { light: 'Sáng', dark: 'Tối', system: 'Theo thiết bị' }[preference];
        const status = document.getElementById('themeStatus');
        if (status) status.textContent = `Giao diện: ${label}.`;
        transitionTimer = setTimeout(() => document.documentElement.classList.remove('theme-transition'), 360);
    }
    apply();
    system.addEventListener('change', () => { if (preference === 'system') apply(); });
    addEventListener('storage', event => {
        if (event.storageArea === storage && (event.key === 'cookshare.theme' || event.key === null)) {
            const value = event.newValue;
            setPreference(['light', 'dark', 'system'].includes(value) ? value : 'system', false);
        }
    });
    document.addEventListener('DOMContentLoaded', () => {
        apply();
        document.querySelectorAll('[data-theme-control]').forEach(control => { control.hidden = false; });
        document.querySelectorAll('[data-theme-switch]').forEach(control => {
            control.addEventListener('click', () => choose(document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark'));
        });
        document.querySelectorAll('[data-theme-system]').forEach(control => {
            control.addEventListener('click', () => choose('system'));
        });
    });
})();
