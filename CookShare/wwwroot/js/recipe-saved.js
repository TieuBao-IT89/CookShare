/**
 * COOKSHARE - RECIPE SAVED (FAVORITES) MODULE
 * Quản lý tính năng Lưu / Bỏ lưu công thức (Saved / Unsaved) bằng localStorage.
 * Hoạt động độc lập với hệ thống Like (Issue #44) và Bình luận (Issue #43).
 * Đồng bộ trạng thái trên toàn bộ website (Home, Recipe List, Recipe Details, Saved Page).
 */

(function () {
  "use strict";

  // ==========================================================================
  // 1. HẰNG SỐ
  // ==========================================================================

  const SAVED_RECIPES_STORAGE_KEY = "cookshare_saved_recipes_v1";

  // ==========================================================================
  // 2. CÁC HÀM XỬ LÝ LOCALSTORAGE
  // ==========================================================================

  /**
   * Đọc danh sách ID công thức đã lưu từ localStorage.
   * @returns {number[]} Mảng các ID dạng số nguyên.
   */
  function getSavedRecipeIds() {
    try {
      const raw = localStorage.getItem(SAVED_RECIPES_STORAGE_KEY);
      if (!raw) return [];
      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) return [];
      return parsed.map(Number).filter((n) => !isNaN(n));
    } catch (err) {
      console.error("[CookShareSaved] Lỗi đọc saved recipes từ localStorage:", err);
      return [];
    }
  }

  /**
   * Ghi danh sách ID công thức đã lưu vào localStorage.
   * @param {number[]} ids Mảng ID dạng số nguyên.
   * @returns {boolean} true nếu ghi thành công.
   */
  function saveSavedRecipeIds(ids) {
    try {
      localStorage.setItem(SAVED_RECIPES_STORAGE_KEY, JSON.stringify(ids));
      return true;
    } catch (err) {
      console.error("[CookShareSaved] Lỗi ghi saved recipes vào localStorage:", err);
      return false;
    }
  }

  // ==========================================================================
  // 3. CÁC HÀM NGHIỆP VỤ (BUSINESS LOGIC)
  // ==========================================================================

  /**
   * Kiểm tra công thức có đang được lưu không.
   * @param {number|string} recipeId ID công thức.
   * @returns {boolean}
   */
  function isRecipeSaved(recipeId) {
    const id = Number(recipeId);
    if (!id) return false;
    return getSavedRecipeIds().includes(id);
  }

  /**
   * Lưu công thức vào danh sách yêu thích.
   * @param {number|string} recipeId ID công thức.
   */
  function saveRecipe(recipeId) {
    const id = Number(recipeId);
    if (!id) return;
    const ids = getSavedRecipeIds();
    if (!ids.includes(id)) {
      ids.push(id);
      saveSavedRecipeIds(ids);
    }
  }

  /**
   * Bỏ lưu công thức khỏi danh sách yêu thích.
   * @param {number|string} recipeId ID công thức.
   */
  function unsaveRecipe(recipeId) {
    const id = Number(recipeId);
    if (!id) return;
    const ids = getSavedRecipeIds().filter((n) => n !== id);
    saveSavedRecipeIds(ids);
  }

  /**
   * Chuyển đổi trạng thái lưu của công thức, cập nhật UI.
   * @param {number|string} recipeId ID công thức.
   * @returns {boolean} Trạng thái mới (true = đã lưu).
   */
  function toggleSavedRecipe(recipeId) {
    const id = Number(recipeId);
    if (!id) return false;

    const wasSaved = isRecipeSaved(id);
    if (wasSaved) {
      unsaveRecipe(id);
    } else {
      saveRecipe(id);
    }

    const nowSaved = isRecipeSaved(id);
    if (nowSaved === wasSaved) {
      window.CookShareUI?.toast("Chưa thể thay đổi danh sách đã lưu. Hãy kiểm tra bộ nhớ trình duyệt.", "error");
      return wasSaved;
    }
    window.CookShareUI?.toast(nowSaved ? "Đã thêm vào góc công thức của bạn." : "Đã bỏ lưu công thức.");
    updateSavedRecipeUI(id);

    // Nếu đang ở trang Saved, xóa card đó ra khỏi danh sách
    const savedGrid = document.getElementById("savedRecipeGrid");
    if (savedGrid && !nowSaved) {
      const removedCard = savedGrid.querySelector(`[data-saved-recipe-id="${id}"]`);
      if (removedCard) {
        const nextCard = removedCard.nextElementSibling || removedCard.previousElementSibling;
        removedCard.remove();
        _refreshSavedPageCount();
        _checkSavedPageEmpty();
        (nextCard?.querySelector(".recipe-save-btn") || document.querySelector("#savedEmptyState .btn"))?.focus();
      }
    }

    return nowSaved;
  }

  // ==========================================================================
  // 4. CÁC HÀM CẬP NHẬT GIAO DIỆN (UI SYNC)
  // ==========================================================================

  /**
   * Cập nhật giao diện của tất cả nút Save có data-recipe-id khớp.
   * @param {number|string} recipeId ID công thức.
   */
  function updateSavedRecipeUI(recipeId) {
    const id = Number(recipeId);
    if (!id) return;

    const saved = isRecipeSaved(id);
    const buttons = document.querySelectorAll(`.recipe-save-btn[data-recipe-id="${id}"]`);

    buttons.forEach((btn) => {
      const name = btn.dataset.recipeName || "công thức";
      btn.setAttribute("aria-label", `${saved ? "Bỏ lưu" : "Lưu"} ${name}`);
      if (saved) {
        btn.classList.add("saved");
        btn.setAttribute("aria-pressed", "true");
        btn.setAttribute("title", "Bỏ lưu công thức này");
        const iconEl = btn.querySelector(".save-icon");
        const textEl = btn.querySelector(".save-text");
        if (iconEl) iconEl.innerHTML = '<svg class="icon" aria-hidden="true"><use href="#i-bookmark" /></svg>';
        if (textEl) textEl.textContent = "Đã lưu";
      } else {
        btn.classList.remove("saved");
        btn.setAttribute("aria-pressed", "false");
        btn.setAttribute("title", "Lưu công thức này");
        const iconEl = btn.querySelector(".save-icon");
        const textEl = btn.querySelector(".save-text");
        if (iconEl) iconEl.innerHTML = '<svg class="icon" aria-hidden="true"><use href="#i-bookmark" /></svg>';
        if (textEl) textEl.textContent = "Lưu";
      }
    });

    // Cập nhật trạng thái "Đã lưu" trên trang chi tiết nếu đang xem đúng công thức
    const detailsSavedStatus = document.getElementById("detailsSavedStatus");
    const detailsRecipeId = document.getElementById("detailsRecipeId");
    if (detailsSavedStatus && detailsRecipeId && Number(detailsRecipeId.value) === id) {
      detailsSavedStatus.textContent = saved ? "Có ✓" : "Chưa";
    }
  }

  /**
   * Quét toàn bộ nút Save trên trang và cập nhật trạng thái.
   */
  function syncSavedRecipeUI() {
    const buttons = document.querySelectorAll(".recipe-save-btn[data-recipe-id]");
    const ids = new Set();
    buttons.forEach((btn) => {
      const id = btn.getAttribute("data-recipe-id");
      if (id) ids.add(id);
    });
    ids.forEach((id) => updateSavedRecipeUI(id));
  }

  // ==========================================================================
  // 5. TRANG CÔNG THỨC ĐÃ LƯU (/Recipe/Saved)
  // ==========================================================================

  /**
   * Cập nhật số lượng công thức đang hiển thị trên trang Saved.
   */
  function _refreshSavedPageCount() {
    const countEl = document.getElementById("savedRecipeCount");
    const inlineEl = document.getElementById("savedRecipeCountInline");
    const cards = document.querySelectorAll("#savedRecipeGrid [data-saved-recipe-id]");
    if (countEl) countEl.textContent = cards.length;
    if (inlineEl) inlineEl.textContent = cards.length;
  }

  /**
   * Hiển thị trạng thái trống nếu danh sách Saved rỗng.
   */
  function _checkSavedPageEmpty() {
    const emptyEl = document.getElementById("savedEmptyState");
    const gridEl = document.getElementById("savedRecipeGrid");
    if (!emptyEl || !gridEl) return;

    const cards = gridEl.querySelectorAll("[data-saved-recipe-id]");
    if (cards.length === 0) {
      emptyEl.style.display = "flex";
      gridEl.style.display = "none";
    } else {
      emptyEl.style.display = "none";
      gridEl.style.display = "grid";
    }
  }

  /**
   * Khởi tạo và render trang Công thức đã lưu.
   * Chỉ chạy khi có phần tử #savedRecipeGrid trong DOM.
   */
  function initializeSavedPage() {
    const gridEl = document.getElementById("savedRecipeGrid");
    if (!gridEl) return;

    const savedIds = getSavedRecipeIds();
    const countEl = document.getElementById("savedRecipeCount");
    const inlineEl = document.getElementById("savedRecipeCountInline");

    // Lấy dữ liệu công thức từ recipe.js (getRecipes được khai báo global)
    let allRecipes = [];
    if (typeof getRecipes === "function") {
      allRecipes = getRecipes();
    }

    gridEl.innerHTML = "";
    if (savedIds.length === 0) {
      if (countEl) countEl.textContent = "0";
      if (inlineEl) inlineEl.textContent = "0";
      _checkSavedPageEmpty();
      return;
    }

    // Lọc công thức đã lưu, giữ đúng thứ tự lưu (mới lưu nhất đầu tiên)
    const savedRecipes = savedIds
      .slice()
      .reverse()
      .map((id) => allRecipes.find((r) => Number(r.id) === id))
      .filter(Boolean);

    if (countEl) countEl.textContent = savedRecipes.length;
    if (inlineEl) inlineEl.textContent = savedRecipes.length;

    gridEl.innerHTML = savedRecipes
      .map((recipe) => _renderSavedRecipeCard(recipe))
      .join("");

    _checkSavedPageEmpty();
    window.CookShareLikes?.syncAllRecipeLikesUI();
    syncSavedRecipeUI();
  }

  /**
   * Tạo HTML cho card công thức trên trang Saved.
   * @param {object} recipe Đối tượng công thức.
   * @returns {string} Chuỗi HTML.
   */
  function _renderSavedRecipeCard(recipe) {
    return renderRecipeCard(recipe, { saved: true });
  }

  // ==========================================================================
  // 6. EVENT DELEGATION TOÀN CỤC
  // ==========================================================================

  // Bắt sự kiện click cho tất cả nút .recipe-save-btn bằng event delegation
  document.addEventListener("click", function (e) {
    const btn = e.target.closest(".recipe-save-btn");
    if (!btn) return;
    e.preventDefault();
    e.stopPropagation();

    const recipeId = btn.getAttribute("data-recipe-id");
    if (recipeId) {
      toggleSavedRecipe(recipeId);
    }
  });

  // Đồng bộ trạng thái khi tab khác thay đổi localStorage
  window.addEventListener("storage", function (e) {
    if (e.key === SAVED_RECIPES_STORAGE_KEY) {
      syncSavedRecipeUI();
      // Nếu đang trên trang Saved, cần render lại toàn bộ
      const gridEl = document.getElementById("savedRecipeGrid");
      if (gridEl) {
        initializeSavedPage();
      }
    }
  });

  // ==========================================================================
  // 7. AUTO INIT
  // ==========================================================================

  document.addEventListener("DOMContentLoaded", function () {
    // Khởi tạo trang Saved nếu đang trên trang đó
    initializeSavedPage();
    // Đồng bộ trạng thái nút Save trên mọi trang
    syncSavedRecipeUI();
  });

  // ==========================================================================
  // 8. PUBLIC API
  // ==========================================================================

  window.CookShareSaved = {
    isRecipeSaved,
    saveRecipe,
    unsaveRecipe,
    toggleSavedRecipe,
    updateSavedRecipeUI,
    syncSavedRecipeUI,
    initializeSavedPage
  };
})();
