/**
 * COOKSHARE - RECIPE LIKES MODULE
 * Quản lý tương tác Thích / Bỏ thích (Like / Unlike) công thức bằng localStorage
 * Đồng bộ trạng thái trên toàn bộ website (Home, Recipe List, Recipe Details).
 */

(function () {
  "use strict";

  // ==========================================================================
  // 1. HẰNG SỐ & DỮ LIỆU CƠ SỞ (CANONICAL BASELINE)
  // ==========================================================================

  const RECIPE_LIKES_STORAGE_KEY = "cookshare_recipe_likes_v1";

  /**
   * Số lượt thích cơ sở (baseline) ổn định cho các công thức mẫu mặc định.
   * displayedLikeCount = baseLikeCount + (isLiked ? 1 : 0)
   */
  const CANONICAL_BASE_LIKES = {
    1: 321, // Phở Bò Hà Nội
    2: 214, // Cơm Gà Xé Hội An / Bánh Tiramisu
    3: 186, // Mì Ý Sốt Cà Chua Bò Bằm / Cơm gà
    4: 95,  // Salad Bơ Trứng & Rau Củ Thanh Nhiệt
    5: 142, // Bánh Tiramisu Cà Phê Ý
    6: 88   // Trà Đào Cam Sả Mát Lạnh
  };

  // ==========================================================================
  // 2. CÁC HÀM XỬ LÝ LOCALSTORAGE
  // ==========================================================================

  /**
   * Đọc toàn bộ trạng thái likes từ localStorage.
   * Định dạng lưu trữ: { "1": true, "2": false, ... }
   * @returns {Object.<string, boolean>}
   */
  function loadAllRecipeLikes() {
    try {
      const raw = localStorage.getItem(RECIPE_LIKES_STORAGE_KEY);
      if (!raw) return {};
      const parsed = JSON.parse(raw);
      if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
        return parsed;
      }
      return {};
    } catch (err) {
      console.warn("[CookShare Likes] Lỗi khi đọc dữ liệu từ localStorage:", err);
      return {};
    }
  }

  /**
   * Ghi toàn bộ trạng thái likes vào localStorage.
   * @param {Object.<string, boolean>} likesObj
   * @returns {boolean} true nếu lưu thành công
   */
  function saveAllRecipeLikes(likesObj) {
    try {
      localStorage.setItem(RECIPE_LIKES_STORAGE_KEY, JSON.stringify(likesObj));
      return true;
    } catch (err) {
      console.error("[CookShare Likes] Lỗi khi ghi dữ liệu vào localStorage:", err);
      return false;
    }
  }

  /**
   * Lấy số lượt thích cơ sở (baseline) của một công thức.
   * @param {number|string} recipeId
   * @returns {number}
   */
  function getRecipeBaseLikes(recipeId) {
    const id = Number(recipeId);
    if (!id || isNaN(id) || id <= 0) return 0;

    if (Object.prototype.hasOwnProperty.call(CANONICAL_BASE_LIKES, id)) {
      return CANONICAL_BASE_LIKES[id];
    }

    // Nếu công thức được người dùng tạo mới trong localStorage
    if (typeof getRecipes === "function") {
      try {
        const recipes = getRecipes();
        const r = recipes.find((item) => Number(item.id) === id);
        if (r && typeof r.likes === "number" && !isNaN(r.likes)) {
          return Math.max(0, r.likes);
        }
      } catch (e) {
        // Fallback im lặng nếu getRecipes chưa sẵn sàng
      }
    }

    return 0;
  }

  /**
   * Kiểm tra xem người dùng hiện tại đã thích công thức này chưa.
   * @param {number|string} recipeId
   * @returns {boolean}
   */
  function isRecipeLiked(recipeId) {
    const id = Number(recipeId);
    if (!id || isNaN(id) || id <= 0) return false;

    const allLikes = loadAllRecipeLikes();
    return Boolean(allLikes[String(id)]);
  }

  /**
   * Tính toán số lượt thích hiển thị trên giao diện:
   * displayedCount = baseLikeCount + (isLiked ? 1 : 0)
   * Không bao giờ âm.
   * @param {number|string} recipeId
   * @returns {number}
   */
  function getRecipeDisplayLikeCount(recipeId) {
    const id = Number(recipeId);
    if (!id || isNaN(id) || id <= 0) return 0;

    const base = getRecipeBaseLikes(id);
    const liked = isRecipeLiked(id);
    return Math.max(0, base + (liked ? 1 : 0));
  }

  // ==========================================================================
  // 3. TƯƠNG TÁC LIKE / UNLIKE
  // ==========================================================================

  /**
   * Chuyển đổi trạng thái Like / Unlike của công thức.
   * @param {number|string} recipeId
   * @returns {boolean|null} Trạng thái mới (true/false) hoặc null nếu không hợp lệ
   */
  function toggleRecipeLike(recipeId) {
    const id = Number(recipeId);
    if (!id || isNaN(id) || id <= 0) return null;

    const allLikes = loadAllRecipeLikes();
    const currentStatus = Boolean(allLikes[String(id)]);
    const nextStatus = !currentStatus;

    allLikes[String(id)] = nextStatus;

    const success = saveAllRecipeLikes(allLikes);
    if (!success) {
      alert("Không thể lưu trạng thái thích món ăn. Vui lòng kiểm tra dung lượng trình duyệt!");
      return currentStatus;
    }

    // Cập nhật tất cả các vị trí hiển thị của công thức này trên trang
    updateRecipeLikeUI(id);

    return nextStatus;
  }

  // ==========================================================================
  // 4. CẬP NHẬT GIAO DIỆN (UI UPDATES)
  // ==========================================================================

  /**
   * Cập nhật tất cả nút Like và bộ đếm liên quan đến một recipeId cụ thể.
   * @param {number|string} recipeId
   */
  function updateRecipeLikeUI(recipeId) {
    const id = Number(recipeId);
    if (!id || isNaN(id) || id <= 0) return;

    const liked = isRecipeLiked(id);
    const count = getRecipeDisplayLikeCount(id);

    // 1. Cập nhật các nút Like: .recipe-like-btn[data-recipe-id="{id}"]
    const buttons = document.querySelectorAll(`.recipe-like-btn[data-recipe-id="${id}"]`);
    buttons.forEach((btn) => {
      btn.setAttribute("aria-pressed", liked ? "true" : "false");

      if (liked) {
        btn.classList.add("liked");
      } else {
        btn.classList.remove("liked");
      }

      // Cập nhật icon
      const iconEl = btn.querySelector(".like-icon");
      if (iconEl) {
        iconEl.textContent = liked ? "♥" : "♡";
      }

      // Cập nhật text label (nếu có .like-text)
      const textEl = btn.querySelector(".like-text");
      if (textEl) {
        textEl.textContent = liked ? "Đã thích" : "Thích";
      }

      // Cập nhật aria-label
      const currentAriaLabel = btn.getAttribute("aria-label") || "";
      if (currentAriaLabel.includes("Thích")) {
        const recipeTitle = btn.getAttribute("data-recipe-name") || "công thức";
        btn.setAttribute(
          "aria-label",
          liked ? `Bỏ thích ${recipeTitle}` : `Thích ${recipeTitle}`
        );
      }
    });

    // 2. Cập nhật các phần tử hiển thị số like: .recipe-like-count-display[data-recipe-id="{id}"]
    const countDisplays = document.querySelectorAll(`.recipe-like-count-display[data-recipe-id="${id}"]`);
    countDisplays.forEach((elem) => {
      const format = elem.getAttribute("data-like-format") || "default";

      if (liked) {
        elem.classList.add("liked");
      } else {
        elem.classList.remove("liked");
      }

      switch (format) {
        case "feed":
          // Định dạng cho bài đăng trên feed trang chủ: "♡ 321 lượt thích" hoặc "♥ 322 lượt thích"
          elem.textContent = `${liked ? "♥" : "♡"} ${count.toLocaleString("vi-VN")} lượt thích`;
          break;
        case "trending":
          // Định dạng cột trending: "321 lượt thích"
          elem.textContent = `${count.toLocaleString("vi-VN")} lượt thích`;
          break;
        case "badge":
        case "number":
        case "meta":
        default:
          // Chỉ hiển thị số lượng
          elem.textContent = count.toLocaleString("vi-VN");
          break;
      }
    });
  }

  /**
   * Đồng bộ toàn bộ các phần tử Like trên trang hiện tại.
   */
  function syncAllRecipeLikesUI() {
    const identifiedIds = new Set();

    // Tìm tất cả các recipeId hiện có trên trang từ các nút Like và display
    document.querySelectorAll(".recipe-like-btn[data-recipe-id]").forEach((btn) => {
      const id = btn.getAttribute("data-recipe-id");
      if (id) identifiedIds.add(id);
    });

    document.querySelectorAll(".recipe-like-count-display[data-recipe-id]").forEach((elem) => {
      const id = elem.getAttribute("data-recipe-id");
      if (id) identifiedIds.add(id);
    });

    // Cập nhật từng recipeId
    identifiedIds.forEach((id) => {
      updateRecipeLikeUI(id);
    });
  }

  // ==========================================================================
  // 5. KHỞI TẠO & SỰ KIỆN TOÀN CỤC (EVENT DELEGATION)
  // ==========================================================================

  // Sử dụng Event Delegation để bắt mọi thao tác bấm nút Like trên toàn trang,
  // bao gồm cả các card được render động sau khi search hay lọc danh mục.
  document.addEventListener("click", function (event) {
    const likeBtn = event.target.closest(".recipe-like-btn");
    if (!likeBtn) return;

    // Ngăn chặn sự kiện click lan ra ngoài gây chuyển trang
    event.preventDefault();
    event.stopPropagation();

    const recipeId = likeBtn.getAttribute("data-recipe-id");
    if (!recipeId) return;

    toggleRecipeLike(recipeId);
  });

  // Lắng nghe sự kiện storage để đồng bộ giữa các tabs trình duyệt
  window.addEventListener("storage", function (event) {
    if (event.key === RECIPE_LIKES_STORAGE_KEY) {
      syncAllRecipeLikesUI();
    }
  });

  // Khởi tạo trạng thái ban đầu khi DOM sẵn sàng
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", syncAllRecipeLikesUI);
  } else {
    syncAllRecipeLikesUI();
  }

  // ==========================================================================
  // 6. XUẤT RA GLOBAL WINDOW OBJECT (PUBLIC API)
  // ==========================================================================

  window.CookShareLikes = {
    RECIPE_LIKES_STORAGE_KEY,
    isRecipeLiked,
    toggleRecipeLike,
    getRecipeBaseLikes,
    getRecipeDisplayLikeCount,
    updateRecipeLikeUI,
    syncAllRecipeLikesUI
  };
})();

