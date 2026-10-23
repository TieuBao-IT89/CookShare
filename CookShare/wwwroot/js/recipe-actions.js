// MVC form behavior only; recipe data stays on the server.
(function () {
    "use strict";

    document.querySelectorAll('form[data-confirm-delete]').forEach(form => {
        let confirming = false;
        form.addEventListener('submit', async event => {
            event.preventDefault();
            if (confirming) return;
            confirming = true;
            try {
                const confirmed = await window.CookShareUI.confirmDelete(form.dataset.confirmDelete);
                if (confirmed) form.submit();
            } finally {
                confirming = false;
            }
        });
    });

    const pendingForms = new Map();

    function restoreForm(form) {
        const state = pendingForms.get(form);
        if (!state) return;
        state.buttons.forEach(([button, disabled]) => { button.disabled = disabled; });
        if (state.ariaBusy === null) form.removeAttribute('aria-busy');
        else form.setAttribute('aria-busy', state.ariaBusy);
        pendingForms.delete(form);
    }

    // Run after form-level validators so rejected submissions remain editable.
    document.addEventListener('submit', event => {
        const form = event.target;
        if (!form.matches('form[data-prevent-duplicate-submit]')) return;
        if (pendingForms.has(form)) {
            event.preventDefault();
            return;
        }
        if (event.defaultPrevented) return;
        if (!form.checkValidity()) {
            event.preventDefault();
            form.reportValidity();
            return;
        }
        const validator = window.jQuery?.(form).data('validator');
        if (validator && (!validator.form() || validator.pendingRequest > 0)) {
            event.preventDefault();
            return;
        }

        const buttons = Array.from(form.querySelectorAll('button[type="submit"], input[type="submit"], button:not([type])'));
        pendingForms.set(form, {
            ariaBusy: form.getAttribute('aria-busy'),
            buttons: buttons.map(button => [button, button.disabled])
        });
        form.setAttribute('aria-busy', 'true');
        buttons.forEach(button => { button.disabled = true; });
        // A later listener may cancel submission after this one has run.
        queueMicrotask(() => {
            if (event.defaultPrevented) restoreForm(form);
        });
    });

    // Browsers may restore a submitted form from the back/forward cache.
    window.addEventListener('pageshow', () => {
        pendingForms.forEach((_, form) => restoreForm(form));
    });
})();
