/**
 * COOKSHARE – RECIPE COMMENTS MODULE
 * Quản lý bình luận công thức nấu ăn bằng localStorage
 *
 * Đây là tính năng demo frontend; việc kiểm tra quyền sở hữu bình luận
 * dựa trên ID demo và KHÔNG có giá trị bảo mật thực sự.
 * Cần tích hợp xác thực thật ở backend khi triển khai production.
 */

// ==========================================================================
// CONSTANTS
// ==========================================================================

const COMMENT_STORAGE_KEY = "cookshare_recipe_comments_v1";
const COMMENT_MAX_LENGTH = 500;
const COMMENT_WARNING_THRESHOLD = 420; // Bắt đầu hiện cảnh báo

/** Demo user – thay bằng thông tin người dùng thực khi có authentication */
const CURRENT_DEMO_USER = {
  id: "demo-user-1",
  name: "Người dùng CookShare",
  avatar: null
};

// ==========================================================================
// UNIQUE ID GENERATOR
// ==========================================================================

/**
 * Tạo ID duy nhất cho bình luận.
 * Ưu tiên dùng crypto.randomUUID() khi có sẵn; fallback sang timestamp + random.
 * @returns {string}
 */
function generateCommentId() {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  // Fallback đơn giản: timestamp + random hex
  return (
    Date.now().toString(36) +
    "-" +
    Math.random().toString(36).slice(2, 10)
  );
}

// ==========================================================================
// LOCALSTORAGE UTILITIES
// ==========================================================================

/**
 * Đọc toàn bộ dữ liệu bình luận từ localStorage.
 * Trả về mảng rỗng nếu không có dữ liệu hoặc dữ liệu bị hỏng.
 * @returns {Array<Object>}
 */
function loadAllComments() {
  try {
    const raw = localStorage.getItem(COMMENT_STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
  } catch (err) {
    console.warn("[CookShare Comments] Lỗi đọc dữ liệu bình luận:", err);
    return [];
  }
}

/**
 * Ghi toàn bộ dữ liệu bình luận vào localStorage.
 * @param {Array<Object>} comments
 * @returns {boolean} true nếu ghi thành công
 */
function saveAllComments(comments) {
  try {
    localStorage.setItem(COMMENT_STORAGE_KEY, JSON.stringify(comments));
    return true;
  } catch (err) {
    console.error("[CookShare Comments] Lỗi ghi dữ liệu bình luận:", err);
    return false;
  }
}

/**
 * Lấy danh sách bình luận thuộc về một recipeId cụ thể.
 * Sắp xếp theo thời gian mới nhất trước.
 * @param {number} recipeId
 * @returns {Array<Object>}
 */
function getCommentsByRecipeId(recipeId) {
  const all = loadAllComments();
  return all
    .filter(
      (c) =>
        c &&
        typeof c === "object" &&
        Number(c.recipeId) === Number(recipeId) &&
        typeof c.id === "string" &&
        typeof c.content === "string"
    )
    .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
}

/**
 * Thêm bình luận mới vào localStorage.
 * @param {Object} comment
 * @returns {boolean} true nếu thành công
 */
function addCommentToStorage(comment) {
  const all = loadAllComments();
  // Ngăn trùng lặp ID
  if (all.some((c) => c.id === comment.id)) return false;
  all.push(comment);
  return saveAllComments(all);
}

/**
 * Cập nhật nội dung bình luận trong localStorage.
 * Chỉ cập nhật bình luận của CURRENT_DEMO_USER.
 * @param {string} commentId
 * @param {string} newContent
 * @returns {boolean}
 */
function updateCommentInStorage(commentId, newContent) {
  const all = loadAllComments();
  const idx = all.findIndex(
    (c) => c.id === commentId && c.userId === CURRENT_DEMO_USER.id
  );
  if (idx === -1) return false;
  all[idx] = {
    ...all[idx],
    content: newContent,
    updatedAt: new Date().toISOString()
  };
  return saveAllComments(all);
}

/**
 * Xóa bình luận khỏi localStorage.
 * Chỉ xóa bình luận của CURRENT_DEMO_USER.
 * @param {string} commentId
 * @returns {boolean}
 */
function deleteCommentFromStorage(commentId) {
  const all = loadAllComments();
  const filtered = all.filter(
    (c) => !(c.id === commentId && c.userId === CURRENT_DEMO_USER.id)
  );
  if (filtered.length === all.length) return false; // Không tìm thấy
  return saveAllComments(filtered);
}

// ==========================================================================
// VALIDATION
// ==========================================================================

/**
 * Kiểm tra nội dung bình luận hợp lệ.
 * @param {string} content
 * @returns {{ valid: boolean, message: string }}
 */
function validateCommentContent(content) {
  if (!content || content.trim().length === 0) {
    return { valid: false, message: "⚠ Nội dung bình luận không được để trống." };
  }
  if (content.trim().length > COMMENT_MAX_LENGTH) {
    return {
      valid: false,
      message: `⚠ Bình luận không được vượt quá ${COMMENT_MAX_LENGTH} ký tự.`
    };
  }
  return { valid: true, message: "" };
}

// ==========================================================================
// DATE FORMATTING
// ==========================================================================

/**
 * Định dạng thời gian theo kiểu Việt Nam thân thiện.
 * @param {string} isoString
 * @returns {string}
 */
function formatCommentDate(isoString) {
  if (!isoString) return "";
  try {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) return "";

    const now = new Date();
    const diffMs = now - date;
    const diffSeconds = Math.floor(diffMs / 1000);
    const diffMinutes = Math.floor(diffSeconds / 60);
    const diffHours = Math.floor(diffMinutes / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffSeconds < 60) return "Vừa xong";
    if (diffMinutes < 60) return `${diffMinutes} phút trước`;
    if (diffHours < 24) return `${diffHours} giờ trước`;
    if (diffDays === 1) return "Hôm qua";
    if (diffDays < 7) return `${diffDays} ngày trước`;

    // Ngày cụ thể
    return date.toLocaleDateString("vi-VN", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit"
    });
  } catch {
    return "";
  }
}

