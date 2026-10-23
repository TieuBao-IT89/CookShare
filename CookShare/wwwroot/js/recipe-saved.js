/**
 * Saved Recipes presentation from Issue #45, backed by authenticated Favorites.
 * Database recipe IDs and server responses own state. Legacy browser data stays untouched.
 */
(function () {
    "use strict";
    if (window.CookShareSaved) return;

    const selector = ".recipe-save-btn[data-db-recipe-save][data-recipe-id]";
    const states = new Map();
    const pending = new Map();
    const collections = [
        ["savedRecipeGrid", "savedEmptyState"],
        ["profileFavoriteGrid", "profileFavoriteEmptyState"]
    ];
    let generation = 0;
    let revalidation = null;
    let reconcileAfterWrites = false;
    let sessionInvalid = false;

    function buttons(recipeId) {
        return Array.from(document.querySelectorAll(selector))
            .filter(button => recipeId === undefined || Number(button.dataset.recipeId) === Number(recipeId));
    }

    function validId(value) {
        return Number.isSafeInteger(Number(value)) && Number(value) > 0;
    }

    function isRecipeSaved(recipeId) {
        return states.get(Number(recipeId)) ?? buttons(recipeId)[0]?.dataset.isSaved === "true";
    }

    function getSavedRecipeIds() {
        return Array.from(states).filter(([, saved]) => saved).map(([id]) => id);
    }

    function clearStatus(button) {
        const scope = button.closest("[data-db-save-control]") || button.parentElement;
        const notice = scope.querySelector("[data-save-status]");
        if (!notice) return;
        notice.textContent = "";
        notice.hidden = true;
        notice.classList.remove("error");
    }

    function notify(message, error = false) {
        window.CookShareUI?.toast(message, error ? "error" : "success");
    }

    function updateSavedRecipeUI(recipeId) {
        const saved = isRecipeSaved(recipeId);
        buttons(recipeId).forEach(button => {
            clearStatus(button);
            button.dataset.isSaved = String(saved);
            button.classList.toggle("saved", saved);
            button.setAttribute("aria-pressed", String(saved));
            button.setAttribute("title", saved ? "Bỏ lưu công thức này" : "Lưu công thức này");
            button.setAttribute("aria-label", `${saved ? "Bỏ lưu" : "Lưu"} ${button.dataset.recipeName || "công thức"}`);
            const text = button.querySelector(".save-text");
            if (text) text.textContent = pending.has(Number(recipeId)) ? "Đang lưu…"
                : saved ? button.classList.contains("saved-card-unsave-btn") ? "Bỏ lưu công thức" : "Đã lưu" : "Lưu";
            const form = button.closest("[data-db-save-form]");
            if (form) form.action = saved ? button.dataset.unsaveUrl : button.dataset.saveUrl;
        });
    }

    function syncSavedRecipeUI() {
        buttons().forEach(button => {
            const id = Number(button.dataset.recipeId);
            if (validId(id) && !states.has(id)) states.set(id, button.dataset.isSaved === "true");
        });
        states.forEach((_, id) => updateSavedRecipeUI(id));
    }

    function lock(recipeId, busy) {
        buttons(recipeId).forEach(button => {
            if (button.tagName !== "BUTTON") return;
            button.disabled = busy || sessionInvalid;
            button.setAttribute("aria-busy", String(busy));
        });
        if (recipeId !== undefined) updateSavedRecipeUI(recipeId);
    }

    function updateCount(count) {
        document.querySelectorAll("[data-saved-count], #savedRecipeCount, #savedRecipeCountInline")
            .forEach(element => { element.textContent = String(count); });
    }

    function initializeSavedPage() {
        collections.forEach(([gridId, emptyId]) => {
            const grid = document.getElementById(gridId);
            const empty = document.getElementById(emptyId);
            if (!grid || !empty || sessionInvalid) return;
            const hasRecipes = Boolean(grid.querySelector("[data-saved-recipe-id]"));
            grid.hidden = !hasRecipes;
            grid.style.display = hasRecipes ? "grid" : "none";
            empty.hidden = hasRecipes;
            empty.style.display = hasRecipes ? "none" : "flex";
        });
    }

    function removeSavedCards(recipeId) {
        collections.forEach(([gridId, emptyId]) => {
            const grid = document.getElementById(gridId);
            if (!grid) return;
            grid.querySelectorAll("[data-saved-recipe-id]").forEach(card => {
                if (Number(card.dataset.savedRecipeId) !== Number(recipeId)) return;
                const hadFocus = card.contains(document.activeElement);
                const next = card.nextElementSibling || card.previousElementSibling;
                card.remove();
                initializeSavedPage();
                if (hadFocus) (next?.querySelector(selector) || document.querySelector(`#${emptyId} a`))?.focus();
            });
        });
    }

    function collectionNotice(grid, message, needsLogin = false) {
        let notice = grid.parentElement.querySelector(`[data-save-collection-status="${grid.id}"]`);
        if (!notice) {
            notice = document.createElement("p");
            notice.className = "notice db-save-status";
            notice.dataset.saveCollectionStatus = grid.id;
            notice.setAttribute("role", "status");
            notice.setAttribute("aria-live", "polite");
            grid.before(notice);
        }
        notice.textContent = message;
        notice.hidden = false;
        const link = document.createElement("a");
        link.className = "text-link";
        link.textContent = needsLogin ? "Đăng nhập" : "Tải lại trang";
        link.href = needsLogin ? grid.dataset.loginUrl : window.location.href;
        notice.append(document.createTextNode(" "), link);
    }

    function checkCollectionCounts(count) {
        collections.forEach(([gridId]) => {
            const grid = document.getElementById(gridId);
            if (!grid) return;
            const notice = grid.parentElement.querySelector(`[data-save-collection-status="${gridId}"]`);
            if (grid.querySelectorAll("[data-saved-recipe-id]").length !== count) {
                collectionNotice(grid, "Bộ sưu tập đã thay đổi. Tải lại trang để xem đầy đủ công thức đã lưu.");
            } else if (notice) notice.hidden = true;
        });
    }

    function hidePrivateCollections(message = null, needsLogin = false) {
        collections.forEach(([gridId, emptyId]) => {
            const grid = document.getElementById(gridId);
            if (!grid) return;
            grid.hidden = true;
            grid.style.display = "none";
            const empty = document.getElementById(emptyId);
            if (empty) { empty.hidden = true; empty.style.display = "none"; }
            if (message) collectionNotice(grid, message, needsLogin);
        });
    }

    function invalidateSession(needsLogin) {
        sessionInvalid = true;
        const message = needsLogin ? "Vui lòng đăng nhập để xem và lưu công thức."
            : "Tài khoản đã thay đổi. Vui lòng tải lại trang để xem bộ sưu tập của bạn.";
        states.forEach((_, id) => states.set(id, false));
        syncSavedRecipeUI();
        updateCount(0);
        lock(undefined, false);
        hidePrivateCollections(message, needsLogin);
        notify(message, true);
    }

    function failure(message, authenticationRequired = false) {
        const error = new Error(message);
        error.authenticationRequired = authenticationRequired;
        return error;
    }

    async function request(url, options) {
        let response;
        try { response = await fetch(url, { credentials: "same-origin", cache: "no-store", ...options }); }
        catch { throw failure("Không thể kết nối. Vui lòng kiểm tra mạng và thử lại."); }
        if (response.redirected || response.status === 401) {
            throw failure("Vui lòng đăng nhập để lưu công thức.", true);
        }
        let data;
        if (response.headers.get("Content-Type")?.includes("application/json")) {
            try { data = await response.json(); } catch { /* Invalid response handled below. */ }
        }
        if (!response.ok) {
            const message = response.status === 400 ? "Phiên làm việc không hợp lệ. Vui lòng tải lại trang rồi thử lại."
                : response.status === 403 ? "Bạn không có quyền thực hiện thao tác này."
                    : response.status === 404 ? "Công thức không còn tồn tại."
                        : "Không thể lưu thay đổi. Vui lòng thử lại sau.";
            throw failure(typeof data?.message === "string" ? data.message : message);
        }
        if (!data || typeof data !== "object") throw failure("Phản hồi không hợp lệ. Vui lòng tải lại trang để kiểm tra kết quả.");
        return data;
    }

    async function changeRecipe(recipeId, saved) {
        const id = Number(recipeId);
        const button = buttons(id)[0];
        if (!validId(id) || !button || sessionInvalid || revalidation) return isRecipeSaved(id);
        if (pending.has(id)) return pending.get(id);
        buttons(id).forEach(clearStatus);
        const form = button.closest("[data-db-save-form]");
        const token = form?.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (!form || !token) {
            const message = form ? "Phiên làm việc không hợp lệ. Vui lòng tải lại trang rồi thử lại." : "Vui lòng đăng nhập để lưu công thức.";
            notify(message, true);
            return isRecipeSaved(id);
        }
        generation++;
        const work = (async () => {
            // Defer until pending contains the promise, including synchronous fetch failures.
            await Promise.resolve();
            lock(id, true);
            try {
                const body = new URLSearchParams({ __RequestVerificationToken: token });
                const data = await request(saved ? button.dataset.saveUrl : button.dataset.unsaveUrl, {
                    method: "POST",
                    headers: { "Accept": "application/json", "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8", "X-Requested-With": "XMLHttpRequest" },
                    body: body.toString()
                });
                if (sessionInvalid) return false;
                if (data.recipeId !== id || typeof data.isSaved !== "boolean" || !Number.isSafeInteger(data.savedCount) || data.savedCount < 0) {
                    throw failure("Phản hồi không hợp lệ. Vui lòng tải lại trang để kiểm tra kết quả.");
                }
                states.set(id, data.isSaved);
                updateSavedRecipeUI(id);
                updateCount(data.savedCount);
                const message = typeof data.message === "string" ? data.message : data.isSaved ? "Đã thêm vào góc công thức của bạn." : "Đã bỏ lưu công thức.";
                notify(message);
                if (!data.isSaved) removeSavedCards(id);
                checkCollectionCounts(data.savedCount);
                return data.isSaved;
            } catch (error) {
                if (error.authenticationRequired) invalidateSession(true);
                else notify(error.message, true);
                return isRecipeSaved(id);
            } finally {
                pending.delete(id);
                generation++;
                lock(id, false);
                if (!pending.size && reconcileAfterWrites) {
                    reconcileAfterWrites = false;
                    void refreshSavedState();
                }
            }
        })();
        pending.set(id, work);
        if (pending.size > 1) reconcileAfterWrites = true;
        return work;
    }

    function saveRecipe(recipeId) { return changeRecipe(recipeId, true); }
    function unsaveRecipe(recipeId) { return changeRecipe(recipeId, false); }
    function toggleSavedRecipe(recipeId) { return changeRecipe(recipeId, !isRecipeSaved(recipeId)); }

    async function refreshSavedState(privateRevalidation = false) {
        if (revalidation || sessionInvalid) return revalidation;
        if (pending.size) { reconcileAfterWrites = true; return; }
        const source = buttons().find(button => button.closest("[data-db-save-form]"))
            || document.querySelector("#savedRecipeGrid[data-state-url], #profileFavoriteGrid[data-state-url]");
        if (!source?.dataset.stateUrl) return;
        if (privateRevalidation) hidePrivateCollections();
        const version = generation;
        lock(undefined, true);
        const work = (async () => {
            await Promise.resolve();
            try {
                const url = new URL(source.dataset.stateUrl, window.location.href);
                const ids = [...new Set(buttons().map(button => Number(button.dataset.recipeId)).filter(validId))];
                ids.forEach(id => url.searchParams.append("recipeIds", String(id)));
                const data = await request(url.href, { headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" } });
                if (version !== generation) return;
                if (!Array.isArray(data.savedRecipeIds) || !data.savedRecipeIds.every(id => Number.isSafeInteger(id) && id > 0)
                    || !Number.isSafeInteger(data.savedCount) || data.savedCount < 0 || typeof data.userId !== "string") {
                    throw failure("Phản hồi không hợp lệ. Vui lòng tải lại trang để kiểm tra kết quả.");
                }
                if (source.dataset.savedUserId && data.userId !== source.dataset.savedUserId) {
                    invalidateSession(false);
                    return;
                }
                const saved = new Set(data.savedRecipeIds.map(Number));
                ids.forEach(id => {
                    states.set(id, saved.has(id));
                    updateSavedRecipeUI(id);
                    if (!saved.has(id)) removeSavedCards(id);
                });
                updateCount(data.savedCount);
                initializeSavedPage();
                checkCollectionCounts(data.savedCount);
            } catch (error) {
                if (error.authenticationRequired) invalidateSession(true);
                else {
                    notify(error.message, true);
                    if (privateRevalidation) hidePrivateCollections("Không thể xác nhận bộ sưu tập. Vui lòng tải lại trang.");
                }
            } finally {
                revalidation = null;
                lock(undefined, false);
            }
        })();
        revalidation = work;
        return work;
    }

    document.addEventListener("submit", event => {
        const form = event.target.closest("[data-db-save-form]");
        if (!form) return;
        const button = form.querySelector(selector);
        if (!button) return;
        event.preventDefault();
        void toggleSavedRecipe(button.dataset.recipeId);
    });

    window.addEventListener("pageshow", event => {
        if (event.persisted) void refreshSavedState(true);
    });
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible") void refreshSavedState();
    });

    function initialize() { syncSavedRecipeUI(); initializeSavedPage(); }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize, { once: true });
    else initialize();

    window.CookShareSaved = {
        isRecipeSaved, getSavedRecipeIds, saveRecipe, unsaveRecipe, toggleSavedRecipe,
        updateSavedRecipeUI, syncSavedRecipeUI, initializeSavedPage, refreshSavedState
    };
})();
