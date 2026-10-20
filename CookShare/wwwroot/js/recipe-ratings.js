/**
 * COOKSHARE - RECIPE RATINGS MODULE (1–5 STAR INTERACTION)
 * Quản lý tính năng đánh giá công thức từ 1 đến 5 sao bằng localStorage.
 * Đồng bộ điểm trung bình, số lượng đánh giá và hiển thị phản hồi mượt mà.
 */

(function () {
  "use strict";

  // ==========================================================================
  // 1. HẰNG SỐ & DỮ LIỆU
  // ==========================================================================

  const RECIPE_RATINGS_STORAGE_KEY = "cookshare_recipe_ratings_v1";

  const RATING_LABELS = {
    1: "Rất tệ",
    2: "Chưa tốt",
    3: "Bình thường",
    4: "Rất ngon",
    5: "Xuất sắc"
  };

  // ==========================================================================
  // 2. CÁC HÀM XỬ LÝ LOCALSTORAGE (SAFE READ/WRITE)
  // ==========================================================================

  /**
   * Đọc toàn bộ dữ liệu đánh giá từ localStorage.
   * Xử lý an toàn khi JSON hỏng hoặc key không tồn tại.
   * @returns {Object<string, {rating: number, updatedAt: string}>}
   */
  function loadAllRatings() {
    try {
      const raw = localStorage.getItem(RECIPE_RATINGS_STORAGE_KEY);
      if (!raw) return {};
      const parsed = JSON.parse(raw);
      if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
        return {};
      }

      // Lọc và làm sạch dữ liệu, chỉ giữ lại các bản ghi hợp lệ
      const cleaned = {};
      Object.keys(parsed).forEach((k) => {
        const item = parsed[k];
        if (
          item &&
          typeof item === "object" &&
          Number.isInteger(item.rating) &&
          item.rating >= 1 &&
          item.rating <= 5
        ) {
          cleaned[k] = {
            rating: item.rating,
            updatedAt: typeof item.updatedAt === "string" ? item.updatedAt : new Date().toISOString()
          };
        }
      });
      return cleaned;
    } catch (err) {
      console.warn("[CookShare Ratings] Lỗi đọc dữ liệu đánh giá từ localStorage:", err);
      return {};
    }
  }

  /**
   * Ghi toàn bộ dữ liệu đánh giá vào localStorage.
   * @param {Object} ratingsMap
   * @returns {boolean} true nếu lưu thành công, false nếu thất bại
   */
  function saveAllRatings(ratingsMap) {
    try {
      localStorage.setItem(RECIPE_RATINGS_STORAGE_KEY, JSON.stringify(ratingsMap));
      return true;
    } catch (err) {
      console.error("[CookShare Ratings] Lỗi ghi dữ liệu đánh giá vào localStorage:", err);
      return false;
    }
  }

  // ==========================================================================
  // 3. CÁC HÀM NGHIỆP VỤ (BUSINESS LOGIC)
  // ==========================================================================

  /**
   * Lấy thông tin đánh giá của người dùng cho một công thức.
   * @param {number|string} recipeId
   * @returns {{rating: number, updatedAt: string}|null}
   */
  function getRecipeRating(recipeId) {
    const id = Number(recipeId);
    if (!id || id <= 0) return null;

    const all = loadAllRatings();
    return all[String(id)] || null;
  }

  /**
   * Lưu hoặc cập nhật đánh giá (1-5 sao) cho một công thức.
   * @param {number|string} recipeId
   * @param {number} rating Giá trị nguyên từ 1 đến 5
   * @returns {{success: boolean, wasUpdate: boolean, rating: number, message: string}}
   */
  function saveRecipeRating(recipeId, rating) {
    const id = Number(recipeId);
    if (!id || id <= 0) {
      return { success: false, wasUpdate: false, rating: 0, message: "Mã công thức không hợp lệ." };
    }

    const ratingVal = Number(rating);
    if (!Number.isInteger(ratingVal) || ratingVal < 1 || ratingVal > 5) {
      return {
        success: false,
        wasUpdate: false,
        rating: 0,
        message: "Giá trị đánh giá phải là số nguyên từ 1 đến 5."
      };
    }

    const all = loadAllRatings();
    const existing = all[String(id)];
    const wasUpdate = !!existing;

    all[String(id)] = {
      rating: ratingVal,
      updatedAt: new Date().toISOString()
    };

    const saved = saveAllRatings(all);
    if (!saved) {
      return {
        success: false,
        wasUpdate: false,
        rating: 0,
        message: "Không thể lưu vào bộ nhớ trình duyệt (localStorage error)."
      };
    }

    return {
      success: true,
      wasUpdate: wasUpdate,
      rating: ratingVal,
      message: wasUpdate
        ? `Đã cập nhật đánh giá thành ${ratingVal} sao (${RATING_LABELS[ratingVal]})!`
        : `Cảm ơn bạn đã đánh giá ${ratingVal} sao (${RATING_LABELS[ratingVal]})!`
    };
  }

  /**
   * Tính toán thống kê đánh giá cho một công thức.
   * @param {number|string} recipeId
   * @returns {{userRating: number|null, totalRatings: number, averageRating: number, formattedAverage: string}}
   */
  function getRecipeRatingStatistics(recipeId) {
    const id = Number(recipeId);
    if (!id || id <= 0) {
      return { userRating: null, totalRatings: 0, averageRating: 0, formattedAverage: "Chưa có đánh giá" };
    }

    const userRecord = getRecipeRating(id);
    const userRating = userRecord ? userRecord.rating : null;

    // Trong mô hình demo frontend không có database nhiều người dùng:
    // Tổng số lượt đánh giá bằng 1 nếu người dùng demo đã đánh giá, ngược lại bằng 0
    if (userRating) {
      return {
        userRating: userRating,
        totalRatings: 1,
        averageRating: userRating,
        formattedAverage: `${userRating.toFixed(1)} / 5.0`
      };
    }

    return {
      userRating: null,
      totalRatings: 0,
      averageRating: 0,
      formattedAverage: "Chưa có đánh giá"
    };
  }

  // ==========================================================================
  // 4. TẠO GIAO DIỆN (UI BUILDER)
  // ==========================================================================

  /**
   * Tạo chuỗi sao hiển thị (★/☆).
   * @param {number} rating
   * @returns {string}
   */
  function renderStarsHtml(rating) {
    let html = "";
    const rounded = Math.round(rating) || 0;
    for (let i = 1; i <= 5; i++) {
      if (i <= rounded) {
        html += `<span class="star-filled">★</span>`;
      } else {
        html += `<span class="star-empty">★</span>`;
      }
    }
    return html;
  }

  /**
   * Tạo phần tử DOM Section Đánh giá công thức.
   * @param {number} recipeId
   * @returns {HTMLElement}
   */
  function buildRatingSectionNode(recipeId) {
    const stats = getRecipeRatingStatistics(recipeId);
    const userRating = stats.userRating;

    const section = document.createElement("section");
    section.className = "recipe-rating-section";
    section.id = "recipeRatingSection";
    section.setAttribute("aria-labelledby", "ratingSectionHeading");

    const averageText = stats.totalRatings > 0 ? `${stats.averageRating.toFixed(1)}` : "—";
    const averageStarsHtml = renderStarsHtml(stats.averageRating);
    const countText = stats.totalRatings > 0 ? `${stats.totalRatings} lượt đánh giá` : "Chưa có lượt đánh giá";

    let statusPillText = "Chưa đánh giá";
    let statusPillClass = "rating-status-pill";
    if (userRating) {
      statusPillText = `Đã đánh giá: ${userRating} sao — ${RATING_LABELS[userRating]}`;
      statusPillClass += " has-rating";
    }

    section.innerHTML = `
      <div class="recipe-rating-card">
        <header class="rating-header">
          <div class="rating-header-badge">⭐ ĐÁNH GIÁ MÓN ĂN</div>
          <h2 class="rating-title" id="ratingSectionHeading">Đánh giá công thức</h2>
          <p class="rating-subtitle">Bạn thấy món ăn này thế nào? Hãy chia sẻ đánh giá của bạn!</p>
        </header>

        <div class="rating-content-layout">
          <!-- Cột tóm tắt điểm số -->
          <div class="rating-overview-box">
            <div class="rating-score-huge ${stats.totalRatings === 0 ? "empty" : ""}" id="ratingScoreHuge">
              ${stats.totalRatings > 0 ? averageText : "Chưa có đánh giá"}
            </div>
            <div class="rating-stars-visual" id="ratingStarsVisual" aria-hidden="true">
              ${averageStarsHtml}
            </div>
            <div class="rating-count-text" id="ratingCountText">
              ${countText}
            </div>
          </div>

          <!-- Cột tương tác chấm sao -->
          <div class="rating-interactive-box">
            <p class="rating-action-prompt" id="ratingActionPrompt">
              ${userRating ? "Thay đổi đánh giá của bạn:" : "Chọn số sao để đánh giá:"}
            </p>

            <div class="rating-stars-control"
                 role="radiogroup"
                 aria-label="Đánh giá công thức từ 1 đến 5 sao"
                 id="ratingStarsControl">
              ${[1, 2, 3, 4, 5]
                .map((star) => {
                  const isChecked = userRating === star;
                  const isSelected = userRating && star <= userRating;
                  return `
                    <button type="button"
                            class="star-btn ${isSelected ? "selected" : ""}"
                            role="radio"
                            data-star="${star}"
                            aria-checked="${isChecked ? "true" : "false"}"
                            aria-label="${star} sao - ${RATING_LABELS[star]}"
                            title="${star} sao - ${RATING_LABELS[star]}"
                            tabindex="0">
                      ★
                    </button>
                  `;
                })
                .join("")}
            </div>

            <div class="${statusPillClass}" id="ratingStatusPill" aria-live="polite">
              ${statusPillText}
            </div>

            <div class="rating-feedback-alert" id="ratingFeedbackAlert" role="status" aria-live="polite"></div>
          </div>
        </div>
      </div>
    `;

    attachRatingEvents(section, recipeId);
    return section;
  }

  // ==========================================================================
  // 5. SỰ KIỆN TƯƠNG TÁC (HOVER, CLICK, KEYBOARD)
  // ==========================================================================

  let feedbackTimer = null;

  /**
   * Hiển thị thông báo phản hồi (success hoặc error).
   * @param {HTMLElement} section
   * @param {string} message
   * @param {"success"|"error"} type
   */
  function showFeedback(section, message, type = "success") {
    const alertEl = section.querySelector("#ratingFeedbackAlert");
    if (!alertEl) return;

    if (feedbackTimer) clearTimeout(feedbackTimer);

    alertEl.textContent = (type === "success" ? "✓ " : "⚠️ ") + message;
    alertEl.className = `rating-feedback-alert show ${type}`;

    feedbackTimer = setTimeout(() => {
      alertEl.classList.remove("show");
    }, 4500);
  }

  /**
   * Gắn các bộ xử lý sự kiện tương tác cho khu vực đánh giá.
   * @param {HTMLElement} section
   * @param {number} recipeId
   */
  function attachRatingEvents(section, recipeId) {
    const control = section.querySelector("#ratingStarsControl");
    const statusPill = section.querySelector("#ratingStatusPill");
    const promptEl = section.querySelector("#ratingActionPrompt");
    if (!control || !statusPill) return;

    const starBtns = Array.from(control.querySelectorAll(".star-btn"));

    // Lấy đánh giá đã lưu hiện tại
    function getCurrentSavedRating() {
      const stats = getRecipeRatingStatistics(recipeId);
      return stats.userRating || 0;
    }

    // Cập nhật trạng thái hiển thị của các sao theo mức rating cụ thể
    function highlightStars(count, isHover = false) {
      starBtns.forEach((btn) => {
        const starVal = Number(btn.getAttribute("data-star"));
        if (isHover) {
          if (starVal <= count) {
            btn.classList.add("hovered");
            btn.classList.remove("dimmed");
          } else {
            btn.classList.remove("hovered");
            btn.classList.add("dimmed");
          }
        } else {
          btn.classList.remove("hovered", "dimmed");
          if (starVal <= count) {
            btn.classList.add("selected");
          } else {
            btn.classList.remove("selected");
          }
        }
      });
    }

    // Đặt lại trạng thái sao về mức đã lưu trong localStorage
    function resetStarsToSaved() {
      const saved = getCurrentSavedRating();
      highlightStars(saved, false);
      starBtns.forEach((btn) => {
        const starVal = Number(btn.getAttribute("data-star"));
        btn.setAttribute("aria-checked", starVal === saved ? "true" : "false");
      });

      if (saved > 0) {
        statusPill.textContent = `Đã đánh giá: ${saved} sao — ${RATING_LABELS[saved]}`;
        statusPill.classList.add("has-rating");
      } else {
        statusPill.textContent = "Chưa đánh giá";
        statusPill.classList.remove("has-rating");
      }
    }

    // Xử lý hover từng sao
    starBtns.forEach((btn) => {
      const starVal = Number(btn.getAttribute("data-star"));

      btn.addEventListener("mouseenter", () => {
        highlightStars(starVal, true);
        statusPill.textContent = `${starVal} sao — ${RATING_LABELS[starVal]}`;
        statusPill.classList.add("has-rating");
      });

      btn.addEventListener("focus", () => {
        highlightStars(starVal, true);
        statusPill.textContent = `${starVal} sao — ${RATING_LABELS[starVal]}`;
      });

      // Xử lý click chọn sao
      btn.addEventListener("click", (e) => {
        e.preventDefault();
        submitRating(starVal);
      });

      // Xử lý phím mũi tên / Enter / Space
      btn.addEventListener("keydown", (e) => {
        if (e.key === "ArrowRight" || e.key === "ArrowUp") {
          e.preventDefault();
          const nextStar = Math.min(5, starVal + 1);
          const nextBtn = starBtns.find((b) => Number(b.getAttribute("data-star")) === nextStar);
          if (nextBtn) nextBtn.focus();
        } else if (e.key === "ArrowLeft" || e.key === "ArrowDown") {
          e.preventDefault();
          const prevStar = Math.max(1, starVal - 1);
          const prevBtn = starBtns.find((b) => Number(b.getAttribute("data-star")) === prevStar);
          if (prevBtn) prevBtn.focus();
        } else if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          submitRating(starVal);
        }
      });
    });

    // Khi chuột rời khỏi cụm sao: khôi phục trạng thái đã lưu
    control.addEventListener("mouseleave", () => {
      resetStarsToSaved();
    });

    // Hàm thực hiện ghi nhận đánh giá
    function submitRating(ratingVal) {
      const result = saveRecipeRating(recipeId, ratingVal);
      if (result.success) {
        // Cập nhật giao diện sao và pill
        resetStarsToSaved();

        // Cập nhật thống kê điểm số ở cột bên trái
        updateRatingOverview(section, recipeId);

        // Cập nhật nhãn prompt
        if (promptEl) {
          promptEl.textContent = "Thay đổi đánh giá của bạn:";
        }

        // Cập nhật thông tin trên Hero Card của Details
        updateHeroRatingDisplay(recipeId);

        // Hiển thị thông báo phản hồi
        showFeedback(section, result.message, "success");
      } else {
        showFeedback(section, result.message, "error");
      }
    }
  }

  /**
   * Cập nhật thông tin điểm số ở cột tóm tắt bên trái card.
   * @param {HTMLElement} section
   * @param {number} recipeId
   */
  function updateRatingOverview(section, recipeId) {
    const stats = getRecipeRatingStatistics(recipeId);
    const scoreHuge = section.querySelector("#ratingScoreHuge");
    const starsVisual = section.querySelector("#ratingStarsVisual");
    const countText = section.querySelector("#ratingCountText");

    if (scoreHuge) {
      if (stats.totalRatings > 0) {
        scoreHuge.textContent = `${stats.averageRating.toFixed(1)}`;
        scoreHuge.classList.remove("empty");
      } else {
        scoreHuge.textContent = "Chưa có đánh giá";
        scoreHuge.classList.add("empty");
      }
    }

    if (starsVisual) {
      starsVisual.innerHTML = renderStarsHtml(stats.averageRating);
    }

    if (countText) {
      countText.textContent =
        stats.totalRatings > 0 ? `${stats.totalRatings} lượt đánh giá` : "Chưa có lượt đánh giá";
    }
  }

  /**
   * Đồng bộ điểm đánh giá lên thẻ tóm tắt Hero phía trên của trang chi tiết.
   * @param {number} recipeId
   */
  function updateHeroRatingDisplay(recipeId) {
    const stats = getRecipeRatingStatistics(recipeId);
    const heroRatingB = document.getElementById("detailsAverageRatingDisplay");
    if (!heroRatingB) return;

    if (stats.totalRatings > 0) {
      heroRatingB.innerHTML = `${stats.averageRating.toFixed(1)} / 5.0 <span style="font-weight: 500; font-size: 13px; color: var(--rc-muted, #617174);">(${stats.totalRatings} đánh giá)</span>`;
    } else {
      heroRatingB.textContent = "Chưa có đánh giá";
    }
  }

  // ==========================================================================
  // 6. KHỞI TẠO MODULE (INITIALIZATION)
  // ==========================================================================

  /**
   * Khởi tạo giao diện Đánh giá công thức cho trang Recipe Details.
   * @param {number|string} recipeId
   */
  function initializeRecipeRatings(recipeId) {
    const id = Number(recipeId);
    if (!id || id <= 0) return;

    // Ngăn chặn tạo trùng lặp
    const existing = document.getElementById("recipeRatingSection");
    if (existing) {
      existing.remove();
    }

    const section = buildRatingSectionNode(id);

    // Vị trí chèn ưu tiên:
    // 1. Trước #recipeCommentSection (nếu comment section đã dựng)
    // 2. Trước .details-footer-nav
    // 3. Cuối #recipeDetailsContainer
    const commentSection = document.getElementById("recipeCommentSection");
    if (commentSection) {
      commentSection.insertAdjacentElement("beforebegin", section);
    } else {
      const footerNav = document.querySelector(".details-footer-nav");
      if (footerNav) {
        footerNav.insertAdjacentElement("beforebegin", section);
      } else {
        const container = document.getElementById("recipeDetailsContainer");
        if (container) {
          container.appendChild(section);
        }
      }
    }

    // Cập nhật hero rating metadata ngay khi khởi tạo
    updateHeroRatingDisplay(id);
  }

  // ==========================================================================
  // 7. LẮNG NGHE SỰ KIỆN STORAGE (CROSS-TAB SYNC)
  // ==========================================================================

  window.addEventListener("storage", function (e) {
    if (e.key === RECIPE_RATINGS_STORAGE_KEY) {
      const detailsRecipeId = document.getElementById("detailsRecipeId");
      if (detailsRecipeId && detailsRecipeId.value) {
        initializeRecipeRatings(Number(detailsRecipeId.value));
      }
    }
  });

  // Tự động khởi tạo nếu đã có sẵn recipeId khi DOM ready
  document.addEventListener("DOMContentLoaded", function () {
    const idInput = document.getElementById("detailsRecipeId");
    if (idInput && idInput.value && !document.getElementById("recipeRatingSection")) {
      initializeRecipeRatings(Number(idInput.value));
    }
  });

  // ==========================================================================
  // 8. PUBLIC API EXPORT
  // ==========================================================================

  window.CookShareRatings = {
    getRecipeRating,
    saveRecipeRating,
    getRecipeRatingStatistics,
    initializeRecipeRatings,
    updateHeroRatingDisplay,
    RATING_LABELS
  };
})();