// ==========================================================================
// TOAST NOTIFICATION
// ==========================================================================

let _toastTimer = null;

/**
 * Hiển thị thông báo toast nhẹ.
 * @param {string} message
 * @param {'success'|'error'|'info'} type
 * @param {number} duration  ms
 */
function showCommentToast(message, type = "success", duration = 3000) {
  let toast = document.getElementById("commentToast");
  if (!toast) {
    toast = document.createElement("div");
    toast.id = "commentToast";
    toast.className = "comment-toast";
    toast.setAttribute("role", "status");
    toast.setAttribute("aria-live", "polite");
    document.body.appendChild(toast);
  }

  // Reset class
  toast.className = "comment-toast comment-toast--" + type;
  toast.textContent = message;

  // Hiển thị
  requestAnimationFrame(() => {
    toast.classList.add("comment-toast--visible");
  });

  // Tự động ẩn
  if (_toastTimer) clearTimeout(_toastTimer);
  _toastTimer = setTimeout(() => {
    toast.classList.remove("comment-toast--visible");
  }, duration);
}

// ==========================================================================
// ARIA LIVE STATUS
// ==========================================================================

/**
 * Thông báo cho screen reader qua vùng aria-live.
 * @param {string} message
 */
function announceToScreenReader(message) {
  const liveRegion = document.getElementById("commentSrLive");
  if (!liveRegion) return;
  liveRegion.textContent = "";
  // setTimeout ngắn để đảm bảo screen reader đọc lại
  setTimeout(() => {
    liveRegion.textContent = message;
  }, 50);
}

// ==========================================================================
// AVATAR RENDERING
// ==========================================================================

/**
 * Tạo nội dung avatar (initials hoặc hình ảnh).
 * @param {Object} user  { name, avatar }
 * @returns {string} HTML safe string (dùng textContent cho tên)
 */
function buildAvatarContent(user) {
  if (user.avatar) {
    const img = document.createElement("img");
    img.src = user.avatar;
    img.alt = user.name || "Avatar";
    return img.outerHTML;
  }
  // Lấy 2 chữ cái đầu tên
  const name = (user.name || "?").trim();
  const initials = name
    .split(" ")
    .filter(Boolean)
    .slice(-2)
    .map((w) => w[0].toUpperCase())
    .join("");
  return initials || "?";
}

// ==========================================================================
// CHARACTER COUNTER HELPER
// ==========================================================================

