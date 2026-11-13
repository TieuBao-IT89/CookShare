(function () {
    'use strict';
    if (window.cookShareCollectionsReady) return;
    window.cookShareCollectionsReady = true;

    var dialog;
    var content;
    var status;
    var opener;
    var currentUrl;
    var pending = false;
    var activeRequest;

    function announce(message, failed) {
        status.textContent = message;
        status.setAttribute('role', failed ? 'alert' : 'status');
        status.classList.toggle('is-error', !!failed);
    }

    function ensureDialog() {
        if (dialog) return true;
        if (!window.HTMLDialogElement || !HTMLDialogElement.prototype.showModal) return false;
        dialog = document.createElement('dialog');
        dialog.className = 'collection-dialog';
        dialog.setAttribute('aria-label', 'Chọn bộ sưu tập');
        dialog.setAttribute('aria-labelledby', 'collectionChooserTitle');
        dialog.innerHTML = '<div class="collection-dialog-top"><span>Bộ sưu tập</span><button type="button" class="collection-dialog-close" aria-label="Đóng hộp chọn bộ sưu tập">×</button></div><p class="collection-dialog-status" role="status" aria-live="polite"></p><div class="collection-dialog-body"></div>';
        document.body.appendChild(dialog);
        content = dialog.querySelector('.collection-dialog-body');
        status = dialog.querySelector('.collection-dialog-status');
        dialog.querySelector('.collection-dialog-close').addEventListener('click', function () { dialog.close(); });
        dialog.addEventListener('close', function () {
            if (activeRequest) activeRequest.abort();
            pending = false;
            content.replaceChildren();
            if (opener && opener.isConnected) opener.focus();
        });
        return true;
    }

    function checkSession(response) {
        if (response.status === 401 || (response.redirected && new URL(response.url, location.href).pathname.toLowerCase().indexOf('/account/login') !== -1)) {
            window.location.assign(response.url || '/Account/Login');
            throw new Error('Phiên đăng nhập đã hết. Hãy đăng nhập lại.');
        }
    }

    function render(html, focusKey) {
        var template = document.createElement('template');
        template.innerHTML = html;
        var chooser = template.content.querySelector('[data-collection-chooser-content]');
        if (!chooser) throw new Error('Không tải được bộ sưu tập. Hãy thử lại.');
        content.replaceChildren(chooser);
        currentUrl = chooser.dataset.chooserUrl || currentUrl;
        var focusTarget = focusKey && Array.from(chooser.querySelectorAll('[data-collection-focus]')).find(function (element) {
            return element.dataset.collectionFocus === focusKey;
        });
        if (focusKey === 'create' && chooser.querySelector('.validation-summary-errors')) {
            chooser.querySelector('details').open = true;
            focusTarget = chooser.querySelector('[name="Name"]');
        }
        if (focusTarget) focusTarget.focus();
    }

    async function load(url, focusKey) {
        var response = await fetch(url, {
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'text/html' },
            signal: activeRequest.signal,
            cache: 'no-store'
        });
        checkSession(response);
        if (!response.ok) throw new Error(response.status === 404 ? 'Công thức không còn hiển thị.' : 'Chưa tải được bộ sưu tập. Hãy thử lại.');
        var html = await response.text();
        if (dialog.open) render(html, focusKey);
    }

    document.addEventListener('click', async function (event) {
        var link = event.target.closest('a[data-collection-chooser], a[data-collection-page]');
        if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        var isPage = link.hasAttribute('data-collection-page');
        if (isPage && (!dialog || !dialog.contains(link))) return;
        if (!ensureDialog()) return;
        event.preventDefault();
        if (pending) return;
        if (!isPage) {
            opener = link;
            currentUrl = link.href;
            content.replaceChildren();
            var fallback = document.createElement('a');
            fallback.href = link.href;
            fallback.textContent = 'Mở trang bộ sưu tập';
            fallback.className = 'collection-back-link';
            content.appendChild(fallback);
            dialog.showModal();
        }
        pending = true;
        activeRequest = new AbortController();
        announce('Đang tải bộ sưu tập…');
        try {
            await load(link.href);
            announce('');
        } catch (error) {
            if (error.name !== 'AbortError') announce(error.message || 'Chưa tải được bộ sưu tập. Hãy thử lại.', true);
        } finally {
            pending = false;
        }
    });

    document.addEventListener('submit', async function (event) {
        var form = event.target.closest('form[data-collection-form]');
        if (!form || !dialog || !dialog.contains(form)) return;
        event.preventDefault();
        if (pending) return;
        pending = true;
        activeRequest = new AbortController();
        var button = event.submitter || form.querySelector('button[type="submit"]');
        var focusKey = button && button.dataset.collectionFocus;
        var formData = new FormData(form);
        var buttons = Array.from(content.querySelectorAll('button[type="submit"]'));
        buttons.forEach(function (item) { item.disabled = true; });
        form.setAttribute('aria-busy', 'true');
        announce('Đang lưu thay đổi…');
        try {
            var response = await fetch(form.action, {
                method: 'POST',
                body: formData,
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' },
                signal: activeRequest.signal
            });
            checkSession(response);
            if (response.status === 400 && (response.headers.get('Content-Type') || '').indexOf('text/html') !== -1) {
                render(await response.text(), 'create');
                announce('Kiểm tra thông tin bộ sưu tập bên dưới.', true);
                return;
            }
            if ((response.headers.get('Content-Type') || '').indexOf('application/json') === -1) {
                throw new Error(response.status === 400 ? 'Phiên biểu mẫu đã hết. Đóng và mở lại bộ sưu tập để thử lại.' : 'Chưa lưu được thay đổi. Hãy thử lại.');
            }
            var result = await response.json();
            if (!response.ok) throw new Error(result.message || 'Chưa lưu được thay đổi. Hãy thử lại.');
            if (!dialog.open) return;
            await load(result.next || currentUrl, focusKey);
            announce(result.message || 'Đã lưu thay đổi.');
            if (typeof result.isSaved === 'boolean' && window.CookShareSaved) {
                window.CookShareSaved.refreshSavedState();
            }
            document.dispatchEvent(new CustomEvent('cookshare:collections-updated', { detail: result }));
        } catch (error) {
            if (error.name !== 'AbortError') announce(error.message || 'Kết nối bị gián đoạn. Hãy thử lại.', true);
        } finally {
            pending = false;
            buttons.forEach(function (item) { item.disabled = false; });
            form.removeAttribute('aria-busy');
        }
    });

    // A returned tab must request fresh private state after another tab changes account.
    document.addEventListener('visibilitychange', function () {
        if (document.hidden && dialog && dialog.open) dialog.close();
    });
    window.addEventListener('pagehide', function () {
        if (dialog && dialog.open) dialog.close();
    });
})();
