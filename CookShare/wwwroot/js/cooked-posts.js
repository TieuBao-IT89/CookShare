(() => {
    'use strict';
    document.querySelectorAll('[data-social-form]').forEach(form => {
        const input = form.querySelector('[data-social-image]');
        const preview = form.querySelector('[data-social-preview]');
        let previewUrl;
        input?.addEventListener('change', () => {
            if (previewUrl) URL.revokeObjectURL(previewUrl);
            const file = input.files?.[0];
            if (file && preview) {
                previewUrl = URL.createObjectURL(file);
                preview.src = previewUrl;
                preview.hidden = false;
            }
        });
        form.addEventListener('submit', event => {
            if (form.dataset.submitting === 'true') {
                event.preventDefault();
                return;
            }
            form.dataset.submitting = 'true';
            // Keep the clicked button enabled so its named disposition reaches the server.
            form.querySelectorAll('[data-social-submit]').forEach(button => {
                button.setAttribute('aria-disabled', 'true');
            });
            const status = form.querySelector('[data-social-status]');
            if (status) status.textContent = 'Đang gửi, vui lòng chờ…';
        });
        window.addEventListener('pageshow', () => {
            delete form.dataset.submitting;
            form.querySelectorAll('[data-social-submit]').forEach(button => button.removeAttribute('aria-disabled'));
            const status = form.querySelector('[data-social-status]');
            if (status) status.textContent = '';
        });
    });
})();
