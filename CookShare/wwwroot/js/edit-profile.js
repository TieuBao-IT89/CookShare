/**
 * COOKSHARE - EDIT PROFILE MODULE
 * Cho phép demo user chỉnh sửa tên, tiểu sử và ảnh đại diện.
 * Lưu trữ thông tin vào localStorage với key: cookshare_demo_profile_v1
 * Không tác động đến DEMO_USERS trong user-follow.js.
 */

(function () {
  "use strict";

  // ==========================================================================
  // 1. HẰNG SỐ
  // ==========================================================================

  const PROFILE_STORAGE_KEY = "cookshare_demo_profile_v1";
  const CURRENT_USER_ID = "demo-current-user";

  const MAX_NAME_LENGTH = 50;
  const MIN_NAME_LENGTH = 2;
  const MAX_BIO_LENGTH = 300;
  const MAX_AVATAR_SIZE_BYTES = 2 * 1024 * 1024; // 2 MB
  const AVATAR_OUTPUT_SIZE = 512;

  // ==========================================================================
  // 2. ĐỌC / GHI LOCALSTORAGE
  // ==========================================================================

  /**
   * Đọc hồ sơ đã chỉnh sửa từ localStorage.
   * @returns {{ name?: string, bio?: string, avatar?: string|null }|null}
   */
  function loadSavedProfile() {
    try {
      const raw = localStorage.getItem(PROFILE_STORAGE_KEY);
      if (!raw) return null;
      const parsed = JSON.parse(raw);
      if (parsed && typeof parsed === "object") return parsed;
      return null;
    } catch {
      return null;
    }
  }

  /**
   * Lưu hồ sơ vào localStorage.
   * @param {{ name: string, bio: string, avatar: string|null }} profile
   * @returns {boolean}
   */
  function saveProfile(profile) {
    try {
      localStorage.setItem(PROFILE_STORAGE_KEY, JSON.stringify(profile));
      return true;
    } catch (err) {
      console.error("[CookShare EditProfile] Lỗi khi lưu hồ sơ:", err);
      return false;
    }
  }

  /**
   * Lấy thông tin hiện tại của demo user (ưu tiên localStorage, fallback sang DEMO_USERS).
   * @returns {{ id: string, name: string, username: string, bio: string, avatar: string|null, initials?: string }}
   */
  function getCurrentDemoProfile() {
    const base =
      (window.CookShareFollow && window.CookShareFollow.getUserById(CURRENT_USER_ID)) ||
      { id: CURRENT_USER_ID, name: "Nguyễn Hải", username: "haivo200c-lang", bio: "", avatar: null };

    const saved = loadSavedProfile();
    if (!saved) return { ...base };

    return {
      ...base,
      name: typeof saved.name === "string" ? saved.name : base.name,
      bio: typeof saved.bio === "string" ? saved.bio : base.bio,
      avatar: saved.avatar !== undefined ? saved.avatar : base.avatar
    };
  }

  // ==========================================================================
  // 3. XÁC THỰC
  // ==========================================================================

  /**
   * @typedef {{ name: string, bio: string }} ProfileData
   * @param {ProfileData} data
   * @returns {{ nameError: string|null, bioError: string|null }}
   */
  function validateProfileData(data) {
    let nameError = null;
    let bioError = null;

    const name = (data.name || "").trim();
    if (name.length < MIN_NAME_LENGTH) {
      nameError = `Tên hiển thị phải có ít nhất ${MIN_NAME_LENGTH} ký tự.`;
    } else if (name.length > MAX_NAME_LENGTH) {
      nameError = `Tên hiển thị không được quá ${MAX_NAME_LENGTH} ký tự.`;
    }

    const bio = data.bio || "";
    if (bio.length > MAX_BIO_LENGTH) {
      bioError = `Tiểu sử không được quá ${MAX_BIO_LENGTH} ký tự.`;
    }

    return { nameError, bioError };
  }

  // ==========================================================================
  // 4. NÉN ẢNH ĐẠI DIỆN
  // ==========================================================================

  /**
   * Đọc file ảnh và nén về kích thước tối đa AVATAR_OUTPUT_SIZE × AVATAR_OUTPUT_SIZE.
   * @param {File} file
   * @returns {Promise<string>} Data URL (webp hoặc jpeg)
   */
  function compressAvatar(file) {
    return new Promise(function (resolve, reject) {
      if (file.size > MAX_AVATAR_SIZE_BYTES) {
        reject(new Error("Ảnh vượt quá dung lượng cho phép (tối đa 2 MB)."));
        return;
      }

      const allowed = ["image/jpeg", "image/png", "image/webp"];
      if (!allowed.includes(file.type)) {
        reject(new Error("Chỉ chấp nhận ảnh định dạng JPEG, PNG hoặc WebP."));
        return;
      }

      const reader = new FileReader();
      reader.onload = function (e) {
        const img = new Image();
        img.onload = function () {
          const canvas = document.createElement("canvas");
          const size = AVATAR_OUTPUT_SIZE;
          const ratio = Math.min(size / img.width, size / img.height);
          const w = Math.round(img.width * ratio);
          const h = Math.round(img.height * ratio);
          canvas.width = size;
          canvas.height = size;
          const ctx = canvas.getContext("2d");
          ctx.fillStyle = "#edf3eb";
          ctx.fillRect(0, 0, size, size);
          const x = Math.round((size - w) / 2);
          const y = Math.round((size - h) / 2);
          ctx.drawImage(img, x, y, w, h);
          const dataUrl = canvas.toDataURL("image/webp", 0.88) || canvas.toDataURL("image/jpeg", 0.88);
          resolve(dataUrl);
        };
        img.onerror = function () { reject(new Error("Không thể đọc file ảnh.")); };
        img.src = e.target.result;
      };
      reader.onerror = function () { reject(new Error("Không thể đọc file.")); };
      reader.readAsDataURL(file);
    });
  }

  // ==========================================================================
  // 5. CẬP NHẬT UI TRANG PROFILE
  // ==========================================================================

  /**
   * Áp dụng thông tin hồ sơ lên các phần tử của trang.
   * @param {{ name: string, bio: string, avatar: string|null, username?: string }} profile
   */
  function updateProfileUI(profile) {
    const nameEl = document.querySelector(".profile-name");
    const descEl = document.querySelector(".profile-description");
    const avatarEl = document.querySelector(".profile-avatar");

    if (nameEl) nameEl.textContent = profile.name;
    if (descEl) descEl.textContent = profile.bio;

    if (avatarEl) {
      if (profile.avatar) {
        avatarEl.src = profile.avatar;
        avatarEl.alt = "Ảnh đại diện " + profile.name;
      } else {
        // Tạo SVG fallback với chữ cái đầu
        const initials = getInitials(profile.name);
        avatarEl.src =
          "data:image/svg+xml," +
          encodeURIComponent(
            '<svg xmlns="http://www.w3.org/2000/svg" width="240" height="240">' +
            '<rect width="240" height="240" fill="#e6eddf"/>' +
            '<text x="120" y="145" text-anchor="middle" font-family="sans-serif" font-size="72" fill="#245c43">' +
            escapeHtml(initials) +
            "</text></svg>"
          );
        avatarEl.alt = "Ảnh đại diện " + profile.name;
      }
    }

    // Cập nhật user cards trong tab Followers / Following (nếu tồn tại)
    const selfCards = document.querySelectorAll(`.user-card[data-user-id="${CURRENT_USER_ID}"]`);
    selfCards.forEach(function (card) {
      const nameLink = card.querySelector(".user-card-name a");
      if (nameLink) nameLink.textContent = profile.name;
      const avatarImg = card.querySelector(".user-card-avatar");
      if (avatarImg && avatarImg.tagName === "IMG") {
        if (profile.avatar) {
          avatarImg.src = profile.avatar;
          avatarImg.alt = profile.name;
        }
      }
      const bioEl = card.querySelector(".user-card-bio");
      if (bioEl) bioEl.textContent = profile.bio;
    });
  }

  // ==========================================================================
  // 6. HELPERS
  // ==========================================================================

  function escapeHtml(text) {
    if (!text) return "";
    return String(text)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  function getInitials(name) {
    if (!name) return "CS";
    const parts = name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  /**
   * Kiểm tra xem đây có phải trang Profile của chính mình không.
   * Trả về true nếu URL không chứa ?userId=... hoặc userId trùng với CURRENT_USER_ID.
   */
  function isOwnProfilePage() {
    const params = new URLSearchParams(window.location.search);
    const userId = params.get("userId");
    return !userId || userId === CURRENT_USER_ID;
  }

  // ==========================================================================
  // 7. TOAST THÔNG BÁO
  // ==========================================================================

  let toastTimeout = null;

  /**
   * Hiển thị toast thành công.
   * @param {string} message
   */
  function showSuccessToast(message) {
    let toast = document.getElementById("epSuccessToast");
    if (!toast) {
      toast = document.createElement("div");
      toast.id = "epSuccessToast";
      toast.className = "ep-toast";
      toast.setAttribute("role", "status");
      toast.setAttribute("aria-live", "polite");
      document.body.appendChild(toast);
    }

    toast.innerHTML =
      '<svg class="icon" aria-hidden="true"><use href="#i-check" /></svg>' +
      escapeHtml(message);

    clearTimeout(toastTimeout);
    toast.classList.add("ep-toast-show");
    toastTimeout = setTimeout(function () {
      toast.classList.remove("ep-toast-show");
    }, 3200);
  }

  // ==========================================================================
  // 8. MODAL - TẠO HTML
  // ==========================================================================

  function buildModalHTML() {
    return `
<div id="editProfileModal" role="dialog" aria-modal="true" aria-labelledby="epModalTitle" hidden>
  <div class="ep-dialog">
    <div class="ep-header">
      <h2 id="epModalTitle">Chỉnh sửa hồ sơ</h2>
      <button type="button" class="ep-close-btn" id="epCloseBtn" aria-label="Đóng">
        <svg class="icon" aria-hidden="true"><use href="#i-close" /></svg>
      </button>
    </div>
    <div class="ep-body">
      <!-- Avatar -->
      <div class="ep-avatar-section">
        <div class="ep-avatar-preview" id="epAvatarPreview" title="Nhấn để chọn ảnh" tabindex="0" role="button" aria-label="Chọn ảnh đại diện">
          <img id="epAvatarImg" src="" alt="Ảnh đại diện xem trước" />
          <div class="ep-avatar-overlay" aria-hidden="true">
            <svg class="icon"><use href="#i-camera" /></svg>
          </div>
          <div class="ep-avatar-fallback" id="epAvatarFallback" style="display:none"></div>
        </div>
        <div class="ep-avatar-info">
          <button type="button" class="ep-avatar-upload-btn" id="epAvatarUploadBtn">
            <svg class="icon" aria-hidden="true"><use href="#i-image" /></svg>
            Chọn ảnh
          </button>
          <p>JPEG, PNG hoặc WebP · Tối đa 2 MB</p>
          <div class="ep-avatar-error" id="epAvatarError" role="alert"></div>
        </div>
      </div>
      <input type="file" id="epAvatarInput" accept="image/jpeg,image/png,image/webp" aria-label="Tải lên ảnh đại diện" />

      <!-- Tên hiển thị -->
      <div class="ep-field">
        <label for="epNameInput">Tên hiển thị <span class="ep-required" aria-hidden="true">*</span></label>
        <input
          type="text"
          id="epNameInput"
          class="ep-input"
          maxlength="${MAX_NAME_LENGTH}"
          autocomplete="name"
          placeholder="Nhập tên hiển thị của bạn"
          aria-describedby="epNameError epNameCount"
          aria-required="true"
        />
        <div class="ep-field-footer">
          <span class="ep-error-msg" id="epNameError" role="alert"></span>
          <span class="ep-char-count" id="epNameCount">0 / ${MAX_NAME_LENGTH}</span>
        </div>
      </div>

      <!-- Tiểu sử -->
      <div class="ep-field">
        <label for="epBioTextarea">Tiểu sử</label>
        <textarea
          id="epBioTextarea"
          class="ep-textarea"
          maxlength="${MAX_BIO_LENGTH}"
          placeholder="Chia sẻ đôi điều về bản thân..."
          rows="4"
          aria-describedby="epBioError epBioCount"
        ></textarea>
        <div class="ep-field-footer">
          <span class="ep-error-msg" id="epBioError" role="alert"></span>
          <span class="ep-char-count" id="epBioCount">0 / ${MAX_BIO_LENGTH}</span>
        </div>
      </div>
    </div>
    <div class="ep-footer">
      <button type="button" class="ep-btn-cancel" id="epCancelBtn">Huỷ</button>
      <button type="button" class="ep-btn-save" id="epSaveBtn">
        <svg class="icon" aria-hidden="true"><use href="#i-check" /></svg>
        Lưu thay đổi
      </button>
    </div>
  </div>
</div>`;
  }

  // ==========================================================================
  // 9. MODAL - TRẠNG THÁI
  // ==========================================================================

  let pendingAvatarDataUrl = undefined; // undefined = không thay đổi, null = xóa, string = data URL mới
  let previouslyFocused = null;

  /** Điền dữ liệu hiện tại vào form */
  function prefillForm() {
    const profile = getCurrentDemoProfile();

    const nameInput = document.getElementById("epNameInput");
    const bioTextarea = document.getElementById("epBioTextarea");
    const avatarImg = document.getElementById("epAvatarImg");
    const avatarFallback = document.getElementById("epAvatarFallback");

    if (nameInput) {
      nameInput.value = profile.name || "";
      updateCharCount("epNameInput", "epNameCount", MAX_NAME_LENGTH);
    }
    if (bioTextarea) {
      bioTextarea.value = profile.bio || "";
      updateCharCount("epBioTextarea", "epBioCount", MAX_BIO_LENGTH);
    }

    // Avatar preview
    pendingAvatarDataUrl = undefined;
    setAvatarPreview(profile.avatar || null, profile.name);

    // Xóa lỗi
    clearFieldError("epNameInput", "epNameError");
    clearFieldError("epBioTextarea", "epBioError");
    hideAvatarError();
  }

  /**
   * Cập nhật avatar preview trong modal.
   * @param {string|null} avatarSrc - data URL hoặc URL của ảnh, null để dùng fallback
   * @param {string} userName
   */
  function setAvatarPreview(avatarSrc, userName) {
    const avatarImg = document.getElementById("epAvatarImg");
    const avatarFallback = document.getElementById("epAvatarFallback");

    if (avatarImg && avatarFallback) {
      if (avatarSrc) {
        avatarImg.src = avatarSrc;
        avatarImg.style.display = "block";
        avatarFallback.style.display = "none";
      } else {
        avatarImg.src = "";
        avatarImg.style.display = "none";
        avatarFallback.textContent = getInitials(userName || "CS");
        avatarFallback.style.display = "flex";
      }
    }
  }

  /** Hiển thị modal */
  function openEditModal() {
    if (!isOwnProfilePage()) return;

    const modal = document.getElementById("editProfileModal");
    if (!modal) return;

    prefillForm();
    previouslyFocused = document.activeElement;
    modal.removeAttribute("hidden");
    document.body.style.overflow = "hidden";

    // Focus vào input đầu tiên
    const nameInput = document.getElementById("epNameInput");
    if (nameInput) {
      requestAnimationFrame(function () { nameInput.focus(); });
    }
  }

  /** Đóng modal (không lưu) */
  function closeEditModal() {
    const modal = document.getElementById("editProfileModal");
    if (!modal) return;

    modal.setAttribute("hidden", "");
    document.body.style.overflow = "";
    pendingAvatarDataUrl = undefined;

    if (previouslyFocused && typeof previouslyFocused.focus === "function") {
      previouslyFocused.focus();
    }
  }

  // ==========================================================================
  // 10. CHAR COUNTER & VALIDATION UI
  // ==========================================================================

  function updateCharCount(inputId, countId, max) {
    const input = document.getElementById(inputId);
    const counter = document.getElementById(countId);
    if (!input || !counter) return;
    const len = input.value.length;
    counter.textContent = len + " / " + max;
    if (len > max) {
      counter.classList.add("ep-over-limit");
    } else {
      counter.classList.remove("ep-over-limit");
    }
  }

  function showFieldError(inputId, errorId, message) {
    const input = document.getElementById(inputId);
    const errorEl = document.getElementById(errorId);
    if (input) input.classList.add("ep-invalid");
    if (errorEl) {
      errorEl.textContent = message;
      errorEl.classList.add("ep-visible");
    }
  }

  function clearFieldError(inputId, errorId) {
    const input = document.getElementById(inputId);
    const errorEl = document.getElementById(errorId);
    if (input) input.classList.remove("ep-invalid");
    if (errorEl) {
      errorEl.textContent = "";
      errorEl.classList.remove("ep-visible");
    }
  }

  function showAvatarError(message) {
    const el = document.getElementById("epAvatarError");
    if (el) {
      el.textContent = message;
      el.classList.add("ep-visible");
    }
  }

  function hideAvatarError() {
    const el = document.getElementById("epAvatarError");
    if (el) {
      el.textContent = "";
      el.classList.remove("ep-visible");
    }
  }

  // ==========================================================================
  // 11. LƯU HỒ SƠ
  // ==========================================================================

  function handleSave() {
    const nameInput = document.getElementById("epNameInput");
    const bioTextarea = document.getElementById("epBioTextarea");
    const saveBtn = document.getElementById("epSaveBtn");

    const nameVal = nameInput ? nameInput.value : "";
    const bioVal = bioTextarea ? bioTextarea.value : "";

    const { nameError, bioError } = validateProfileData({ name: nameVal, bio: bioVal });

    clearFieldError("epNameInput", "epNameError");
    clearFieldError("epBioTextarea", "epBioError");

    let hasError = false;
    if (nameError) {
      showFieldError("epNameInput", "epNameError", nameError);
      hasError = true;
    }
    if (bioError) {
      showFieldError("epBioTextarea", "epBioError", bioError);
      hasError = true;
    }

    if (hasError) {
      const firstInvalid = document.querySelector(".ep-invalid");
      if (firstInvalid) firstInvalid.focus();
      return;
    }

    // Xác định avatar mới
    const currentProfile = getCurrentDemoProfile();
    let newAvatar = currentProfile.avatar || null;
    if (pendingAvatarDataUrl !== undefined) {
      newAvatar = pendingAvatarDataUrl; // null hoặc data URL
    }

    const newProfile = {
      name: nameVal.trim(),
      bio: bioVal,
      avatar: newAvatar
    };

    if (saveBtn) {
      saveBtn.disabled = true;
      saveBtn.textContent = "Đang lưu...";
    }

    const success = saveProfile(newProfile);

    if (saveBtn) {
      saveBtn.disabled = false;
      saveBtn.innerHTML =
        '<svg class="icon" aria-hidden="true"><use href="#i-check" /></svg> Lưu thay đổi';
    }

    if (success) {
      closeEditModal();
      updateProfileUI({ ...currentProfile, ...newProfile });
      showSuccessToast("Hồ sơ đã được cập nhật thành công!");
    } else {
      showFieldError("epNameInput", "epNameError", "Không thể lưu. Dung lượng trình duyệt có thể đầy.");
    }
  }

  // ==========================================================================
  // 12. FOCUS TRAP
  // ==========================================================================

  function trapFocus(e) {
    const modal = document.getElementById("editProfileModal");
    if (!modal || modal.hasAttribute("hidden")) return;

    const focusable = modal.querySelectorAll(
      'button:not([disabled]), input:not([disabled]), textarea:not([disabled]), [tabindex="0"]'
    );
    const first = focusable[0];
    const last = focusable[focusable.length - 1];

    if (e.key === "Tab") {
      if (e.shiftKey) {
        if (document.activeElement === first) {
          e.preventDefault();
          last.focus();
        }
      } else {
        if (document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    }

    if (e.key === "Escape") {
      closeEditModal();
    }
  }

  // ==========================================================================
  // 13. KHỞI TẠO
  // ==========================================================================

  function initializeEditProfile() {
    // Chỉ hoạt động trên trang Profile
    if (!document.querySelector(".profile-page")) return;

    // Chèn modal HTML vào cuối body
    const modalWrapper = document.createElement("div");
    modalWrapper.innerHTML = buildModalHTML();
    document.body.appendChild(modalWrapper.firstElementChild);

    // Tạo nút "Chỉnh sửa hồ sơ" chỉ khi xem profile của chính mình
    if (isOwnProfilePage()) {
      insertEditProfileButton();
    }

    // Áp dụng profile đã lưu (nếu có) lên header trang
    const savedProfile = loadSavedProfile();
    if (savedProfile) {
      const base = getCurrentDemoProfile();
      updateProfileUI(base);
    }

    // Gắn sự kiện modal
    bindModalEvents();
  }

  /** Tạo và chèn nút Chỉnh sửa hồ sơ vào header profile */
  function insertEditProfileButton() {
    // Kiểm tra đã có nút chưa
    if (document.getElementById("epOpenModalBtn")) return;

    const profileHeader = document.querySelector(".profile-header");
    if (!profileHeader) return;

    const btn = document.createElement("button");
    btn.type = "button";
    btn.id = "epOpenModalBtn";
    btn.className = "btn-edit-profile";
    btn.setAttribute("aria-label", "Mở form chỉnh sửa hồ sơ");
    btn.innerHTML =
      '<svg class="icon" aria-hidden="true"><use href="#i-edit" /></svg>' +
      " Chỉnh sửa hồ sơ";

    // Chèn vào bên cạnh nút "Chia sẻ món mới"
    const shareBtn = profileHeader.querySelector("a.btn");
    if (shareBtn) {
      // Bọc cả hai nút trong flex container nếu chưa bọc
      let actionsGroup = profileHeader.querySelector(".profile-header-actions");
      if (!actionsGroup) {
        actionsGroup = document.createElement("div");
        actionsGroup.className = "profile-header-actions";
        actionsGroup.style.cssText = "display:flex;gap:10px;align-items:center;flex-wrap:wrap;";
        shareBtn.parentNode.insertBefore(actionsGroup, shareBtn);
        actionsGroup.appendChild(shareBtn);
      }
      actionsGroup.appendChild(btn);
    } else {
      profileHeader.appendChild(btn);
    }
  }

  function bindModalEvents() {
    // Nút mở
    document.addEventListener("click", function (e) {
      if (e.target.closest("#epOpenModalBtn")) {
        openEditModal();
      }
    });

    // Nút đóng (X)
    const closeBtn = document.getElementById("epCloseBtn");
    if (closeBtn) closeBtn.addEventListener("click", closeEditModal);

    // Nút Huỷ
    const cancelBtn = document.getElementById("epCancelBtn");
    if (cancelBtn) cancelBtn.addEventListener("click", closeEditModal);

    // Nút Lưu
    const saveBtn = document.getElementById("epSaveBtn");
    if (saveBtn) saveBtn.addEventListener("click", handleSave);

    // Click overlay để đóng
    const modal = document.getElementById("editProfileModal");
    if (modal) {
      modal.addEventListener("click", function (e) {
        if (e.target === modal) closeEditModal();
      });
    }

    // Focus trap & Escape
    document.addEventListener("keydown", trapFocus);

    // Char counters
    const nameInput = document.getElementById("epNameInput");
    if (nameInput) {
      nameInput.addEventListener("input", function () {
        clearFieldError("epNameInput", "epNameError");
        updateCharCount("epNameInput", "epNameCount", MAX_NAME_LENGTH);
      });
    }

    const bioTextarea = document.getElementById("epBioTextarea");
    if (bioTextarea) {
      bioTextarea.addEventListener("input", function () {
        clearFieldError("epBioTextarea", "epBioError");
        updateCharCount("epBioTextarea", "epBioCount", MAX_BIO_LENGTH);
      });
    }

    // Upload ảnh qua nút / click preview
    const avatarInput = document.getElementById("epAvatarInput");
    const avatarUploadBtn = document.getElementById("epAvatarUploadBtn");
    const avatarPreview = document.getElementById("epAvatarPreview");

    if (avatarUploadBtn && avatarInput) {
      avatarUploadBtn.addEventListener("click", function () { avatarInput.click(); });
    }

    if (avatarPreview && avatarInput) {
      avatarPreview.addEventListener("click", function () { avatarInput.click(); });
      avatarPreview.addEventListener("keydown", function (e) {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          avatarInput.click();
        }
      });
    }

    if (avatarInput) {
      avatarInput.addEventListener("change", function () {
        const file = avatarInput.files && avatarInput.files[0];
        if (!file) return;

        hideAvatarError();
        compressAvatar(file)
          .then(function (dataUrl) {
            pendingAvatarDataUrl = dataUrl;
            const nameInput = document.getElementById("epNameInput");
            setAvatarPreview(dataUrl, nameInput ? nameInput.value : "");
          })
          .catch(function (err) {
            showAvatarError(err.message || "Không thể xử lý ảnh.");
            avatarInput.value = "";
          });
      });
    }
  }

  // ==========================================================================
  // 14. KHỞI CHẠY KHI DOM SẴN SÀNG
  // ==========================================================================

  document.addEventListener("DOMContentLoaded", function () {
    // Đợi user-follow.js chạy trước (nó cũng lắng nghe DOMContentLoaded)
    // Sử dụng setTimeout 0 để đảm bảo thứ tự thực thi sau user-follow.js
    setTimeout(function () {
      initializeEditProfile();
    }, 0);
  });

  // ==========================================================================
  // 15. PUBLIC API
  // ==========================================================================

  window.CookShareEditProfile = {
    getCurrentDemoProfile: getCurrentDemoProfile,
    updateProfileUI: updateProfileUI,
    openEditModal: openEditModal,
    closeEditModal: closeEditModal
  };
})();
