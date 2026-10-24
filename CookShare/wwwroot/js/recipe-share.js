(function () {
    "use strict";

    const root = document.querySelector("[data-recipe-share]");
    if (!root) return;

    const status = document.getElementById("recipeShareStatus");
    const dialog = document.querySelector("[data-recipe-share-dialog]");
    const link = document.getElementById("recipeShareLink");

    function notice(message, isError) {
        window.CookShareUI?.toast(message, isError ? "error" : "success");
        if (!status) return;
        status.hidden = false;
        status.textContent = message;
        status.classList.toggle("error", Boolean(isError));
    }

    function showLink(url) {
        if (link) {
            link.value = url;
            link.focus();
            link.select();
        }
        if (dialog && typeof dialog.showModal === "function" && !dialog.open) dialog.showModal();
        else window.prompt("Sao chép liên kết công thức:", url);
    }

    async function copyLink(url) {
        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(url);
                notice(root.dataset.shareSuccess, false);
                return;
            }
        } catch {
            /* The clipboard can be blocked; the selectable link remains available. */
        }

        showLink(url);
        notice(root.dataset.shareFailure, true);
    }

    root.addEventListener("click", function (event) {
        const url = root.dataset.shareUrl;
        if (event.target.closest("[data-recipe-share-native]")) {
            if (navigator.share) {
                navigator.share({ title: root.dataset.shareTitle, url: url }).catch(function (error) {
                    if (error && error.name === "AbortError") return;
                    copyLink(url);
                });
            } else {
                copyLink(url);
            }
            return;
        }

        if (event.target.closest("[data-recipe-copy-link]")) {
            copyLink(url);
            return;
        }

        if (event.target.closest("[data-recipe-print]")) window.print();
    });
})();