/**
 * Cập nhật giao diện bộ đếm ký tự.
 * @param {HTMLElement} counterEl
 * @param {number} currentLength
 */
function updateCharCounter(counterEl, currentLength) {
  if (!counterEl) return;
  const remaining = COMMENT_MAX_LENGTH - currentLength;
  counterEl.textContent = `${currentLength} / ${COMMENT_MAX_LENGTH}`;
  counterEl.className = "comment-char-counter";

  if (remaining <= 0) {
    counterEl.classList.add("comment-char-counter--danger");
  } else if (currentLength >= COMMENT_WARNING_THRESHOLD) {
    counterEl.classList.add("comment-char-counter--warning");
  }
}

// ==========================================================================
// RENDER: SINGLE COMMENT ITEM
// ==========================================================================

/**
 * Tạo DOM element cho một bình luận.
 * Tất cả nội dung người dùng được thiết lập qua textContent (không dùng innerHTML).
 * @param {Object} comment
 * @param {Function} onEdit   callback(commentId)
 * @param {Function} onDelete callback(commentId)
 * @returns {HTMLElement}
 */
function createCommentElement(comment, onEdit, onDelete) {
  const isOwn = comment.userId === CURRENT_DEMO_USER.id;

  const item = document.createElement("div");
  item.className = "comment-item" + (isOwn ? " comment-item--own" : "");
  item.dataset.commentId = comment.id;

  // Avatar
  const avatarEl = document.createElement("div");
  avatarEl.className = "comment-avatar comment-avatar--small";
  avatarEl.setAttribute("aria-hidden", "true");
  avatarEl.innerHTML = buildAvatarContent({
    name: comment.userName,
    avatar: comment.userAvatar
  });

  // Body
  const bodyEl = document.createElement("div");
  bodyEl.className = "comment-body";

  // Header row
  const headerRow = document.createElement("div");
  headerRow.className = "comment-header-row";

  const authorEl = document.createElement("span");
  authorEl.className = "comment-author-name";
  authorEl.textContent = comment.userName || "Người dùng";

  const timeEl = document.createElement("time");
  timeEl.className = "comment-timestamp";
  timeEl.setAttribute("datetime", comment.createdAt || "");
  timeEl.textContent = formatCommentDate(comment.createdAt);

  headerRow.appendChild(authorEl);
  headerRow.appendChild(timeEl);

  if (comment.updatedAt) {
    const editedTag = document.createElement("span");
    editedTag.className = "comment-edited-tag";
    editedTag.textContent = "(đã chỉnh sửa)";
    headerRow.appendChild(editedTag);
  }

  // Content (an toàn: dùng textContent)
  const contentEl = document.createElement("p");
  contentEl.className = "comment-content-text";
  contentEl.textContent = comment.content;

  bodyEl.appendChild(headerRow);
  bodyEl.appendChild(contentEl);

  // Nút Edit / Delete chỉ hiện với bình luận của người dùng demo
  if (isOwn) {
    const actionsRow = document.createElement("div");
    actionsRow.className = "comment-actions-row";

    const editBtn = document.createElement("button");
    editBtn.type = "button";
    editBtn.className = "btn-comment-action btn-comment-edit";
    editBtn.setAttribute("aria-label", `Sửa bình luận của ${comment.userName}`);
    editBtn.textContent = "✏ Sửa";
    editBtn.addEventListener("click", () => onEdit(comment.id));

    const deleteBtn = document.createElement("button");
    deleteBtn.type = "button";
    deleteBtn.className = "btn-comment-action btn-comment-delete";
    deleteBtn.setAttribute("aria-label", `Xóa bình luận của ${comment.userName}`);
    deleteBtn.textContent = "🗑 Xóa";
    deleteBtn.addEventListener("click", () => onDelete(comment.id));

    actionsRow.appendChild(editBtn);
    actionsRow.appendChild(deleteBtn);
    bodyEl.appendChild(actionsRow);
  }

  item.appendChild(avatarEl);
  item.appendChild(bodyEl);

  return item;
}

// ==========================================================================
// INLINE EDIT FORM
// ==========================================================================

/**
 * Xây dựng form chỉnh sửa nội tuyến bên trong item.
 * @param {Object} comment
 * @param {HTMLElement} bodyEl   – phần thân bình luận
 * @param {Function} onSave     – callback(commentId, newContent)
 * @param {Function} onCancel   – callback()
 */
