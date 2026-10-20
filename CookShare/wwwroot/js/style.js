/* Shared, dependency-free presentation behavior. Recipe data stays in recipe.js. */
(function () {
    "use strict";
    const menuButton = document.querySelector('.menu-btn');
    const navLinks = document.getElementById('primaryNav');
    function closeMenu() {
        navLinks?.classList.remove('open');
        menuButton?.setAttribute('aria-expanded', 'false');
        menuButton?.setAttribute('aria-label', 'Mở menu điều hướng');
    }
    menuButton?.addEventListener('click', () => {
        const open = navLinks.classList.toggle('open');
        menuButton.setAttribute('aria-expanded', String(open));
        menuButton.setAttribute('aria-label', open ? 'Đóng menu điều hướng' : 'Mở menu điều hướng');
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && navLinks?.classList.contains('open')) {
            closeMenu();
            menuButton.focus();
        }
    });
    document.addEventListener('click', event => {
        if (!event.target.closest('.site-header')) closeMenu();
        if (event.target.closest('#primaryNav a')) closeMenu();
    });
    matchMedia('(min-width: 720px)').addEventListener('change', closeMenu);

    let toastTimer;
    function toast(message, type = 'success') {
        const element = document.getElementById('uiToast');
        if (!element) return;
        clearTimeout(toastTimer);
        element.textContent = message;
        element.classList.toggle('error', type === 'error');
        element.hidden = false;
        toastTimer = setTimeout(() => { element.hidden = true; }, 5000);
    }
    function flash(message) {
        try { sessionStorage.setItem('cookshare_ui_flash', message); }
        catch { toast(message); }
    }
    function confirmDelete(message) {
        const dialog = document.getElementById('confirmDialog');
        if (!dialog || typeof dialog.showModal !== 'function') return Promise.resolve(confirm(message));
        if (dialog.open) return Promise.resolve(false);
        document.getElementById('confirmMessage').textContent = message;
        dialog.returnValue = '';
        return new Promise(resolve => {
            dialog.addEventListener('close', () => resolve(dialog.returnValue === 'confirm'), { once: true });
            dialog.showModal();
        });
    }
    function imageFallback(image) {
        if (image.tagName !== 'IMG' || image.dataset.fallbackApplied) return;
        image.dataset.fallbackApplied = 'true';
        image.src = '/img/recipe-placeholder.svg';
        image.removeAttribute('srcset');
    }
    document.addEventListener('error', event => imageFallback(event.target), true);
    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('img').forEach(image => {
            if (image.complete && !image.naturalWidth) imageFallback(image);
        });
        try {
            const message = sessionStorage.getItem('cookshare_ui_flash');
            if (message) { toast(message); sessionStorage.removeItem('cookshare_ui_flash'); }
        } catch { /* A blocked sessionStorage should not stop navigation. */ }
    });
    document.querySelectorAll('[data-password-target]').forEach(button => {
        button.addEventListener('click', () => {
            const input = document.getElementById(button.dataset.passwordTarget);
            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            button.setAttribute('aria-pressed', String(show));
            button.setAttribute('aria-label', show ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
        });
    });
    document.querySelectorAll('[data-feature-notice]').forEach(button => {
        button.addEventListener('click', () => toast(button.dataset.featureNotice));
    });
    document.querySelectorAll('[data-auth-form]').forEach(form => {
        const password = form.querySelector('#registerPassword');
        const confirmation = form.querySelector('#confirmPassword');
        if (confirmation) {
            const validate = () => confirmation.setCustomValidity(
                confirmation.value && confirmation.value !== password.value ? 'Mật khẩu xác nhận chưa khớp.' : ''
            );
            confirmation.addEventListener('input', validate);
            password.addEventListener('input', validate);
        }
        form.addEventListener('submit', event => {
            event.preventDefault();
            const status = form.querySelector('.auth-status');
            status.textContent = form.dataset.authForm === 'login'
                ? 'Đăng nhập đang được hoàn thiện. Bạn vẫn có thể khám phá và lưu công thức trên thiết bị này.'
                : 'Đăng ký đang được hoàn thiện. CookShare chưa tạo tài khoản hoặc lưu mật khẩu của bạn.';
            status.hidden = false;
        });
    });
    window.CookShareUI = { toast, flash, confirmDelete };
})();
