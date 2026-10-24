/* Progressive enhancements for server-rendered creator profiles. */
(function () {
    "use strict";
    if (window.CookShareCreator) return;
    window.CookShareCreator = {};
    const avatarFallback = "/img/recipes/profile-avatar.webp";

    function imageSource(value) {
        let source = value.trim();
        if (!source || source.length > 500 || /[\\\u0000-\u001f\u007f]/.test(source)) return null;
        if (source.startsWith("~/")) source = source.slice(1);
        if (source.startsWith("/")) {
            try {
                const path = decodeURIComponent(source.split(/[?#]/, 1)[0]);
                if (path.startsWith("//") || /[\\\u0000-\u001f\u007f]/.test(path) || path.split("/").some(segment => segment === "." || segment === "..")) return null;
            } catch { return null; }
            return source;
        }
        try {
            const url = new URL(source);
            return url.protocol === "https:" && url.hostname && !url.username && !url.password ? source : null;
        } catch { return null; }
    }

    function fallback(image) {
        if (!image.matches("[data-profile-image]")) return;
        const source = image.dataset.imageFallback;
        if (source && !image.dataset.fallbackApplied) {
            image.dataset.fallbackApplied = "true";
            image.src = source;
        } else image.hidden = true;
    }

    function tabs() {
        const nav = document.querySelector("[data-profile-tabs]");
        if (!nav) return;
        const links = [...nav.querySelectorAll('a[href^="#"]')];
        const panels = [...document.querySelectorAll("[data-profile-panel]")];
        const ids = links.map(link => link.hash.slice(1));
        nav.setAttribute("role", "tablist");
        links.forEach(link => { link.setAttribute("role", "tab"); link.setAttribute("aria-controls", link.hash.slice(1)); });
        panels.forEach(panel => { panel.setAttribute("role", "tabpanel"); panel.setAttribute("aria-labelledby", "creatorTab_" + panel.id); panel.tabIndex = 0; });
        function activate(id) {
            if (!ids.includes(id)) id = ids[0];
            links.forEach(link => {
                const active = link.hash === "#" + id;
                link.classList.toggle("is-active", active); link.setAttribute("aria-selected", String(active)); link.tabIndex = active ? 0 : -1;
            });
            panels.forEach(panel => { panel.hidden = panel.id !== id; });
        }
        document.querySelector("[data-creator-profile]").addEventListener("click", event => {
            const link = event.target.closest('a[href^="#"]');
            if (!link || !ids.includes(link.hash.slice(1))) return;
            event.preventDefault(); activate(link.hash.slice(1)); history.replaceState(null, "", link.hash);
            if (!nav.contains(link)) nav.scrollIntoView({ block: "start" });
        });
        nav.addEventListener("keydown", event => {
            const current = links.indexOf(document.activeElement);
            if (current < 0) return;
            let next = current;
            if (event.key === "ArrowRight") next = (current + 1) % links.length;
            else if (event.key === "ArrowLeft") next = (current + links.length - 1) % links.length;
            else if (event.key === "Home") next = 0;
            else if (event.key === "End") next = links.length - 1;
            else if (event.key === " ") { event.preventDefault(); links[current].click(); return; }
            else return;
            event.preventDefault(); links[next].focus(); links[next].click();
        });
        addEventListener("hashchange", () => activate(location.hash.slice(1)));
        activate(location.hash.slice(1));
    }

    function editor() {
        const scope = document.querySelector("[data-profile-editor]");
        if (!scope) return;
        const bio = scope.querySelector("[data-profile-bio-input]");
        const name = scope.querySelector("[data-profile-name-input]");
        const updateText = () => {
            const text = bio.value.replace(/\r\n?/g, "\n");
            scope.querySelector("[data-bio-counter]").textContent = text.length + "/300";
            scope.querySelector("[data-profile-bio-preview]").textContent = text.trim() || "Một câu chuyện nhỏ từ căn bếp của bạn…";
            scope.querySelector("[data-profile-name-preview]").textContent = name.value.trim() || "Tên hiển thị của bạn";
        };
        bio.addEventListener("input", updateText); name.addEventListener("input", updateText); updateText();
        for (const kind of ["avatar", "cover"]) {
            const input = scope.querySelector("[data-" + kind + "-input]");
            const preview = scope.querySelector("[data-" + kind + "-preview]");
            const status = scope.querySelector("[data-" + kind + "-status]");
            let generation = 0; let timer;
            function reset() { if (kind === "avatar") { preview.src = avatarFallback; preview.hidden = false; } else { preview.removeAttribute("src"); preview.hidden = true; } }
            function update() {
                const version = ++generation;
                status.classList.remove("is-error");
                if (!input.value.trim()) { reset(); status.textContent = "Đang dùng hình ảnh mặc định."; return; }
                const source = imageSource(input.value);
                if (!source) { reset(); status.textContent = "Dùng đường dẫn nội bộ hoặc URL HTTPS hợp lệ, tối đa 500 ký tự."; status.classList.add("is-error"); return; }
                status.textContent = "Đang tải ảnh xem trước…";
                const loader = new Image();
                loader.onload = () => { if (generation !== version) return; preview.src = source; preview.hidden = false; status.textContent = "Ảnh đã sẵn sàng để xem trước."; };
                loader.onerror = () => { if (generation !== version) return; reset(); status.textContent = "Không tải được ảnh. Kiểm tra đường dẫn hoặc chọn ảnh khác."; status.classList.add("is-error"); };
                loader.src = source;
            }
            input.addEventListener("input", () => { generation++; clearTimeout(timer); timer = setTimeout(update, 180); });
            update();
        }
    }

    function highlights() {
        const manager = document.querySelector("[data-highlight-manager]");
        if (!manager) return;
        const slots = [...manager.querySelectorAll("[data-highlight-slot]")];
        const selects = slots.map(slot => slot.querySelector("[data-highlight-select]"));
        const templates = [...manager.querySelectorAll("template[data-highlight-template]")];
        const status = manager.querySelector("[data-highlight-status]");
        manager.querySelectorAll("[data-highlight-moves]").forEach(control => { control.hidden = false; });
        function update() {
            const selected = selects.map(select => select.value).filter(value => value !== "0");
            manager.querySelector("[data-highlight-count]").textContent = new Set(selected).size + "/3";
            slots.forEach((slot, index) => {
                const select = selects[index];
                slot.classList.toggle("is-selected", select.value !== "0");
                [...select.options].forEach(option => { option.disabled = option.value !== "0" && option.value !== select.value && selected.includes(option.value); });
                const preview = slot.querySelector("[data-highlight-preview]");
                const template = templates.find(item => item.dataset.highlightTemplate === select.value);
                if (template) preview.replaceChildren(template.content.cloneNode(true));
                else {
                    const empty = document.createElement("div"); empty.className = "creator-slot-empty";
                    const icon = document.createElement("i"); icon.className = "fa-solid fa-plus"; icon.setAttribute("aria-hidden", "true");
                    const text = document.createElement("span"); text.textContent = "Chọn một món ngon để xem trước"; empty.append(icon, text); preview.replaceChildren(empty);
                }
            });
        }
        selects.forEach(select => select.addEventListener("change", () => { update(); status.textContent = "Lựa chọn đã thay đổi. Nhấn Lưu để cập nhật hồ sơ."; }));
        manager.addEventListener("click", event => {
            const button = event.target.closest("[data-highlight-move]");
            if (!button) return;
            const from = slots.indexOf(button.closest("[data-highlight-slot]"));
            const to = from + Number(button.dataset.highlightMove);
            if (from < 0 || to < 0 || to >= slots.length) return;
            const previous = selects[from].value; selects[from].value = selects[to].value; selects[to].value = previous;
            update(); status.textContent = "Đã đổi thứ tự. Nhấn Lưu để cập nhật hồ sơ.";
            selects[to].focus();
        });
        update();
    }

    function initialize() {
        document.addEventListener("error", event => { if (event.target instanceof HTMLImageElement) fallback(event.target); }, true);
        document.querySelectorAll("img[data-profile-image]").forEach(image => { if (image.complete && image.naturalWidth === 0) fallback(image); });
        tabs(); editor(); highlights();
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize, { once: true });
    else initialize();
})();
