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

    // 2. Recipe Form: Local image selection and preview
    const imageInput = root.querySelector("#cs108ImageInput");
    const imagePreview = root.querySelector("#cs108PreviewImage");
    const imageStatus = root.querySelector("#cs108ImageStatus");

    if (imageInput && imagePreview && imageInput.type === "file") {
        const fallback = imagePreview.dataset.fallback;
        const originalSource = imagePreview.getAttribute("src") || fallback;
        const maxSize = Number(imageInput.dataset.maxSize) || 5 * 1024 * 1024;
        const allowedTypes = new Set(["image/jpeg", "image/png", "image/webp"]);
        let previewUrl = null;

        function releasePreview() {
            if (previewUrl) URL.revokeObjectURL(previewUrl);
            previewUrl = null;
        }

        function showImageStatus(message, error = false) {
            if (!imageStatus) return;
            imageStatus.textContent = message;
            imageStatus.classList.toggle("is-error", error);
        }

        function restorePreview() {
            imagePreview.onload = null;
            imagePreview.onerror = () => {
                imagePreview.onerror = null;
                imagePreview.src = fallback;
            };
            imagePreview.src = originalSource;
        }

        function updateSelectedImage() {
            releasePreview();
            const file = imageInput.files?.[0];
            imageInput.setCustomValidity("");
            imageInput.removeAttribute("aria-invalid");

            if (!file) {
                restorePreview();
                showImageStatus("");
                return;
            }

            let error = "";
            if (!/\.(jpe?g|png|webp)$/i.test(file.name) || (file.type && !allowedTypes.has(file.type))) {
                error = "Vui lòng chọn ảnh JPG, PNG hoặc WebP.";
            } else if (!file.size) {
                error = "Ảnh đã chọn không có dữ liệu. Vui lòng chọn ảnh khác.";
            } else if (file.size > maxSize) {
                error = "Ảnh không được vượt quá 5 MB. Vui lòng chọn ảnh nhỏ hơn.";
            }

            if (error) {
                imageInput.setCustomValidity(error);
                imageInput.setAttribute("aria-invalid", "true");
                restorePreview();
                showImageStatus(error, true);
                return;
            }

            const validation = root.querySelector("#ImageFileValidation");
            if (validation) validation.textContent = "";
            const selectedUrl = URL.createObjectURL(file);
            previewUrl = selectedUrl;
            showImageStatus(`Đang xem trước: ${file.name}`);

            imagePreview.onload = () => {
                if (previewUrl === selectedUrl) {
                    showImageStatus(`Đã chọn: ${file.name}. Ảnh sẽ được tải lên khi gửi công thức.`);
                }
            };
            imagePreview.onerror = () => {
                if (previewUrl !== selectedUrl) return;
                releasePreview();
                const message = "Không đọc được ảnh đã chọn. Vui lòng chọn ảnh khác.";
                imageInput.setCustomValidity(message);
                imageInput.setAttribute("aria-invalid", "true");
                restorePreview();
                showImageStatus(message, true);
            };
            imagePreview.src = selectedUrl;
        }

        imageInput.addEventListener("change", updateSelectedImage);
        imageInput.addEventListener("cancel", () => {
            if (!imageInput.files?.length) updateSelectedImage();
        });
        window.addEventListener("pagehide", releasePreview);
        imagePreview.onerror = () => {
            imagePreview.onerror = null;
            imagePreview.src = fallback;
        };
    } else if (imageInput && imagePreview) {
        const fallback = imagePreview.dataset.fallback;

        // Match RecipeImageReference for previews without changing the submitted value.
        function resolveImageSource(value) {
            let reference = value.trim();
            if (!reference || reference.length > 2000 || /[\u0000-\u001f\u007f-\u009f\\]/.test(reference)) {
                return null;
            }
            if (reference.startsWith("~/")) reference = reference.slice(1);

            if (reference.startsWith("/")) {
                if (reference.startsWith("//")) return null;
                try {
                    const path = decodeURIComponent(reference.split(/[?#]/, 1)[0]);
                    if (/[\u0000-\u001f\u007f-\u009f\\]/.test(path) || path.startsWith("//")
                        || path.split("/").some(segment => segment === "." || segment === "..")) {
                        return null;
                    }
                    return reference;
                } catch {
                    return null;
                }
            }

            try {
                const url = new URL(reference);
                return ["http:", "https:"].includes(url.protocol) && url.hostname && !url.username && !url.password
                    ? reference : null;
            } catch {
                return null;
            }
        }

        function updateImagePreview() {
            const source = resolveImageSource(imageInput.value);
            const hasValue = imageInput.value.trim().length > 0;
            function status(message, error = false) {
                if (!imageStatus) return;
                imageStatus.textContent = message;
                imageStatus.classList.toggle("is-error", error);
            }
            status(source ? "Đang tải ảnh xem trước…" : hasValue ? "Đường dẫn ảnh chưa hợp lệ. Vui lòng kiểm tra lại." : "", !source && hasValue);

            imagePreview.onload = () => {
                if (source) status("Ảnh đã sẵn sàng để xem trước.");
            };

            imagePreview.onerror = () => {
                imagePreview.onerror = null;
                imagePreview.onload = null;
                imagePreview.src = fallback;
                status("Không tải được ảnh. Kiểm tra đường dẫn hoặc chọn ảnh khác; nội dung đã nhập vẫn được giữ lại.", true);
            };

            imagePreview.src = source || fallback;
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
