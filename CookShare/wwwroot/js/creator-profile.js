(function () {
    "use strict";

    var avatar = document.querySelector(".profile-avatar[data-avatar-fallback]");
    if (avatar) {
        avatar.addEventListener("error", function () {
            if (avatar.getAttribute("src") !== avatar.getAttribute("data-avatar-fallback")) {
                avatar.setAttribute("src", avatar.getAttribute("data-avatar-fallback"));
            }
        });
    }

    var cover = document.querySelector("[data-cover-image]");
    if (cover) {
        cover.addEventListener("error", function () {
            var frame = document.getElementById("profileCover");
            cover.remove();
            if (frame) {
                frame.classList.add("is-fallback");
            }
        });
    }

    document.querySelectorAll(".profile-tab").forEach(function (tab) {
        tab.addEventListener("click", function () {
            document.querySelectorAll(".profile-tab").forEach(function (item) {
                item.classList.remove("active");
                item.removeAttribute("aria-current");
            });
            tab.classList.add("active");
            tab.setAttribute("aria-current", "page");
        });
    });

    var bio = document.getElementById("profileBio");
    var bioCount = document.getElementById("profileBioCount");
    function updateBioCount() {
        if (!bio || !bioCount) {
            return;
        }
        bioCount.textContent = bio.value.length + " / 300";
    }
    if (bio) {
        bio.addEventListener("input", updateBioCount);
        updateBioCount();
    }

    function bindPreview(inputId, imageId) {
        var input = document.getElementById(inputId);
        var image = document.getElementById(imageId);
        if (!input || !image) {
            return;
        }
        function apply() {
            var value = input.value.trim();
            if (!value) {
                image.removeAttribute("src");
                image.classList.add("is-empty");
                return;
            }
            image.classList.remove("is-empty");
            image.src = value;
        }
        image.addEventListener("error", function () {
            image.removeAttribute("src");
            image.classList.add("is-empty");
        });
        input.addEventListener("input", apply);
        apply();
    }
    bindPreview("avatarUrlInput", "avatarPreview");
    bindPreview("coverUrlInput", "coverPreview");

    var dialog = document.getElementById("highlightDialog");
    var picker = document.getElementById("highlightPicker");
    var inputs = document.getElementById("highlightInputs");
    var count = document.getElementById("highlightCount");
    var formError = document.getElementById("highlightFormError");

    function selectedItems() {
        if (!picker) {
            return [];
        }
        return Array.prototype.filter.call(picker.querySelectorAll("li"), function (item) {
            var choice = item.querySelector("[data-highlight-choice]");
            return choice && choice.checked;
        });
    }

    function syncHighlights() {
        if (!inputs || !count) {
            return;
        }
        var items = selectedItems();
        inputs.replaceChildren();
        items.forEach(function (item) {
            var hidden = document.createElement("input");
            hidden.type = "hidden";
            hidden.name = "recipeIds";
            hidden.value = item.getAttribute("data-recipe-id");
            inputs.appendChild(hidden);
        });
        count.textContent = items.length + "/3";
    }

    if (picker) {
        picker.addEventListener("change", function (event) {
            if (!event.target.matches("[data-highlight-choice]")) {
                return;
            }
            if (selectedItems().length > 3) {
                event.target.checked = false;
                if (formError) {
                    formError.hidden = false;
                    formError.textContent = "Chỉ được ghim tối đa 3 công thức.";
                }
            } else if (formError) {
                formError.hidden = true;
            }
            syncHighlights();
        });
        picker.addEventListener("click", function (event) {
            var button = event.target.closest("[data-move]");
            if (!button) {
                return;
            }
            var item = button.closest("li");
            if (button.getAttribute("data-move") === "up" && item.previousElementSibling) {
                item.parentElement.insertBefore(item, item.previousElementSibling);
            }
            if (button.getAttribute("data-move") === "down" && item.nextElementSibling) {
                item.parentElement.insertBefore(item.nextElementSibling, item);
            }
            syncHighlights();
        });
    }

    document.querySelectorAll("[data-open-highlights]").forEach(function (button) {
        button.addEventListener("click", function () {
            if (dialog && typeof dialog.showModal === "function") {
                dialog.showModal();
            }
        });
    });

    var cancel = document.querySelector("[data-highlight-cancel]");
    if (cancel && dialog) {
        cancel.addEventListener("click", function () {
            dialog.close();
        });
    }
})();