function buildInlineEditForm(comment, bodyEl, onSave, onCancel) {
  // Ẩn nội dung hiện tại
  const existingContent = bodyEl.querySelector(".comment-content-text");
  const existingActions = bodyEl.querySelector(".comment-actions-row");
  if (existingContent) existingContent.style.display = "none";
  if (existingActions) existingActions.style.display = "none";

  const editForm = document.createElement("div");
  editForm.className = "comment-edit-form";
  editForm.setAttribute("data-edit-form", comment.id);

  // Textarea
  const label = document.createElement("label");
  label.setAttribute("for", `editTextarea_${comment.id}`);
  label.className = "comment-sr-live"; // label ẩn, nhưng dùng cho a11y
  label.textContent = "Chỉnh sửa nội dung bình luận";

  const textarea = document.createElement("textarea");
  textarea.id = `editTextarea_${comment.id}`;
  textarea.className = "comment-edit-textarea";
  textarea.setAttribute("maxlength", String(COMMENT_MAX_LENGTH));
  textarea.setAttribute("aria-label", "Chỉnh sửa nội dung bình luận");
  textarea.value = comment.content;

  // Character counter
  const charCounter = document.createElement("div");
  charCounter.className = "comment-edit-char-counter";
  updateCharCounter(charCounter, comment.content.length);

  textarea.addEventListener("input", () => {
    updateCharCounter(charCounter, textarea.value.length);
  });

  // Action buttons
  const actionsDiv = document.createElement("div");
  actionsDiv.className = "comment-edit-actions";

  const cancelBtn = document.createElement("button");
  cancelBtn.type = "button";
  cancelBtn.className = "btn-edit-cancel";
  cancelBtn.textContent = "Hủy";
  cancelBtn.addEventListener("click", () => {
    editForm.remove();
    if (existingContent) existingContent.style.display = "";
    if (existingActions) existingActions.style.display = "";
    onCancel();
  });

  const saveBtn = document.createElement("button");
  saveBtn.type = "button";
  saveBtn.className = "btn-edit-save";
  saveBtn.textContent = "💾 Lưu";
  saveBtn.addEventListener("click", () => {
    const newContent = textarea.value;
    const validation = validateCommentContent(newContent);
    if (!validation.valid) {
      showCommentToast(validation.message, "error");
      textarea.focus();
      return;
    }
    saveBtn.disabled = true;
    saveBtn.textContent = "Đang lưu...";
    onSave(comment.id, newContent.trim());
  });

  actionsDiv.appendChild(cancelBtn);
  actionsDiv.appendChild(saveBtn);

  editForm.appendChild(label);
  editForm.appendChild(textarea);
  editForm.appendChild(charCounter);
  editForm.appendChild(actionsDiv);

  bodyEl.appendChild(editForm);

  // Focus vào textarea
  setTimeout(() => textarea.focus(), 50);
}

// ==========================================================================
// COMMENT COUNT BADGE UPDATE
// ==========================================================================

/**
 * Cập nhật badge hiển thị số lượng bình luận trong tiêu đề section.
 * @param {number} count
 */
function updateCommentCountBadge(count) {
  const badge = document.getElementById("commentCountBadge");
  if (badge) badge.textContent = String(count);
}

// ==========================================================================
// RENDER: COMMENT LIST
// ==========================================================================

/**
 * Render toàn bộ danh sách bình luận vào container.
 * @param {number} recipeId
 * @param {HTMLElement} listContainer
 */
