// Database recipe interactions. The server owns identities, counts and content.
(function () {
    "use strict";
    if (window.CookShareRecipeInteractions) return;
    window.CookShareRecipeInteractions = { initialized: true };

    const commentLimit = 500;
    const pendingLikes = new Set();
    const pendingComments = new WeakSet();

    function element(tag, className, content) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (content !== undefined) node.textContent = content;
        return node;
    }

    function showStatus(scope, selector, message, isError = false, needsLogin = false) {
        const status = scope.querySelector(selector);
        if (!status) return;
        status.textContent = message;
        status.hidden = false;
        status.classList.toggle("error", isError);
        if (needsLogin) {
            const login = element("a", "text-link", "Đăng nhập");
            login.href = scope.dataset.loginUrl;
            status.append(document.createTextNode(" "), login);
        }
    }

    function notify(message, isError = false) {
        window.CookShareUI?.toast(message, isError ? "error" : "success");
    }

    function responseError(message, authenticationRequired = false) {
        const error = new Error(message);
        error.authenticationRequired = authenticationRequired;
        return error;
    }

    async function post(scope, url, values = {}) {
        const token = scope.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (!token) throw responseError("Phiên làm việc không hợp lệ. Vui lòng tải lại trang rồi thử lại.");
        const body = new URLSearchParams(values);
        body.set("__RequestVerificationToken", token);
        let response;
        try {
            response = await fetch(url, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: body.toString()
            });
        } catch {
            throw responseError("Không thể kết nối. Vui lòng kiểm tra mạng và thử lại.");
        }
        if (response.redirected || response.status === 401) {
            throw responseError("Vui lòng đăng nhập để tương tác với công thức.", true);
        }
        let data;
        if (response.headers.get("Content-Type")?.includes("application/json")) {
            try { data = await response.json(); } catch { /* Handled as an invalid response below. */ }
        }
        if (!response.ok) {
            const fallback = response.status === 403 ? "Bạn không có quyền thực hiện thao tác này."
                : response.status === 404 ? "Công thức hoặc bình luận không còn tồn tại."
                    : response.status === 400 ? "Yêu cầu không hợp lệ. Vui lòng tải lại trang rồi thử lại."
                        : "Không thể lưu thay đổi. Vui lòng thử lại sau.";
            throw responseError(typeof data?.message === "string" ? data.message : fallback);
        }
        if (!data || typeof data !== "object") {
            throw responseError("Phản hồi không hợp lệ. Vui lòng tải lại trang để kiểm tra kết quả.");
        }
        return data;
    }

    function likeControls(recipeId) {
        return Array.from(document.querySelectorAll("[data-db-recipe-like]"))
            .filter(scope => scope.dataset.recipeId === recipeId);
    }

    function clearLikeStatus(scope) {
        const status = scope.querySelector("[data-like-status]");
        if (!status) return;
        status.textContent = "";
        status.hidden = true;
        status.classList.remove("error");
    }

    function lockLikes(recipeId, busy) {
        likeControls(recipeId).forEach(scope => {
            clearLikeStatus(scope);
            const button = scope.querySelector("button.recipe-like-btn");
            if (!button) return;
            button.disabled = busy;
            button.setAttribute("aria-busy", String(busy));
            const label = button.querySelector(".like-text");
            if (label) label.textContent = busy ? "Đang lưu…" : scope.dataset.isLiked === "true" ? "Đã thích" : "Thích";
        });
    }

    function updateLikes(recipeId, summary) {
        likeControls(recipeId).forEach(scope => {
            clearLikeStatus(scope);
            scope.dataset.isLiked = String(summary.isLiked);
            const button = scope.querySelector("button.recipe-like-btn");
            if (button) {
                button.classList.toggle("liked", summary.isLiked);
                button.setAttribute("aria-pressed", String(summary.isLiked));
                button.setAttribute("aria-label", summary.isLiked ? "Bỏ thích công thức" : "Thích công thức");
                button.querySelector(".like-icon").textContent = summary.isLiked ? "♥" : "♡";
                scope.querySelector("form").action = summary.isLiked ? scope.dataset.unlikeUrl : scope.dataset.likeUrl;
            }
            scope.querySelectorAll("[data-db-like-count]").forEach(count => {
                count.textContent = summary.likeCount.toLocaleString("vi-VN");
                count.classList.toggle("liked", summary.isLiked);
            });
        });
    }

    async function submitLike(form) {
        const scope = form.closest("[data-db-recipe-like]");
        const recipeId = scope.dataset.recipeId;
        if (pendingLikes.has(recipeId)) return;
        pendingLikes.add(recipeId);
        lockLikes(recipeId, true);
        const liked = scope.dataset.isLiked === "true";
        try {
            const summary = await post(scope, liked ? scope.dataset.unlikeUrl : scope.dataset.likeUrl);
            if (summary.recipeId !== Number(recipeId) || !Number.isInteger(summary.likeCount)
                || summary.likeCount < 0 || typeof summary.isLiked !== "boolean") {
                throw responseError("Phản hồi không hợp lệ. Vui lòng tải lại trang để kiểm tra kết quả.");
            }
            updateLikes(recipeId, summary);
            const message = summary.message || (summary.isLiked ? "Đã thích công thức." : "Đã bỏ thích công thức.");
            notify(message);
        } catch (error) {
            notify(error.message, true);
        } finally {
            pendingLikes.delete(recipeId);
            lockLikes(recipeId, false);
        }
    }

    function updateCounter(form) {
        const input = form.querySelector("[data-comment-input]");
        const counter = form.querySelector("[data-comment-counter]");
        if (!input || !counter) return;
        const length = input.value.length;
        counter.textContent = `${length} / ${commentLimit}`;
        counter.classList.toggle("comment-char-counter--warning", length >= 420 && length < commentLimit);
        counter.classList.toggle("comment-char-counter--danger", length >= commentLimit);
    }

    function validationMessage(form, message) {
        const validation = form.querySelector("[data-comment-validation]");
        if (validation) {
            validation.textContent = message;
            validation.classList.toggle("comment-validation-msg--visible", Boolean(message));
        }
        form.querySelector("[data-comment-input]")?.setAttribute("aria-invalid", String(Boolean(message)));
    }

    function formatDate(time) {
        const date = new Date(time.dateTime);
        if (!Number.isNaN(date.getTime())) {
            time.textContent = date.toLocaleString("vi-VN", {
                day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit"
            });
        }
    }

    function avatarSource(value) {
        if (typeof value !== "string" || !value.trim()) return null;
        const source = value.trim().replace(/^~\//, "/");
        if (/[\u0000-\u001f\u007f\\]/.test(source) || source.startsWith("//")) return null;
        try {
            if (source.startsWith("/")) {
                const path = decodeURIComponent(source.split(/[?#]/, 1)[0]);
                if (path.startsWith("//") || /[\u0000-\u001f\u007f\\]/.test(path) || path.split("/").some(part => part === "." || part === "..")) return null;
            }
            const url = new URL(source, window.location.origin);
            return ["https:", "http:"].includes(url.protocol) && !url.username && !url.password ? url.href : null;
        } catch { return null; }
    }

    function commentButton(className, label, action) {
        const button = element("button", className, label);
        button.type = "button";
        button.setAttribute(action, "");
        return button;
    }

    function commentItem(section, comment) {
        const item = element("article", `comment-item${comment.canManage ? " comment-item--own" : ""}`);
        item.id = `comment-${comment.id}`;
        item.setAttribute("data-db-comment", "");
        item.dataset.commentId = String(comment.id);
        const avatar = element("div", "comment-avatar comment-avatar--small");
        avatar.setAttribute("aria-hidden", "true");
        const source = avatarSource(comment.avatarUrl);
        if (source) {
            const image = element("img");
            image.src = source;
            image.alt = "";
            image.width = 36;
            image.height = 36;
            image.loading = "lazy";
            avatar.append(image);
        } else {
            avatar.textContent = comment.authorName.trim().split(/\s+/).filter(Boolean).slice(-2)
                .map(part => Array.from(part)[0].toLocaleUpperCase("vi-VN")).join("") || "?";
        }
        const body = element("div", "comment-body");
        const header = element("div", "comment-header-row");
        const author = element("a", "comment-author-name", comment.authorName);
        author.href = `${section.dataset.profileUrl}?userId=${encodeURIComponent(comment.userId)}`;
        const time = element("time", "comment-timestamp");
        time.dateTime = comment.createdAt;
        formatDate(time);
        header.append(author, time);
        body.append(header, element("p", "comment-content-text", comment.content));
        if (comment.canManage) {
            const actions = element("div", "comment-actions-row");
            const edit = commentButton("btn-comment-action btn-comment-edit", "✎ Sửa", "data-comment-edit");
            edit.setAttribute("aria-label", `Sửa bình luận của ${comment.authorName}`);
            const remove = commentButton("btn-comment-action btn-comment-delete", "🗑 Xóa", "data-comment-delete");
            remove.setAttribute("aria-label", `Xóa bình luận của ${comment.authorName}`);
            actions.append(edit, remove);
            body.append(actions);
        }
        item.append(avatar, body);
        return item;
    }

    function validComment(comment, recipeId) {
        return comment && Number.isInteger(comment.id) && comment.id > 0 && comment.recipeId === Number(recipeId)
            && typeof comment.userId === "string" && typeof comment.authorName === "string"
            && typeof comment.content === "string" && typeof comment.createdAt === "string" && typeof comment.canManage === "boolean";
    }

    function updateCommentCount(section, count) {
        document.querySelectorAll("[data-db-comment-count]").forEach(counter => {
            if (counter.closest("[data-recipe-id]")?.dataset.recipeId === section.dataset.recipeId) {
                counter.textContent = count.toLocaleString("vi-VN");
            }
        });
        section.querySelector("[data-comment-empty]").hidden = count > 0;
    }

    function lockComments(section) {
        pendingComments.add(section);
        section.setAttribute("aria-busy", "true");
        const controls = Array.from(section.querySelectorAll("button, textarea")).map(control => [control, control.disabled]);
        controls.forEach(([control]) => { control.disabled = true; });
        return () => {
            controls.forEach(([control, disabled]) => { control.disabled = disabled; });
            section.removeAttribute("aria-busy");
            pendingComments.delete(section);
        };
    }

    function commentFailure(section, error, form = null) {
        if (form) validationMessage(form, error.message);
        showStatus(section, "[data-comment-status]", error.message, true, error.authenticationRequired);
        const loginPrompt = section.querySelector("[data-comment-login]");
        if (error.authenticationRequired && loginPrompt) loginPrompt.hidden = false;
        notify(error.message, true);
    }

    async function submitComment(form) {
        const section = form.closest("[data-db-recipe-comments]");
        if (pendingComments.has(section)) return;
        const input = form.querySelector("[data-comment-input]");
        const content = input.value.trim();
        if (!content || content.length > commentLimit) {
            validationMessage(form, !content ? "Vui lòng nhập nội dung bình luận." : "Bình luận tối đa 500 ký tự.");
            input.focus();
            return;
        }
        validationMessage(form, "");
        const unlock = lockComments(section);
        const button = form.querySelector('button[type="submit"]');
        const label = button.textContent;
        button.textContent = "Đang lưu…";
        const editing = form.hasAttribute("data-comment-edit-form");
        let focusTarget = input;
        try {
            const data = await post(section, form.action, { Content: content });
            const original = editing ? form.closest("[data-db-comment]") : null;
            if (!validComment(data.comment, section.dataset.recipeId) || !Number.isInteger(data.count) || data.count < 0
                || (original && data.comment.id !== Number(original.dataset.commentId))) {
                throw responseError("Phản hồi không hợp lệ. Vui lòng tải lại trang để kiểm tra kết quả.");
            }
            const savedItem = commentItem(section, data.comment);
            if (original) {
                original.replaceWith(savedItem);
                focusTarget = savedItem.querySelector("[data-comment-edit]");
            }
            else {
                section.querySelector("[data-comment-list]").prepend(savedItem);
                input.value = "";
                updateCounter(form);
            }
            updateCommentCount(section, data.count);
            const message = data.message || (editing ? "Đã cập nhật bình luận." : "Đã đăng bình luận.");
            showStatus(section, "[data-comment-status]", message);
            notify(message);
        } catch (error) {
            commentFailure(section, error, form);
        } finally {
            button.textContent = label;
            unlock();
            focusTarget?.focus();
        }
    }

    function startEdit(button) {
        const section = button.closest("[data-db-recipe-comments]");
        if (pendingComments.has(section)) return;
        const item = button.closest("[data-db-comment]");
        if (item.querySelector("[data-comment-edit-form]")) return;
        const form = element("form", "comment-edit-form");
        form.setAttribute("data-comment-edit-form", "");
        form.method = "post";
        form.action = `${section.dataset.commentsUrl}/${item.dataset.commentId}/Edit`;
        const input = element("textarea", "comment-edit-textarea");
        input.id = `editRecipeComment_${item.dataset.commentId}`;
        input.name = "Content";
        input.maxLength = commentLimit;
        input.required = true;
        input.rows = 3;
        input.value = item.querySelector(".comment-content-text").textContent;
        input.setAttribute("data-comment-input", "");
        const label = element("label", "comment-sr-live", "Chỉnh sửa nội dung bình luận");
        label.htmlFor = input.id;
        const counter = element("div", "comment-edit-char-counter");
        counter.id = `${input.id}_counter`;
        counter.setAttribute("data-comment-counter", "");
        const validation = element("p", "comment-validation-msg");
        validation.id = `${input.id}_validation`;
        validation.setAttribute("data-comment-validation", "");
        validation.setAttribute("role", "alert");
        input.setAttribute("aria-describedby", `${counter.id} ${validation.id}`);
        const actions = element("div", "comment-edit-actions");
        const cancel = commentButton("btn-edit-cancel", "Hủy", "data-comment-cancel");
        const save = element("button", "btn-edit-save", "Lưu bình luận");
        save.type = "submit";
        actions.append(cancel, save);
        form.append(label, input, counter, validation, actions);
        item.querySelector(".comment-content-text").hidden = true;
        item.querySelector(".comment-actions-row").hidden = true;
        item.querySelector(".comment-body").append(form);
        updateCounter(form);
        input.focus();
    }

    function cancelEdit(button) {
        const section = button.closest("[data-db-recipe-comments]");
        if (pendingComments.has(section)) return;
        const item = button.closest("[data-db-comment]");
        button.closest("[data-comment-edit-form]").remove();
        item.querySelector(".comment-content-text").hidden = false;
        item.querySelector(".comment-actions-row").hidden = false;
        item.querySelector("[data-comment-edit]").focus();
    }

    async function deleteComment(button) {
        const section = button.closest("[data-db-recipe-comments]");
        if (pendingComments.has(section) || !window.confirm("Bạn có chắc muốn xóa bình luận này không?")) return;
        const item = button.closest("[data-db-comment]");
        const unlock = lockComments(section);
        const label = button.textContent;
        button.textContent = "Đang xóa…";
        let focusTarget = button;
        try {
            const data = await post(section, `${section.dataset.commentsUrl}/${item.dataset.commentId}/Delete`);
            if (data.commentId !== Number(item.dataset.commentId) || !Number.isInteger(data.count) || data.count < 0) {
                throw responseError("Phản hồi không hợp lệ. Vui lòng tải lại trang để kiểm tra kết quả.");
            }
            item.remove();
            focusTarget = section.querySelector("[data-comment-create] [data-comment-input]");
            updateCommentCount(section, data.count);
            const message = data.message || "Đã xóa bình luận.";
            showStatus(section, "[data-comment-status]", message);
            notify(message);
        } catch (error) {
            commentFailure(section, error);
        } finally {
            button.textContent = label;
            unlock();
            focusTarget?.focus();
        }
    }

    document.addEventListener("submit", event => {
        const form = event.target;
        if (form.matches("[data-db-like-form]")) {
            event.preventDefault();
            submitLike(form);
        } else if (form.matches("[data-comment-create], [data-comment-edit-form]")) {
            event.preventDefault();
            submitComment(form);
        }
    });

    document.addEventListener("input", event => {
        if (!event.target.matches("[data-comment-input]")) return;
        const form = event.target.closest("form");
        updateCounter(form);
        validationMessage(form, "");
    });

    document.addEventListener("click", event => {
        const button = event.target.closest("[data-comment-edit], [data-comment-cancel], [data-comment-delete]");
        if (!button || !button.closest("[data-db-recipe-comments]")) return;
        event.preventDefault();
        if (button.hasAttribute("data-comment-edit")) startEdit(button);
        else if (button.hasAttribute("data-comment-cancel")) cancelEdit(button);
        else deleteComment(button);
    });

    document.querySelectorAll("[data-db-recipe-comments] .comment-timestamp").forEach(formatDate);
})();
