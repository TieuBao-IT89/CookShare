/**
 * Legacy browser-local Saved collection and reusable presentation helpers.
 * Published recipes are rendered by MVC from the database. This module does not
 * initialize published pages, seed browser data, or create/edit/delete recipes.
 */

const RECIPE_STORAGE_KEY = "cookshare_recipes";

function reportRecipeStorageError(message) {
    window.CookShareUI?.toast(message, "error");
    let notice = document.getElementById("recipeDataError");
    if (!notice) {
        notice = document.createElement("p");
        notice.id = "recipeDataError";
        notice.className = "notice container storage-error";
        notice.setAttribute("role", "alert");
        document.getElementById("main-content")?.prepend(notice);
    }
    notice.textContent = message;
}

// Kept for the legacy Saved/likes modules only. A local numeric ID is never a
// database recipe ID, and this collection must not supply published pages.
function getRecipes() {
    if (!document.getElementById("savedRecipeGrid")) return [];
    try {
        const data = localStorage.getItem(RECIPE_STORAGE_KEY);
        if (data === null) return [];
        const parsed = JSON.parse(data);
        if (!Array.isArray(parsed)) throw new Error("Invalid recipe collection");
        const valid = parsed.every(recipe => recipe && typeof recipe.name === "string" && Number.isSafeInteger(Number(recipe.id)) && Number(recipe.id) > 0);
        if (!valid) throw new Error("Invalid recipe data");
        return parsed;
    } catch {
        reportRecipeStorageError("Không thể đọc công thức trên thiết bị này. Hãy kiểm tra quyền lưu trữ của trình duyệt rồi tải lại trang. Dữ liệu hiện có chưa bị thay đổi.");
        return [];
    }
}

function escapeHtml(text) {
    if (!text) return "";
    return String(text)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

function recipeImageSource(value) {
    if (typeof value !== "string" || !value) return "/img/recipe-placeholder.svg";
    // Existing local image records remain readable; no new uploads are stored.
    if (/^data:image\/(png|jpe?g|webp|gif|avif);base64,/i.test(value)) return value;
    try {
        const url = new URL(value, location.origin);
        const sampleImages = {
            "photo-1582878826629-29b7ad1cdc43": "pho",
            "photo-1569058242253-92a9c755a0ec": "chicken-rice",
            "photo-1621996346565-e3dbc646d9a9": "pasta",
            "photo-1512621776951-a57141f2eefd": "salad",
            "photo-1571877227200-a0d98ea607e9": "tiramisu",
            "photo-1499638673689-79a0b5115d87": "peach-tea",
            "photo-1494790108377-be9c29b29330": "profile-avatar"
        };
        const localPhoto = url.hostname === "images.unsplash.com" && sampleImages[url.pathname.slice(1)];
        if (localPhoto) return `/img/recipes/${localPhoto}.webp`;
        if (["http:", "https:"].includes(url.protocol)) return url.href;
    } catch { /* Use the local fallback for invalid image sources. */ }
    return "/img/recipe-placeholder.svg";
}

function renderRecipeCard(recipe, options = {}) {
    const card = document.getElementById("recipeCardTemplate").content.firstElementChild.cloneNode(true);
    const id = Number(recipe.id);
    card.dataset.id = id;
    card.dataset.recipeSource = "legacy-local";
    if (options.saved) card.dataset.savedRecipeId = id;
    const image = card.querySelector("img");
    image.src = recipeImageSource(recipe.image);
    image.alt = recipe.name;
    card.querySelector(".recipe-category-badge").textContent = recipe.category || "Món ngon";
    card.querySelector(".card-category").textContent = recipe.category || "Công thức trên thiết bị";
    const ingredients = String(recipe.ingredients || "").split(/\r?\n/).map(line => line.trim()).filter(Boolean);
    const instructions = String(recipe.instructions || "").split(/\r?\n/).map(line => line.trim()).filter(Boolean);
    card.querySelector(".card-ingredient-count").textContent = `${ingredients.length} nguyên liệu`;
    card.querySelector(".recipe-card-title span").textContent = recipe.name;
    card.querySelector(".recipe-card-desc").textContent = recipe.description || "Công thức đã lưu trên thiết bị này.";
    card.querySelector(".card-author-name").textContent = recipe.author || "Đầu bếp CookShare";
    card.querySelectorAll(".recipe-save-btn, .recipe-like-btn, .recipe-like-count-display, .recipe-rating-display").forEach(element => {
        element.dataset.recipeId = id;
        if (element.matches("button")) {
            element.dataset.recipeName = recipe.name;
            element.setAttribute("aria-label", `${element.classList.contains("recipe-like-btn") ? "Thích" : "Lưu"} ${recipe.name}`);
        }
    });
    // Keep saved content accessible without sending legacy IDs to MVC routes.
    [
        [".legacy-recipe-ingredients", ingredients, "Chưa có thông tin nguyên liệu."],
        [".legacy-recipe-instructions", instructions, "Chưa có hướng dẫn chế biến."]
    ].forEach(([selector, lines, emptyText]) => {
        const list = card.querySelector(selector);
        (lines.length ? lines : [emptyText]).forEach(line => {
            const item = document.createElement("li");
            item.textContent = line;
            list.append(item);
        });
    });
    card.querySelector(".saved-card-unsave-btn").hidden = !options.saved;
    return card.outerHTML;
}

function syncRecipeCards() {
    window.CookShareLikes?.syncAllRecipeLikesUI();
    window.CookShareSaved?.syncSavedRecipeUI();
    window.CookShareRatings?.syncAllRecipeRatingsUI();
}