function renderCommentList(recipeId, listContainer) {
  // Xóa nội dung cũ
  listContainer.innerHTML = "";

  const comments = getCommentsByRecipeId(recipeId);
  updateCommentCountBadge(comments.length);

  if (comments.length === 0) {
    const emptyEl = document.createElement("div");
    emptyEl.className = "comment-empty-state";
    emptyEl.setAttribute("id", "commentEmptyState");

    const icon = document.createElement("div");
    icon.className = "comment-empty-state-icon";
    icon.setAttribute("aria-hidden", "true");
    icon.textContent = "💬";

    const text = document.createElement("p");
    text.className = "comment-empty-state-text";
    text.textContent =
      "Chưa có bình luận nào. Hãy là người đầu tiên chia sẻ cảm nhận về món ăn này!";

    emptyEl.appendChild(icon);
    emptyEl.appendChild(text);
    listContainer.appendChild(emptyEl);
    return;
  }

  // Xóa empty state nếu đang hiển thị
  const emptyEl = listContainer.querySelector("#commentEmptyState");
  if (emptyEl) emptyEl.remove();

  comments.forEach((comment) => {
    const itemEl = createCommentElement(
      comment,
      /* onEdit */ (commentId) => handleEditComment(commentId, recipeId, listContainer),
      /* onDelete */ (commentId) => handleDeleteComment(commentId, recipeId, listContainer)
    );
    listContainer.appendChild(itemEl);
  });
}

// ==========================================================================
// ACTION HANDLERS
// ==========================================================================

/**
 * Xử lý thêm bình luận mới.
 * @param {number} recipeId
 * @param {HTMLElement} listContainer
 * @param {HTMLTextAreaElement} textarea
 * @param {HTMLElement} charCounter
 * @param {HTMLElement} validationMsg
 * @param {HTMLButtonElement} submitBtn
 */
function handleAddComment(
  recipeId,
  listContainer,
  textarea,
  charCounter,
  validationMsg,
  submitBtn
) {
  const rawContent = textarea.value;
  const validation = validateCommentContent(rawContent);

  // Ẩn thông báo cũ
  validationMsg.textContent = "";
  validationMsg.classList.remove("comment-validation-msg--visible");

  if (!validation.valid) {
    validationMsg.textContent = validation.message;
    validationMsg.classList.add("comment-validation-msg--visible");
    textarea.focus();
    return;
  }

  const newComment = {
    id: generateCommentId(),
    recipeId: Number(recipeId),
    userId: CURRENT_DEMO_USER.id,
    userName: CURRENT_DEMO_USER.name,
    userAvatar: CURRENT_DEMO_USER.avatar,
    content: rawContent.trim(),
    createdAt: new Date().toISOString(),
    updatedAt: null
  };

  const saved = addCommentToStorage(newComment);

  if (!saved) {
    showCommentToast("Không thể lưu bình luận. Vui lòng thử lại.", "error");
    announceToScreenReader("Không thể lưu bình luận. Vui lòng thử lại.");
    return;
  }

  // Reset form
  textarea.value = "";
  updateCharCounter(charCounter, 0);
  validationMsg.classList.remove("comment-validation-msg--visible");

  // Re-render danh sách
  renderCommentList(recipeId, listContainer);

  showCommentToast("✓ Bình luận thành công.", "success");
  announceToScreenReader("Bình luận thành công.");

  textarea.focus();
}

/**
 * Xử lý chỉnh sửa bình luận (mở form nội tuyến).
 * @param {string} commentId
 * @param {number} recipeId
 * @param {HTMLElement} listContainer
 */
function handleEditComment(commentId, recipeId, listContainer) {
  // Đóng các form edit đang mở khác
  listContainer.querySelectorAll("[data-edit-form]").forEach((form) => {
    if (form.dataset.editForm !== commentId) {
      // Khôi phục bình luận gốc
      const parentItem = form.closest(".comment-item");
      if (parentItem) {
        const content = parentItem.querySelector(".comment-content-text");
        const actions = parentItem.querySelector(".comment-actions-row");
        if (content) content.style.display = "";
        if (actions) actions.style.display = "";
        form.remove();
      }
    }
  });

  const all = loadAllComments();
  const comment = all.find(
    (c) => c.id === commentId && c.userId === CURRENT_DEMO_USER.id
  );
  if (!comment) return;

  const itemEl = listContainer.querySelector(`[data-comment-id="${commentId}"]`);
  if (!itemEl) return;

  // Nếu form đang mở rồi thì bỏ qua
  if (itemEl.querySelector("[data-edit-form]")) return;

  const bodyEl = itemEl.querySelector(".comment-body");
  if (!bodyEl) return;

  buildInlineEditForm(
    comment,
    bodyEl,
    /* onSave */ (cId, newContent) => {
      const ok = updateCommentInStorage(cId, newContent);
      if (ok) {
        renderCommentList(recipeId, listContainer);
        showCommentToast("✓ Cập nhật bình luận thành công.", "success");
        announceToScreenReader("Cập nhật bình luận thành công.");
      } else {
        showCommentToast("Không thể cập nhật bình luận. Vui lòng thử lại.", "error");
        announceToScreenReader("Không thể cập nhật bình luận.");
      }
    },
    /* onCancel */ () => { /* Không cần xử lý thêm */ }
  );
}

