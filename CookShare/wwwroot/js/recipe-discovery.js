/* Enhance native GET filters to a mobile sheet without duplicating controls. */
(function () {
    "use strict";
    const details = document.querySelector("[data-discovery-filters]");
    const dialog = document.querySelector("[data-discovery-filter-dialog]");
    const content = document.querySelector("[data-discovery-filter-content]");
    const dialogContent = dialog?.querySelector("[data-discovery-dialog-content]");
    if (!details || !dialog || !content || !dialogContent || typeof dialog.showModal !== "function") return;
    const trigger = details.querySelector("[data-discovery-filter-trigger]");
    const close = dialog.querySelector("[data-discovery-filter-close]");
    const narrow = matchMedia("(max-width: 719px)");
    function adapt() {
        if (dialog.open) dialog.close();
        details.classList.toggle("is-enhanced", narrow.matches);
        if (narrow.matches) {
            dialogContent.append(content);
            details.open = false;
            trigger.setAttribute("aria-haspopup", "dialog");
        } else {
            details.append(content);
            details.open = Number(details.dataset.activeFilterCount) > 0;
            trigger.removeAttribute("aria-haspopup");
        }
    }
    trigger.addEventListener("click", function (event) {
        if (!narrow.matches) return;
        event.preventDefault();
        dialog.showModal();
        content.querySelector("input, select")?.focus();
    });
    function closeDialog() {
        dialog.close();
        if (narrow.matches) trigger.focus();
    }
    close.addEventListener("click", closeDialog);
    dialog.addEventListener("cancel", function (event) {
        event.preventDefault();
        closeDialog();
    });
    dialog.addEventListener("close", function () { if (narrow.matches) trigger.focus(); });
    narrow.addEventListener("change", adapt);
    adapt();
})();
