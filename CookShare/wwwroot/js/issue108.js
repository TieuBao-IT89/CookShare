/**
 * CookShare - Issue #108
 * UI interactions only.
 * Does not modify database or authentication.
 */

document.addEventListener("DOMContentLoaded", () => {
    const root = document.querySelector(".cs108");
    if (!root) return;

    // 1. Notifications: All / Unread filter
    const filters = root.querySelectorAll("[data-notification-filter]");

    const notifications = root.querySelectorAll("[data-notification-read]");

    const emptyUnread = root.querySelector("#cs108NoUnread");

    filters.forEach((button) => {
        button.addEventListener("click", () => {
            const selected = button.dataset.notificationFilter;

            filters.forEach((item) => {
                const active = item === button;
                item.classList.toggle("active", active);
                item.setAttribute("aria-pressed", String(active));
            });

            let visible = 0;

            notifications.forEach((item) => {
                const show =
                    selected === "all" ||
                    item.dataset.notificationRead === "false";

                item.hidden = !show;
                if (show) visible++;
            });

            if (emptyUnread) {
                emptyUnread.hidden = !(selected === "unread" && visible === 0);
            }
        });
    });

    // 2. Recipe Form: Image URL preview
    const imageInput = root.querySelector("#cs108ImageInput");
    const imagePreview = root.querySelector("#cs108PreviewImage");

    if (imageInput && imagePreview) {
        const fallback = imagePreview.dataset.fallback;

        function validImageUrl(value) {
            if (
                value.startsWith("/") &&
                !value.startsWith("//") &&
                !value.includes("\\")
            ) {
                return true;
            }

            try {
                const url = new URL(value);
                return url.protocol === "https:";
            } catch {
                return false;
            }
        }

        function updateImagePreview() {
            const value = imageInput.value.trim();

            imagePreview.onerror = () => {
                imagePreview.onerror = null;
                imagePreview.src = fallback;
            };

            imagePreview.src = value && validImageUrl(value) ? value : fallback;
        }

        imageInput.addEventListener("input", updateImagePreview);
        imageInput.addEventListener("change", updateImagePreview);

        updateImagePreview();
    }

    // 3. Recipe Details: Ingredient checklist progress
    const ingredientChecks = root.querySelectorAll(".cs108-ingredient-check");

    const progress = root.querySelector("#cs108IngredientProgress");

    if (progress) {
        const updateProgress = () => {
            let checked = 0;

            ingredientChecks.forEach((input) => {
                const isChecked = input.checked;

                input
                    .closest(".ingredient-item")
                    ?.classList.toggle("checked", isChecked);

                if (isChecked) checked++;
            });

            progress.textContent = `Đã chuẩn bị ${checked}/${ingredientChecks.length} nguyên liệu`;
        };

        ingredientChecks.forEach((input) => {
            input.addEventListener("change", updateProgress);
        });

        updateProgress();
    }
});
