(function () {
    "use strict";

    const input = document.getElementById("Image");
    const preview = document.getElementById("recipeImagePreview");
    const image = document.getElementById("recipeImagePreviewImg");
    const form = document.querySelector(".recipe-studio-form");
    if (!input || !preview || !image || !form) return;

    function usableSource(value) {
        return value.startsWith("/") || value.startsWith("https://") || value.startsWith("http://");
    }

    function renderPreview() {
        const value = input.value.trim();
        if (!usableSource(value)) {
            preview.hidden = true;
            image.removeAttribute("src");
            return;
        }

        image.src = value;
    }

    image.addEventListener("load", function () {
        preview.hidden = false;
    });

    image.addEventListener("error", function () {
        preview.hidden = true;
    });

    input.addEventListener("input", renderPreview);
    renderPreview();

    const button = form.querySelector("[data-submit]");
    if (!button) return;
    const label = button.textContent;

    form.addEventListener("submit", function () {
        window.setTimeout(function () {
            if (form.querySelector(".input-validation-error, .field-validation-error:not(:empty)")) return;
            button.classList.add("is-submitting");
            button.setAttribute("aria-busy", "true");
            button.textContent = "Đang lưu…";
        }, 0);
    });

    window.addEventListener("pageshow", function () {
        button.classList.remove("is-submitting");
        button.removeAttribute("aria-busy");
        button.textContent = label;
    });
})();
