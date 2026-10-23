(function () {
    "use strict";

    const buttons = document.querySelectorAll("[data-notification-filter]");
    const items = document.querySelectorAll(".notification-list .notification-item");
    const empty = document.getElementById("notificationFilterEmpty");
    if (buttons.length === 0) return;

    buttons.forEach(function (button) {
        button.addEventListener("click", function () {
            const unreadOnly = button.dataset.notificationFilter === "unread";
            buttons.forEach(function (item) {
                const active = item === button;
                item.classList.toggle("is-active", active);
                item.setAttribute("aria-pressed", active ? "true" : "false");
            });

            let visible = 0;
            items.forEach(function (item) {
                const hide = unreadOnly && !item.classList.contains("is-unread");
                item.hidden = hide;
                if (!hide) visible += 1;
            });

            if (empty) empty.hidden = visible !== 0;
        });
    });
})();