/**
 * Xử lý xóa bình luận sau khi người dùng xác nhận.
 * @param {string} commentId
 * @param {number} recipeId
 * @param {HTMLElement} listContainer
 */
function handleDeleteComment(commentId, recipeId, listContainer) {
  const confirmed = confirm("Bạn có chắc muốn xóa bình luận này không?");
  if (!confirmed) return;

  const ok = deleteCommentFromStorage(commentId);
  if (ok) {
    renderCommentList(recipeId, listContainer);
    showCommentToast("✓ Xóa bình luận thành công.", "success");
    announceToScreenReader("Xóa bình luận thành công.");
  } else {
    showCommentToast("Không thể xóa bình luận. Vui lòng thử lại.", "error");
  }
}

// ==========================================================================
// SECTION BUILDER
// ==========================================================================

/**
 * Xây dựng cấu trúc DOM của phần bình luận và trả về section element.
 * Caller chịu trách nhiệm chèn phần tử vào DOM.
 * @param {number} recipeId
 * @returns {HTMLElement} section element đã được khởi tạo đầy đủ
 */
function buildCommentSectionNode(recipeId) {
  /* ---------- Wrapper section ---------- */
  const section = document.createElement("section");
  section.id = "recipeCommentSection";
  section.className = "comment-section";
  section.setAttribute("aria-label", "Khu vực bình luận công thức");

  /* ---------- Header ---------- */
  const headerDiv = document.createElement("div");
  headerDiv.className = "comment-section-header";

  const heading = document.createElement("h2");
  heading.className = "comment-section-title";

  const titleIcon = document.createElement("span");
  titleIcon.className = "title-icon";
  titleIcon.setAttribute("aria-hidden", "true");
  titleIcon.textContent = "💬";

  const titleText = document.createTextNode(" Bình luận");

  const countBadge = document.createElement("span");
  countBadge.id = "commentCountBadge";
  countBadge.className = "comment-count-badge";
  countBadge.setAttribute("aria-label", "Số lượng bình luận");
  countBadge.textContent = "0";

  heading.appendChild(titleIcon);
  heading.appendChild(titleText);
  heading.appendChild(countBadge);
  headerDiv.appendChild(heading);
  section.appendChild(headerDiv);

  /* ---------- ARIA live region (ẩn) ---------- */
  const liveRegion = document.createElement("div");
  liveRegion.id = "commentSrLive";
  liveRegion.className = "comment-sr-live";
  liveRegion.setAttribute("aria-live", "polite");
  liveRegion.setAttribute("aria-atomic", "true");
  section.appendChild(liveRegion);

  /* ---------- Comment form ---------- */
  const formArea = document.createElement("div");
  formArea.className = "comment-form-area";

  // Demo user avatar
  const avatarEl = document.createElement("div");
  avatarEl.className = "comment-avatar";
  avatarEl.setAttribute("aria-hidden", "true");
  avatarEl.innerHTML = buildAvatarContent(CURRENT_DEMO_USER);

  // Form content
  const formContent = document.createElement("div");
  formContent.className = "comment-form-content";

  // Textarea wrapper
  const textareaWrapper = document.createElement("div");
  textareaWrapper.className = "comment-textarea-wrapper";

  const textareaId = "commentNewTextarea";
  const textareaLabel = document.createElement("label");
  textareaLabel.setAttribute("for", textareaId);
  textareaLabel.className = "comment-sr-live"; // ẩn – a11y
  textareaLabel.textContent = "Nhập nội dung bình luận của bạn";

  const textarea = document.createElement("textarea");
  textarea.id = textareaId;
  textarea.className = "comment-textarea";
  textarea.setAttribute("placeholder", "Chia sẻ cảm nhận của bạn về món ăn này...");
  textarea.setAttribute("maxlength", String(COMMENT_MAX_LENGTH));
  textarea.setAttribute("rows", "3");
  textarea.setAttribute("aria-label", "Nhập nội dung bình luận");
  textarea.setAttribute("aria-describedby", "commentCharCounter commentValidationMsg");

  textareaWrapper.appendChild(textareaLabel);
  textareaWrapper.appendChild(textarea);

  // Character counter
  const charCounter = document.createElement("div");
  charCounter.id = "commentCharCounter";
  charCounter.className = "comment-char-counter";
  charCounter.setAttribute("aria-live", "off");
  charCounter.textContent = `0 / ${COMMENT_MAX_LENGTH}`;

  // Validation message
  const validationMsg = document.createElement("div");
  validationMsg.id = "commentValidationMsg";
  validationMsg.className = "comment-validation-msg";
  validationMsg.setAttribute("role", "alert");

  // Actions (submit button)
  const formActions = document.createElement("div");
  formActions.className = "comment-form-actions";

  const submitBtn = document.createElement("button");
  submitBtn.type = "button";
  submitBtn.className = "btn-submit-comment";
  submitBtn.setAttribute("aria-label", "Gửi bình luận");
  submitBtn.textContent = "💬 Gửi bình luận";

  formActions.appendChild(submitBtn);

  formContent.appendChild(textareaWrapper);
  formContent.appendChild(charCounter);
  formContent.appendChild(validationMsg);
  formContent.appendChild(formActions);

  formArea.appendChild(avatarEl);
  formArea.appendChild(formContent);
  section.appendChild(formArea);

  /* ---------- Comment list container ---------- */
  const listContainer = document.createElement("div");
  listContainer.id = "commentList";
  listContainer.className = "comment-list";
  listContainer.setAttribute("aria-label", "Danh sách bình luận");
  section.appendChild(listContainer);

  /* ---------- Bind Events ---------- */

  // Bộ đếm ký tự
  textarea.addEventListener("input", () => {
    updateCharCounter(charCounter, textarea.value.length);
    // Ẩn validation khi người dùng bắt đầu nhập
    if (validationMsg.classList.contains("comment-validation-msg--visible")) {
      validationMsg.classList.remove("comment-validation-msg--visible");
    }
  });

  // Submit bằng nút
  submitBtn.addEventListener("click", () => {
    handleAddComment(
      recipeId,
      listContainer,
      textarea,
      charCounter,
      validationMsg,
      submitBtn
    );
  });

  // Submit bằng Ctrl+Enter
  textarea.addEventListener("keydown", (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key === "Enter") {
      e.preventDefault();
      handleAddComment(
        recipeId,
        listContainer,
        textarea,
        charCounter,
        validationMsg,
        submitBtn
      );
    }
  });

  /* ---------- Render danh sách lần đầu ---------- */
  renderCommentList(recipeId, listContainer);

  return section;
}


