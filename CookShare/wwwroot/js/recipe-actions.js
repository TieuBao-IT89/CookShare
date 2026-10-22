// Confirm MVC delete forms on Index and Details; recipe data stays on the server.
(function () {
    "use strict";

    document.querySelectorAll('form[data-confirm-delete]').forEach(form => {
        form.addEventListener('submit', async event => {
            event.preventDefault();
            const confirmed = await window.CookShareUI.confirmDelete(form.dataset.confirmDelete);
            if (confirmed) form.submit();
        });
    });
})();
