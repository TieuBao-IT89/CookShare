(function () {
    "use strict";

    const start = document.querySelector("[data-cooking-start]");
    const mode = document.querySelector("[data-cooking-mode]");
    if (!start || !mode) return;

    const steps = Array.from(mode.querySelectorAll("[data-cooking-step]"), function (item) {
        return item.textContent.trim();
    }).filter(Boolean);
    if (steps.length === 0) return;

    const progress = mode.querySelector("[data-cooking-progress]");
    const label = mode.querySelector("[data-cooking-step-label]");
    const text = mode.querySelector("[data-cooking-step-text]");
    const complete = mode.querySelector("[data-cooking-complete]");
    const previous = mode.querySelector("[data-cooking-prev]");
    const next = mode.querySelector("[data-cooking-next]");
    const doneButton = mode.querySelector("[data-cooking-done]");
    const exit = mode.querySelector("[data-cooking-exit]");
    const storageKey = "cookshare_cooking_step_" + mode.dataset.recipeId;
    let index = 0;
    let done = steps.map(function () { return false; });

    function load() {
        try {
            const saved = JSON.parse(sessionStorage.getItem(storageKey) || "");
            const savedIndex = Number(saved.index);
            if (!Array.isArray(saved.done) || saved.done.length !== steps.length) return;
            if (!Number.isInteger(savedIndex) || savedIndex < 0 || savedIndex >= steps.length) return;
            index = savedIndex;
            done = saved.done.map(Boolean);
        } catch {
            /* A blocked or outdated session keeps the first step. */
        }
    }

    function save() {
        try {
            sessionStorage.setItem(storageKey, JSON.stringify({ index: index, done: done }));
        } catch {
            /* Private browsing can refuse session storage. */
        }
    }

    function render() {
        progress.textContent = (index + 1) + "/" + steps.length;
        label.textContent = "Bước " + (index + 1);
        text.textContent = steps[index];
        previous.disabled = index === 0;
        next.disabled = index === steps.length - 1;
        const finished = done.every(Boolean);
        doneButton.setAttribute("aria-pressed", String(done[index]));
        doneButton.textContent = done[index] ? "Đã xong bước này" : "Đánh dấu xong";
        complete.hidden = !finished;
    }

    function go(target) {
        if (target < 0 || target >= steps.length || target === index) return;
        index = target;
        save();
        render();
    }

    start.addEventListener("click", function () {
        load();
        render();
        if (typeof mode.showModal === "function") mode.showModal();
    });

    previous.addEventListener("click", function () { go(index - 1); });
    next.addEventListener("click", function () { go(index + 1); });
    doneButton.addEventListener("click", function () {
        done[index] = true;
        if (index < steps.length - 1) index += 1;
        save();
        render();
    });
    exit.addEventListener("click", function () {
        if (typeof mode.close === "function") mode.close();
        start.focus();
    });
})();
