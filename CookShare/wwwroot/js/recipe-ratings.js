/**
 * Account ratings are rendered by Razor and confirmed by the Ratings API.
 * Browser storage is never a source of ratings.
 */
(function () {
    "use strict";
    if (window.CookShareRatings) return;
    let busy = false;
    let invalidSession = false;
    const section = document.querySelector("[data-db-recipe-rating]");
    if (!section) return;
    const form = section.querySelector("[data-rating-form]");
    const status = section.querySelector("[data-rating-status]");
    const viewerId = section.dataset.viewerId || null;
    const recipeId = Number(section.dataset.recipeId);

    function notice(message, error = false) {
        status.textContent = message;
        status.classList.toggle("error", error);
        status.hidden = !message;
    }

    function lock(locked) {
        busy = locked;
        section.setAttribute("aria-busy", String(locked));
        if (!form) return;
        form.querySelector("fieldset").disabled = locked || invalidSession;
        const button = form.querySelector("[data-rating-submit]");
        button.disabled = locked || invalidSession;
        button.textContent = locked ? "Đang lưu…"
            : section.dataset.userRating ? "Cập nhật đánh giá" : "Gửi đánh giá";
    }

    function invalidate(message) {
        invalidSession = true;
        section.dataset.userRating = "";
        const controls = section.querySelector("[data-rating-controls]");
        controls.replaceChildren(document.createTextNode(message + " "));
        const reload = document.createElement("a");
        reload.href = window.location.href;
        reload.textContent = "Tải lại trang";
        reload.className = "text-link";
        controls.append(reload);
        notice(message, true);
    }

    function validate(state) {
        if (!state || state.recipeId !== recipeId || !Number.isSafeInteger(state.ratingCount) || state.ratingCount < 0
            || !Number.isFinite(state.averageRating)
            || (state.ratingCount === 0 ? state.averageRating !== 0 : state.averageRating < 1 || state.averageRating > 5)
            || (state.userRating !== null && (!Number.isInteger(state.userRating) || state.userRating < 1 || state.userRating > 5))
            || typeof state.canRate !== "boolean" || (state.viewerId !== null && typeof state.viewerId !== "string")) {
            throw Error("Phản hồi không hợp lệ. Vui lòng tải lại trang để kiểm tra đánh giá.");
        }
        if (state.viewerId !== viewerId) {
            invalidate("Tài khoản đã thay đổi. Vui lòng tải lại trang.");
            return false;
        }
        if (form && !state.canRate) {
            invalidate("Bạn không thể đánh giá công thức này trong phiên hiện tại.");
            return false;
        }
        return true;
    }

    function apply(state) {
        document.querySelectorAll("[data-db-rating-summary]").forEach(element => {
            if (Number(element.dataset.recipeId) !== recipeId) return;
            element.dataset.ratingCount = String(state.ratingCount);
            element.dataset.averageRating = String(state.averageRating);
            element.querySelector("[data-rating-summary-text]").textContent = state.ratingCount
                ? state.averageRating.toLocaleString("vi-VN", { minimumFractionDigits: 1, maximumFractionDigits: 1 }) + " / 5 · " + state.ratingCount + " đánh giá"
                : "Chưa có đánh giá";
            const icon = element.querySelector(".fa-star");
            icon.classList.toggle("fa-solid", state.ratingCount > 0);
            icon.classList.toggle("fa-regular", state.ratingCount === 0);
        });
        section.dataset.userRating = state.userRating || "";
        if (!form || invalidSession) return;
        form.querySelectorAll('[name="Stars"]').forEach(input => { input.checked = Number(input.value) === state.userRating; });
        form.querySelector("[data-rating-own]").textContent = state.userRating
            ? "Bạn đã đánh giá " + state.userRating + " sao. Có thể chọn lại để cập nhật."
            : "Bạn chưa đánh giá công thức này.";
    }

    async function request(options = {}) {
        let response;
        try { response = await fetch(section.dataset.ratingsUrl, { credentials: "same-origin", cache: "no-store", ...options }); }
        catch { throw Error("Không thể kết nối. Vui lòng kiểm tra mạng và thử lại."); }
        if (response.redirected || response.status === 401) {
            invalidate("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
            return null;
        }
        if (response.status === 403) {
            invalidate("Bạn không có quyền đánh giá công thức này. Vui lòng tải lại trang.");
            return null;
        }
        let data;
        if (response.headers.get("Content-Type")?.includes("application/json")) {
            try { data = await response.json(); } catch { /* Handled by validation below. */ }
        }
        if (!response.ok) {
            throw Error(response.status === 400 ? "Vui lòng chọn số sao từ 1 đến 5; nếu vẫn lỗi, hãy tải lại trang."
                : response.status === 404 ? "Công thức không còn tồn tại."
                    : "Chưa thể lưu đánh giá. Vui lòng thử lại sau.");
        }
        return validate(data) ? data : null;
    }

    async function refresh() {
        if (busy || invalidSession) return;
        lock(true);
        try {
            const state = await request({ headers: { "Accept": "application/json" } });
            if (state) { apply(state); notice(""); }
        } catch (error) { notice(error.message, true); }
        finally { lock(false); }
    }

    form?.addEventListener("submit", async event => {
        event.preventDefault();
        if (busy || invalidSession) return;
        const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const stars = Number(form.querySelector('[name="Stars"]:checked')?.value);
        if (!Number.isInteger(stars) || stars < 1 || stars > 5) {
            notice("Vui lòng chọn số sao từ 1 đến 5.", true);
            return;
        }
        if (!token) { notice("Phiên làm việc không hợp lệ. Vui lòng tải lại trang.", true); return; }
        notice("");
        lock(true);
        try {
            const body = new URLSearchParams({ Stars: String(stars), __RequestVerificationToken: token });
            const state = await request({
                method: "POST",
                headers: { "Accept": "application/json", "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8", "X-Requested-With": "XMLHttpRequest" },
                body: body.toString()
            });
            if (state) { apply(state); notice("Đã lưu đánh giá của bạn."); }
        } catch (error) { notice(error.message, true); }
        finally { lock(false); }
    });

    window.addEventListener("pageshow", event => { if (event.persisted) void refresh(); });
    document.addEventListener("visibilitychange", () => { if (!document.hidden) void refresh(); });
    window.CookShareRatings = { refresh, syncAllRecipeRatingsUI: refresh };
})();
