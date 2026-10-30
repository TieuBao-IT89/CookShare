(function () {
    "use strict";
    const root = document.querySelector("[data-home-feed]");
    if (!root || root.dataset.initialized === "true") return;
    root.dataset.initialized = "true";
    const items = root.querySelector("[data-feed-items]");
    const more = root.querySelector("[data-feed-more]");
    const status = root.querySelector("[data-feed-status]");
    const loading = root.querySelector("[data-feed-loading]");
    const end = root.querySelector("[data-feed-end]");
    const automatic = root.querySelector("[data-feed-auto]");
    const top = root.querySelector("[data-feed-top]");
    const ids = new Set(Array.from(items.querySelectorAll("[data-feed-post]")).map(post => post.dataset.recipeId));
    const pendingComments = new WeakSet();
    let busy = false;
    let autoLoads = 0;
    let autoPaused = false;

    function message(text, error = false, login = false) {
        status.textContent = text;
        status.classList.toggle("is-error", error);
        if (login) {
            const link = document.createElement("a");
            link.href = root.dataset.loginUrl;
            link.textContent = "Đăng nhập";
            status.append(link);
        }
    }

    function safeUrl(value, path) {
        if (!value) return null;
        const url = new URL(value, window.location.origin);
        if (url.origin !== window.location.origin || url.pathname !== path) throw new Error("Phản hồi tải thêm không hợp lệ.");
        if (path.endsWith("/Feed/More") && (url.searchParams.get("feed") || "latest") !== root.dataset.mode) {
            throw new Error("Bảng tin đã thay đổi. Vui lòng tải lại trang.");
        }
        return url;
    }

    function prepare(scope) {
        scope.querySelectorAll("[data-feed-share]").forEach(button => { button.hidden = false; });
        const formatter = new Intl.RelativeTimeFormat("vi", { numeric: "auto" });
        scope.querySelectorAll("[data-feed-time]").forEach(time => {
            const date = new Date(time.dateTime);
            if (Number.isNaN(date.getTime())) return;
            time.title = date.toLocaleString("vi-VN");
            const minutes = Math.floor((Date.now() - date.getTime()) / 60000);
            time.textContent = minutes < 1 ? "Vừa xong" : minutes < 60 ? formatter.format(-minutes, "minute")
                : minutes < 1440 ? formatter.format(-Math.floor(minutes / 60), "hour")
                    : minutes < 10080 ? formatter.format(-Math.floor(minutes / 1440), "day") : date.toLocaleDateString("vi-VN");
        });
    }

    async function htmlResponse(url) {
        const response = await fetch(url.href, {
            credentials: "same-origin", cache: "no-store", redirect: "follow",
            headers: { "Accept": "text/html", "X-Requested-With": "XMLHttpRequest" }
        });
        if (response.redirected || response.status === 401) {
            const error = new Error("Phiên đăng nhập đã thay đổi. Vui lòng đăng nhập lại.");
            error.needsLogin = true;
            throw error;
        }
        if (!response.ok) throw new Error("Chưa tải được nội dung. Bạn có thể thử lại mà không mất vị trí đang xem.");
        if (!response.headers.get("Content-Type")?.includes("text/html")) throw new Error("Phản hồi không hợp lệ. Vui lòng thử lại.");
        const template = document.createElement("template");
        template.innerHTML = await response.text();
        if (template.content.querySelector("script, iframe, object")) throw new Error("Phản hồi không hợp lệ.");
        return template.content;
    }

    function checkViewer(element) {
        if ((element.dataset.viewerId || "") !== (root.dataset.viewerId || "")) {
            // The account may have changed in another tab. Never append another account's state.
            items.hidden = true;
            const error = new Error("Tài khoản đã thay đổi. Vui lòng tải lại trang để xem đúng bảng tin của bạn.");
            error.sessionChanged = true;
            throw error;
        }
    }

    async function loadMore(manual = false) {
        if (busy || !more || more.hidden || !more.dataset.endpoint || items.hidden) return;
        busy = true;
        items.setAttribute("aria-busy", "true");
        more.setAttribute("aria-disabled", "true");
        loading.hidden = false;
        message("Đang mang thêm món ngon đến bạn…");
        try {
            const response = await htmlResponse(safeUrl(more.dataset.endpoint, root.dataset.morePath || "/Feed/More"));
            const batch = response.firstElementChild;
            if (!batch?.matches("[data-feed-batch]") || batch.dataset.mode !== root.dataset.mode) throw new Error("Phản hồi bảng tin không hợp lệ.");
            checkViewer(batch);
            const next = safeUrl(batch.dataset.nextUrl, root.dataset.morePath || "/Feed/More");
            const nextPage = safeUrl(batch.dataset.nextPage, root.dataset.homePath || "/");
            if ((next && !nextPage) || (!next && nextPage) || next?.href === new URL(more.dataset.endpoint, location.href).href) throw new Error("Phân trang không hợp lệ.");
            const posts = Array.from(batch.querySelectorAll(":scope > [data-feed-post]"));
            if (posts.length > 8 || posts.some(post => !/^[1-9]\d*$/.test(post.dataset.recipeId || ""))) throw new Error("Dữ liệu công thức không hợp lệ.");
            const incomingIds = new Set();
            posts.forEach(post => {
                if (ids.has(post.dataset.recipeId) || incomingIds.has(post.dataset.recipeId)) post.remove();
                else incomingIds.add(post.dataset.recipeId);
            });
            const first = batch.querySelector("[data-feed-post]");
            prepare(batch);
            items.append(batch);
            incomingIds.forEach(id => ids.add(id));
            window.CookShareSaved?.syncSavedRecipeUI();
            if (next) {
                more.dataset.endpoint = next.href;
                more.href = nextPage.href;
            } else {
                more.hidden = true;
                end.hidden = false;
            }
            if (!manual) autoLoads++;
            autoPaused = false;
            if (!incomingIds.size && next) autoPaused = true;
            message(incomingIds.size ? `Đã tải thêm ${incomingIds.size} công thức.` : "Bạn đã xem hết những món trong lượt tải này.");
            if (autoLoads >= 3 && next) message("Còn nhiều món ngon đang chờ. Chọn Xem thêm để tiếp tục.");
            if (manual && first) first.focus({ preventScroll: true });
        } catch (error) {
            autoPaused = true;
            if (error.needsLogin && root.dataset.mode === "following") items.hidden = true;
            message(error.message || "Kết nối bị gián đoạn. Chọn Xem thêm để thử lại.", true, error.needsLogin);
            if (error.sessionChanged) {
                const reload = document.createElement("a");
                reload.href = window.location.href;
                reload.textContent = "Tải lại trang";
                status.append(reload);
            }
        } finally {
            busy = false;
            loading.hidden = true;
            items.setAttribute("aria-busy", "false");
            more.removeAttribute("aria-disabled");
        }
    }

    more?.addEventListener("click", event => {
        if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        void loadMore(true);
    });
    if ("IntersectionObserver" in window && more) {
        root.querySelector("[data-feed-auto-control]").hidden = false;
        const observer = new IntersectionObserver(entries => {
            if (entries.some(entry => entry.isIntersecting) && automatic.checked && autoLoads < 3 && !autoPaused) void loadMore();
        }, { rootMargin: "350px" });
        observer.observe(root.querySelector("[data-feed-sentinel]"));
        automatic.addEventListener("change", () => {
            autoLoads = 0;
            autoPaused = false;
            if (automatic.checked && root.querySelector("[data-feed-sentinel]").getBoundingClientRect().top < window.innerHeight + 350) void loadMore();
        });
    }

    async function toggleComments(link) {
        const post = link.closest("[data-feed-post]");
        const slot = post.querySelector("[data-feed-comments-slot]");
        if (pendingComments.has(slot)) return;
        if (slot.dataset.loaded === "true") {
            slot.hidden = !slot.hidden;
            link.setAttribute("aria-expanded", String(!slot.hidden));
            return;
        }
        pendingComments.add(slot);
        link.setAttribute("aria-busy", "true");
        slot.hidden = false;
        slot.textContent = "Đang mở câu chuyện…";
        link.setAttribute("aria-expanded", "true");
        try {
            const url = new URL(link.dataset.commentsUrl, window.location.origin);
            if (url.origin !== window.location.origin) throw new Error("Liên kết bình luận không hợp lệ.");
            const content = await htmlResponse(url);
            const section = content.firstElementChild;
            if (!section?.matches("[data-db-recipe-comments]") || section.dataset.recipeId !== post.dataset.recipeId) throw new Error("Phản hồi bình luận không hợp lệ.");
            checkViewer(section);
            slot.replaceChildren(section);
            slot.dataset.loaded = "true";
            section.querySelector("[data-comment-input]")?.focus({ preventScroll: true });
        } catch (error) {
            if (error.sessionChanged) message(error.message + " Tải lại trang để tiếp tục.", true);
            slot.replaceChildren(document.createTextNode("Chưa mở được bình luận. "));
            const fallback = document.createElement("a");
            fallback.href = error.needsLogin ? root.dataset.loginUrl : link.href;
            fallback.textContent = error.needsLogin ? "Đăng nhập để tiếp tục" : "Mở trang công thức";
            slot.append(fallback);
        } finally {
            link.removeAttribute("aria-busy");
            pendingComments.delete(slot);
        }
    }

    async function share(button) {
        const url = new URL(button.dataset.shareUrl, window.location.origin);
        if (url.origin !== window.location.origin) return;
        try {
            if (navigator.share) {
                await navigator.share({ title: button.dataset.shareTitle, url: url.href });
                return;
            }
            if (navigator.clipboard?.writeText) {
                await navigator.clipboard.writeText(url.href);
                window.CookShareUI?.toast("Đã sao chép liên kết món ngon.");
                return;
            }
        } catch (error) { if (error.name === "AbortError") return; }
        const dialog = root.querySelector("[data-feed-share-dialog]");
        const input = dialog.querySelector("input");
        input.value = url.href;
        if (typeof dialog.showModal === "function") {
            dialog.showModal();
            input.focus();
            input.select();
        } else window.prompt("Sao chép liên kết công thức:", url.href);
    }

    root.addEventListener("click", event => {
        const readMore = event.target.closest("[data-feed-read-more]");
        if (readMore && root.contains(readMore)) {
            const rest = document.getElementById(readMore.getAttribute("aria-controls"));
            if (rest && readMore.closest("[data-feed-post]")?.contains(rest)) {
                rest.hidden = false;
                readMore.setAttribute("aria-expanded", "true");
                readMore.hidden = true;
            }
        }
        const comments = event.target.closest("[data-feed-comment-toggle]");
        if (comments && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) {
            event.preventDefault();
            void toggleComments(comments);
        }
        const sharing = event.target.closest("[data-feed-share]");
        if (sharing) void share(sharing);
    });
    document.addEventListener("cookshare:comments-updated", event => {
        const { recipeId, count } = event.detail || {};
        if (!Number.isInteger(recipeId) || !Number.isInteger(count) || count < 0) return;
        root.querySelectorAll("[data-feed-comment-count]").forEach(node => {
            if (Number(node.dataset.recipeId) === recipeId) node.textContent = String(count);
        });
    });
    top?.addEventListener("click", () => window.scrollTo({ top: 0, behavior: matchMedia("(prefers-reduced-motion: reduce)").matches ? "instant" : "smooth" }));
    window.addEventListener("scroll", () => { if (top) top.hidden = window.scrollY < 850; }, { passive: true });
    window.addEventListener("pageshow", event => {
        if (event.persisted) {
            items.hidden = true;
            window.location.reload();
        }
    });
    prepare(root);
})();