// ==========================================================================
// PUBLIC INITIALIZATION ENTRY POINT
// ==========================================================================

/**
 * Khởi tạo module bình luận trên trang Chi tiết Công thức.
 * Phải được gọi sau khi initializeDetailsRecipe() hoàn tất và
 * DOM đã được cập nhật với nội dung công thức.
 * @param {number} recipeId  – ID công thức đã xác thực từ recipe.js
 */
function initializeRecipeComments(recipeId) {
  // Kiểm tra recipeId hợp lệ
  if (!recipeId || isNaN(Number(recipeId)) || Number(recipeId) <= 0) {
    console.warn("[CookShare Comments] recipeId không hợp lệ – bỏ qua khởi tạo.");
    return;
  }

  // Ngăn chạy 2 lần (ví dụ: hot reload hoặc DOMContentLoaded trùng)
  if (document.getElementById("recipeCommentSection")) return;

  // Tìm phần tử `.details-footer-nav` được render bởi recipe.js;
  // comment section được chèn ngay trước nó (giữa content grid và nav bar).
  const footerNav = document.querySelector(".details-footer-nav");
  if (footerNav) {
    const section = buildCommentSectionNode(Number(recipeId));
    footerNav.insertAdjacentElement("beforebegin", section);
    return;
  }

  // Fallback: chèn vào cuối recipeDetailsContainer khi không tìm thấy footerNav
  const container = document.getElementById("recipeDetailsContainer");
  if (container) {
    const section = buildCommentSectionNode(Number(recipeId));
    container.appendChild(section);
  }
}
