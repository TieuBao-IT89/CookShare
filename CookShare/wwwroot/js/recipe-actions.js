// Confirm MVC delete forms on Index and Details; recipe data stays on the server.
(function () {
    "use strict";

    document.querySelectorAll(".ingredient-item input").forEach(input => {
        const sync = () => {
            input.closest(".ingredient-item")?.classList.toggle("checked", input.checked);
            const total = document.querySelectorAll(".ingredient-item input").length;
            const done = document.querySelectorAll(".ingredient-item input:checked").length;
            const counter = document.getElementById("ingredientCheckedCount");
            if (counter) counter.textContent = done + "/" + total;
        };
        input.addEventListener("change", sync);
    });

    document.querySelectorAll('form[data-confirm-delete]').forEach(form => {
        form.addEventListener('submit', async event => {
            event.preventDefault();
            const confirmed = await window.CookShareUI.confirmDelete(form.dataset.confirmDelete);
            if (confirmed) form.submit();
        });
    });
})();
