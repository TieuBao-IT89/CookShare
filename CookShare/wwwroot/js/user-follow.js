/**
 * COOKSHARE - USER FOLLOW INTERACTIONS MODULE
 * Quản lý tương tác Follow / Unfollow người dùng bằng localStorage
 * Hỗ trợ nhiều tài khoản demo, các tab Followers, Following, và đồng bộ counters.
 */

(function () {
  "use strict";

  // ==========================================================================
  // 1. HẰNG SỐ & DỮ LIỆU DEMO USERS
  // ==========================================================================

  const FOLLOW_STORAGE_KEY = "cookshare_user_follows_v1";
  const CURRENT_DEMO_USER_ID = "demo-current-user";

  /**
   * Danh sách tài khoản demo chuẩn trong hệ thống CookShare
   */
  const DEMO_USERS = [
    {
      id: "demo-current-user",
      name: "Nguyễn Hải",
      username: "haivo200c-lang",
      avatar: "https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=300",
      bio: "Yêu thích nấu ăn, khám phá những món ngon và chia sẻ công thức với cộng đồng CookShare.",
      recipesCount: 6
    },
    {
      id: "demo-nguyen-van-an",
      name: "Nguyễn Văn An",
      username: "nguyenvanan",
      avatar: null,
      initials: "NA",
      bio: "Đam mê ẩm thực Việt Nam truyền thống, đặc biệt là các món phở và nước dùng ninh xương đậm đà.",
      recipesCount: 1
    },
    {
      id: "demo-tran-minh-anh",
      name: "Trần Minh Anh",
      username: "tranminhanh",
      avatar: null,
      initials: "MA",
      bio: "Yêu thích làm bánh mì, bánh ngọt phương Tây và các món tráng miệng thanh mát.",
      recipesCount: 1
    },
    {
      id: "demo-le-hoang-nam",
      name: "Lê Hoàng Nam",
      username: "lehoangnam",
      avatar: null,
      initials: "LN",
      bio: "Đầu bếp tại gia yêu thích các món ăn gia đình đậm đà hương vị miền Trung.",
      recipesCount: 1
    }
  ];

  /**
   * Quan hệ Follow mẫu mặc định ban đầu (nếu localStorage chưa từng có dữ liệu).
   * Ví dụ: Một số người dùng đã theo dõi nhau từ trước.
   */
  const DEFAULT_INITIAL_FOLLOWS = [
    {
      followerId: "demo-nguyen-van-an",
      followingId: "demo-current-user",
      createdAt: "2026-10-18T08:00:00.000Z"
    },
    {
      followerId: "demo-tran-minh-anh",
      followingId: "demo-current-user",
      createdAt: "2026-10-19T09:30:00.000Z"
    },
    {
      followerId: "demo-current-user",
      followingId: "demo-nguyen-van-an",
      createdAt: "2026-10-19T10:00:00.000Z"
    }
  ];

  // ==========================================================================
  // 2. CÁC HÀM XỬ LÝ LOCALSTORAGE & RELATIONSHIPS
  // ==========================================================================

  /**
   * Đọc danh sách quan hệ follow từ localStorage.
   * @returns {Array<{followerId: string, followingId: string, createdAt: string}>}
   */
  function getFollowRelationships() {
    try {
      const raw = localStorage.getItem(FOLLOW_STORAGE_KEY);
      if (raw === null) {
        // Khởi tạo quan hệ mặc định
        saveFollowRelationships(DEFAULT_INITIAL_FOLLOWS);
        return [...DEFAULT_INITIAL_FOLLOWS];
      }
      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) return [];
      return parsed.filter(
        (rel) =>
          rel &&
          typeof rel.followerId === "string" &&
          typeof rel.followingId === "string"
      );
    } catch (err) {
      console.warn("[CookShare Follow] Lỗi khi đọc dữ liệu từ localStorage:", err);
      return [];
    }
  }

  /**
   * Ghi danh sách quan hệ follow vào localStorage.
   * @param {Array} relationships
   * @returns {boolean} true nếu thành công
   */
  function saveFollowRelationships(relationships) {
    try {
      localStorage.setItem(FOLLOW_STORAGE_KEY, JSON.stringify(relationships));
      return true;
    } catch (err) {
      console.error("[CookShare Follow] Lỗi khi lưu vào localStorage:", err);
      return false;
    }
  }

  // ==========================================================================
  // 3. CÁC HÀM NGHIỆP VỤ (BUSINESS LOGIC)
  // ==========================================================================

  /**
   * Tìm người dùng demo theo ID
   * @param {string} userId
   * @returns {Object|null}
   */
  function getUserById(userId) {
    if (!userId) return null;
    return DEMO_USERS.find((u) => u.id === userId) || null;
  }

  /**
   * Kiểm tra followerId có đang theo dõi followingId không
   * @param {string} followerId
   * @param {string} followingId
   * @returns {boolean}
   */
  function isFollowing(followerId, followingId) {
    if (!followerId || !followingId || followerId === followingId) return false;
    const list = getFollowRelationships();
    return list.some(
      (rel) => rel.followerId === followerId && rel.followingId === followingId
    );
  }

  /**
   * Theo dõi một người dùng
   * @param {string} followerId
   * @param {string} followingId
   * @returns {boolean}
   */
  function followUser(followerId, followingId) {
    if (!followerId || !followingId) return false;
    if (followerId === followingId) {
      console.warn("[CookShare Follow] Không thể tự theo dõi chính mình!");
      return false;
    }
    if (!getUserById(followerId) || !getUserById(followingId)) {
      console.warn("[CookShare Follow] Người dùng không hợp lệ!");
      return false;
    }

    const list = getFollowRelationships();
    const already = list.some(
      (rel) => rel.followerId === followerId && rel.followingId === followingId
    );
    if (already) return true;

    list.push({
      followerId,
      followingId,
      createdAt: new Date().toISOString()
    });

    return saveFollowRelationships(list);
  }

  /**
   * Bỏ theo dõi một người dùng
   * @param {string} followerId
   * @param {string} followingId
   * @returns {boolean}
   */
  function unfollowUser(followerId, followingId) {
    if (!followerId || !followingId) return false;
    const list = getFollowRelationships();
    const nextList = list.filter(
      (rel) => !(rel.followerId === followerId && rel.followingId === followingId)
    );
    return saveFollowRelationships(nextList);
  }

  /**
   * Toggle follow của current demo user đối với targetUserId
   * @param {string} targetUserId
   * @returns {boolean} Trạng thái sau khi toggle (true = đang theo dõi)
   */
  function toggleFollow(targetUserId) {
    if (!targetUserId || targetUserId === CURRENT_DEMO_USER_ID) return false;
    const following = isFollowing(CURRENT_DEMO_USER_ID, targetUserId);
    if (following) {
      unfollowUser(CURRENT_DEMO_USER_ID, targetUserId);
      return false;
    } else {
      followUser(CURRENT_DEMO_USER_ID, targetUserId);
      return true;
    }
  }

  /**
   * Lấy danh sách những người đang theo dõi userId (Followers)
   * @param {string} userId
   * @returns {Array<Object>} Mảng demo users
   */
  function getFollowers(userId) {
    if (!userId) return [];
    const list = getFollowRelationships();
    const followerIds = list
      .filter((rel) => rel.followingId === userId)
      .map((rel) => rel.followerId);
    return followerIds.map((id) => getUserById(id)).filter(Boolean);
  }

  /**
   * Lấy danh sách những người mà userId đang theo dõi (Following)
   * @param {string} userId
   * @returns {Array<Object>} Mảng demo users
   */
  function getFollowing(userId) {
    if (!userId) return [];
    const list = getFollowRelationships();
    const followingIds = list
      .filter((rel) => rel.followerId === userId)
      .map((rel) => rel.followingId);
    return followingIds.map((id) => getUserById(id)).filter(Boolean);
  }

  /**
   * Đếm số lượng followers
   * @param {string} userId
   * @returns {number}
   */
  function getFollowerCount(userId) {
    return getFollowers(userId).length;
  }

  /**
   * Đếm số lượng following
   * @param {string} userId
   * @returns {number}
   */
  function getFollowingCount(userId) {
    return getFollowing(userId).length;
  }

  // ==========================================================================
  // 4. TIỆN ÍCH HELPER & HTML RENDERING
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
   * Tạo HTML nút Follow cho một user cụ thể
   * @param {string} targetUserId
   * @param {string} extraClass
   * @returns {string}
   */
  function renderFollowButton(targetUserId, extraClass = "") {
    if (!targetUserId || targetUserId === CURRENT_DEMO_USER_ID) {
      return "";
    }
    const following = isFollowing(CURRENT_DEMO_USER_ID, targetUserId);
    const pressed = following ? "true" : "false";
    const activeClass = following ? "is-following" : "";
    const label = following ? "✓ Đang theo dõi" : "+ Theo dõi";
    const title = following ? "Nhấn để bỏ theo dõi" : "Nhấn để theo dõi";

    return `
      <button type="button"
              class="btn-follow-action ${extraClass} ${activeClass}"
              data-user-id="${escapeHtml(targetUserId)}"
              aria-pressed="${pressed}"
              title="${title}">
        <span class="follow-btn-text">${escapeHtml(label)}</span>
      </button>
    `;
  }

  /**
   * Tạo HTML thẻ User Card cho danh sách Followers / Following
   * @param {Object} user
   * @returns {string}
   */
  function renderUserCard(user) {
    const isSelf = user.id === CURRENT_DEMO_USER_ID;
    const profileUrl = isSelf ? "/Profile" : `/Profile?userId=${encodeURIComponent(user.id)}`;
    const initials = user.initials || getInitials(user.name);

    let avatarHtml = "";
    if (user.avatar) {
      avatarHtml = `<img src="${escapeHtml(user.avatar)}" alt="${escapeHtml(user.name)}" class="user-card-avatar" />`;
    } else {
      avatarHtml = `<div class="user-card-avatar avatar-fallback">${escapeHtml(initials)}</div>`;
    }

    let actionBtnHtml = "";
    if (isSelf) {
      actionBtnHtml = `<span class="user-card-badge-self">Bạn</span>`;
    } else {
      actionBtnHtml = renderFollowButton(user.id);
    }

    return `
      <div class="user-card" data-user-id="${escapeHtml(user.id)}">
        <div class="user-card-left">
          <a href="${profileUrl}" title="Xem trang cá nhân ${escapeHtml(user.name)}">
            ${avatarHtml}
          </a>
          <div class="user-card-info">
            <h4 class="user-card-name">
              <a href="${profileUrl}">${escapeHtml(user.name)}</a>
            </h4>
            <div class="user-card-username">@${escapeHtml(user.username)}</div>
            <p class="user-card-bio" title="${escapeHtml(user.bio || "")}">${escapeHtml(user.bio || "")}</p>
          </div>
        </div>
        <div class="user-card-right">
          ${actionBtnHtml}
        </div>
      </div>
    `;
  }

  // ==========================================================================
  // 5. HIỂN THỊ VÀ ĐỒNG BỘ TRANG PROFILE
  // ==========================================================================

  let currentActiveTab = "recipes"; // 'recipes' | 'followers' | 'following'
  let currentProfileUserId = CURRENT_DEMO_USER_ID;

  /**
   * Lấy ID người dùng đang hiển thị trên trang Profile (từ URL param ?userId=...)
   */
  function resolveProfileUserId() {
    // 1. Kiểm tra URL query param
    const urlParams = new URLSearchParams(window.location.search);
    const userIdFromUrl = urlParams.get("userId");
    if (userIdFromUrl && getUserById(userIdFromUrl)) {
      return userIdFromUrl;
    }

    // 2. Kiểm tra phần tử ẩn (nếu Controller truyền qua ViewData)
    const hiddenEl = document.getElementById("profileTargetUserId");
    if (hiddenEl && hiddenEl.value && getUserById(hiddenEl.value)) {
      return hiddenEl.value;
    }

    // 3. Mặc định là tài khoản hiện tại
    return CURRENT_DEMO_USER_ID;
  }

  /**
   * Khởi tạo giao diện trang Profile
   */
  function initializeProfilePage() {
    const profilePageEl = document.querySelector(".profile-page");
    if (!profilePageEl) return;

    currentProfileUserId = resolveProfileUserId();
    const user = getUserById(currentProfileUserId) || getUserById(CURRENT_DEMO_USER_ID);
    const isSelf = currentProfileUserId === CURRENT_DEMO_USER_ID;

    // 1. Cập nhật thông tin Header
    const nameEl = document.querySelector(".profile-name");
    const usernameEl = document.querySelector(".profile-username");
    const descEl = document.querySelector(".profile-description");
    const avatarEl = document.querySelector(".profile-avatar");
    const settingsBtn = document.querySelector(".profile-settings");
    const nameRowEl = document.querySelector(".profile-name-row");

    if (nameEl) nameEl.textContent = user.name;
    if (usernameEl) usernameEl.textContent = `@${user.username}`;
    if (descEl) descEl.textContent = user.bio;

    if (avatarEl) {
      if (user.avatar) {
        avatarEl.src = user.avatar;
        avatarEl.alt = `Ảnh đại diện ${user.name}`;
      } else {
        // Thay thế bằng avatar fallback chứa chữ cái đầu
        const initials = user.initials || getInitials(user.name);
        avatarEl.src = `https://ui-avatars.com/api/?name=${encodeURIComponent(user.name)}&background=e8f5e9&color=2e7d32&bold=true&size=240`;
        avatarEl.alt = `Ảnh đại diện ${user.name}`;
      }
    }

    // 2. Nút Settings / Nút Follow trong Profile Header
    // Xóa nút follow header cũ nếu có
    const existingHeaderFollowBtn = document.getElementById("profileHeaderFollowBtn");
    if (existingHeaderFollowBtn) existingHeaderFollowBtn.remove();

    if (isSelf) {
      if (settingsBtn) settingsBtn.style.display = "flex";
    } else {
      if (settingsBtn) settingsBtn.style.display = "none";
      if (nameRowEl) {
        const followBtnWrapper = document.createElement("div");
        followBtnWrapper.id = "profileHeaderFollowBtn";
        followBtnWrapper.innerHTML = renderFollowButton(currentProfileUserId);
        nameRowEl.appendChild(followBtnWrapper);
      }
    }

    // 3. Cập nhật Counters
    updateProfileCounters();

    // 4. Khởi tạo cấu trúc các container chứa tab content
    setupTabContainers();

    // 5. Gắn sự kiện chuyển tab
    setupTabListeners();

    // 6. Hiển thị tab đang chọn
    switchTab(currentActiveTab);
  }

  /**
   * Cập nhật số lượng Followers & Following hiển thị
   */
  function updateProfileCounters() {
    const statsContainer = document.querySelector(".profile-stats");
    if (!statsContainer) return;

    const followerCount = getFollowerCount(currentProfileUserId);
    const followingCount = getFollowingCount(currentProfileUserId);

    const stats = statsContainer.querySelectorAll(".stat");
    if (stats.length >= 2) {
      // Stat 1: Followers
      const followerNum = stats[0].querySelector("strong");
      if (followerNum) followerNum.textContent = followerCount;

      // Stat 2: Following
      const followingNum = stats[1].querySelector("strong");
      if (followingNum) followingNum.textContent = followingCount;
    }
  }

  /**
   * Chuẩn bị DOM containers cho Followers và Following tabs
   */
  function setupTabContainers() {
    const container = document.querySelector(".profile-page .container");
    if (!container) return;

    // Kiểm tra xem container followers/following đã có chưa
    let followersContainer = document.getElementById("profileFollowersContainer");
    if (!followersContainer) {
      followersContainer = document.createElement("div");
      followersContainer.id = "profileFollowersContainer";
      followersContainer.style.display = "none";
      container.appendChild(followersContainer);
    }

    let followingContainer = document.getElementById("profileFollowingContainer");
    if (!followingContainer) {
      followingContainer = document.createElement("div");
      followingContainer.id = "profileFollowingContainer";
      followingContainer.style.display = "none";
      container.appendChild(followingContainer);
    }
  }

  /**
   * Gắn sự kiện click cho các tab
   */
  function setupTabListeners() {
    const tabs = document.querySelectorAll(".profile-tab");
    if (tabs.length < 3) return;

    tabs[0].setAttribute("data-tab-type", "recipes");
    tabs[1].setAttribute("data-tab-type", "followers");
    tabs[2].setAttribute("data-tab-type", "following");

    tabs.forEach((tab) => {
      tab.addEventListener("click", function () {
        const type = this.getAttribute("data-tab-type");
        if (type) switchTab(type);
      });
    });
  }

  /**
   * Chuyển tab trên trang Profile
   * @param {string} tabType 'recipes' | 'followers' | 'following'
   */
  function switchTab(tabType) {
    currentActiveTab = tabType;

    const tabs = document.querySelectorAll(".profile-tab");
    tabs.forEach((t) => {
      if (t.getAttribute("data-tab-type") === tabType) {
        t.classList.add("active");
        t.setAttribute("aria-selected", "true");
      } else {
        t.classList.remove("active");
        t.setAttribute("aria-selected", "false");
      }
    });

    const headingEl = document.querySelector(".profile-section-heading");
    const headingH2 = headingEl ? headingEl.querySelector("h2") : null;
    const headingP = headingEl ? headingEl.querySelector("p") : null;

    const recipesGrid = document.querySelector(".profile-recipe-grid");
    const followersContainer = document.getElementById("profileFollowersContainer");
    const followingContainer = document.getElementById("profileFollowingContainer");

    const user = getUserById(currentProfileUserId) || getUserById(CURRENT_DEMO_USER_ID);

    if (tabType === "recipes") {
      if (headingH2) headingH2.textContent = "Công thức đã đăng";
      if (headingP) headingP.textContent = `Những công thức được chia sẻ bởi ${user.name}`;
      if (recipesGrid) recipesGrid.style.display = "grid";
      if (followersContainer) followersContainer.style.display = "none";
      if (followingContainer) followingContainer.style.display = "none";
    } else if (tabType === "followers") {
      if (headingH2) headingH2.textContent = "Người theo dõi (Followers)";
      if (headingP) headingP.textContent = `Danh sách những người đang theo dõi ${user.name}`;
      if (recipesGrid) recipesGrid.style.display = "none";
      if (followingContainer) followingContainer.style.display = "none";
      if (followersContainer) {
        followersContainer.style.display = "block";
        renderFollowersList(followersContainer);
      }
    } else if (tabType === "following") {
      if (headingH2) headingH2.textContent = "Đang theo dõi (Following)";
      if (headingP) headingP.textContent = `Danh sách những người mà ${user.name} đang theo dõi`;
      if (recipesGrid) recipesGrid.style.display = "none";
      if (followersContainer) followersContainer.style.display = "none";
      if (followingContainer) {
        followingContainer.style.display = "block";
        renderFollowingList(followingContainer);
      }
    }
  }

  /**
   * Render danh sách người theo dõi
   */
  function renderFollowersList(containerEl) {
    const followers = getFollowers(currentProfileUserId);
    if (followers.length === 0) {
      containerEl.innerHTML = `
        <div class="follow-empty-state">
          <span class="follow-empty-icon">👥</span>
          <p>Chưa có người theo dõi nào.</p>
        </div>
      `;
      return;
    }

    containerEl.innerHTML = `
      <div class="user-cards-grid">
        ${followers.map((u) => renderUserCard(u)).join("")}
      </div>
    `;
  }

  /**
   * Render danh sách đang theo dõi
   */
  function renderFollowingList(containerEl) {
    const following = getFollowing(currentProfileUserId);
    if (following.length === 0) {
      containerEl.innerHTML = `
        <div class="follow-empty-state">
          <span class="follow-empty-icon">✨</span>
          <p>Chưa theo dõi ai.</p>
        </div>
      `;
      return;
    }

    containerEl.innerHTML = `
      <div class="user-cards-grid">
        ${following.map((u) => renderUserCard(u)).join("")}
      </div>
    `;
  }

  // ==========================================================================
  // 6. TÍCH HỢP HOMEPAGE (FEED AUTHOR NAVIGATION & FOLLOW BUTTON)
  // ==========================================================================

  /**
   * Ánh xạ tác giả bài đăng sang Demo User ID
   */
  const POST_AUTHOR_MAPPING = {
    "Nguyễn Văn An": "demo-nguyen-van-an",
    "Trần Minh Anh": "demo-tran-minh-anh",
    "Lê Hoàng Nam": "demo-le-hoang-nam"
  };

  /**
   * Khởi tạo liên kết tác giả và nút theo dõi trên bài đăng feed trang chủ
   */
  function initializeHomepageAuthors() {
    const posts = document.querySelectorAll(".recipe-post");
    posts.forEach((post) => {
      const userEl = post.querySelector(".post-user strong");
      const avatarEl = post.querySelector(".post-avatar");
      if (!userEl) return;

      const authorName = userEl.textContent.trim();
      const targetUserId = POST_AUTHOR_MAPPING[authorName];
      if (!targetUserId) return;

      const profileUrl = `/Profile?userId=${encodeURIComponent(targetUserId)}`;

      // 1. Biến tên tác giả thành link đến profile nếu chưa gắn
      if (!userEl.querySelector("a")) {
        userEl.innerHTML = `<a href="${profileUrl}" style="color:inherit;text-decoration:none;" title="Xem trang cá nhân của ${escapeHtml(authorName)}">${escapeHtml(authorName)}</a>`;
      }

      // 2. Biến avatar tác giả thành link đến profile nếu chưa gắn
      if (avatarEl && !avatarEl.querySelector("a") && avatarEl.tagName !== "A") {
        avatarEl.style.cursor = "pointer";
        avatarEl.setAttribute("title", `Xem trang cá nhân của ${authorName}`);
        avatarEl.addEventListener("click", function () {
          window.location.href = profileUrl;
        });
      }

      // 3. Thêm nút theo dõi nhanh cạnh tên tác giả nếu chưa có
      const postHeader = post.querySelector(".post-header");
      if (postHeader && !postHeader.querySelector(".btn-follow-feed")) {
        const followFeedBtnWrapper = document.createElement("div");
        followFeedBtnWrapper.innerHTML = renderFollowButton(targetUserId, "btn-follow-feed");
        const btnNode = followFeedBtnWrapper.firstElementChild;
        if (btnNode) {
          // Chèn trước nút menu '⋯'
          const menuEl = postHeader.querySelector(".post-menu");
          if (menuEl) {
            postHeader.insertBefore(btnNode, menuEl);
          } else {
            postHeader.appendChild(btnNode);
          }
        }
      }
    });
  }

  // ==========================================================================
  // 7. EVENT DELEGATION & STATE SYNC
  // ==========================================================================

  /**
   * Cập nhật tất cả các nút Follow trên trang có targetUserId tương ứng
   * @param {string} targetUserId
   */
  function syncFollowButtonsUI(targetUserId) {
    if (!targetUserId) return;
    const following = isFollowing(CURRENT_DEMO_USER_ID, targetUserId);
    const label = following ? "✓ Đang theo dõi" : "+ Theo dõi";
    const title = following ? "Nhấn để bỏ theo dõi" : "Nhấn để theo dõi";
    const pressed = following ? "true" : "false";

    const buttons = document.querySelectorAll(`.btn-follow-action[data-user-id="${targetUserId}"]`);
    buttons.forEach((btn) => {
      btn.setAttribute("aria-pressed", pressed);
      btn.setAttribute("title", title);
      const textSpan = btn.querySelector(".follow-btn-text");
      if (textSpan) textSpan.textContent = label;

      if (following) {
        btn.classList.add("is-following");
      } else {
        btn.classList.remove("is-following");
      }
    });
  }

  /**
   * Đồng bộ toàn bộ trạng thái UI sau khi follow/unfollow
   * @param {string} targetUserId
   */
  function syncFullUI(targetUserId) {
    // 1. Cập nhật các nút liên quan
    syncFollowButtonsUI(targetUserId);

    // 2. Cập nhật counters trang profile
    updateProfileCounters();

    // 3. Render lại danh sách tab nếu đang ở Followers hoặc Following
    const followersContainer = document.getElementById("profileFollowersContainer");
    const followingContainer = document.getElementById("profileFollowingContainer");

    if (currentActiveTab === "followers" && followersContainer) {
      renderFollowersList(followersContainer);
    } else if (currentActiveTab === "following" && followingContainer) {
      renderFollowingList(followingContainer);
    }
  }

  // Bắt sự kiện click vào nút follow bằng event delegation
  document.addEventListener("click", function (e) {
    const btn = e.target.closest(".btn-follow-action");
    if (!btn) return;

    e.preventDefault();
    e.stopPropagation();

    const targetUserId = btn.getAttribute("data-user-id");
    if (!targetUserId) return;

    toggleFollow(targetUserId);
    syncFullUI(targetUserId);
  });

  // Đồng bộ qua các tab bằng storage event
  window.addEventListener("storage", function (e) {
    if (e.key === FOLLOW_STORAGE_KEY) {
      updateProfileCounters();
      DEMO_USERS.forEach((u) => syncFollowButtonsUI(u.id));
      const followersContainer = document.getElementById("profileFollowersContainer");
      const followingContainer = document.getElementById("profileFollowingContainer");
      if (currentActiveTab === "followers" && followersContainer) {
        renderFollowersList(followersContainer);
      } else if (currentActiveTab === "following" && followingContainer) {
        renderFollowingList(followingContainer);
      }
    }
  });

  // ==========================================================================
  // 8. AUTO INITIALIZATION
  // ==========================================================================

  document.addEventListener("DOMContentLoaded", function () {
    // 1. Khởi tạo dữ liệu mẫu nếu chưa có
    getFollowRelationships();

    // 2. Khởi tạo trang Profile nếu đang ở Profile
    initializeProfilePage();

    // 3. Khởi tạo Homepage tác giả và nút follow
    initializeHomepageAuthors();

    // 4. Đồng bộ tất cả nút
    DEMO_USERS.forEach((u) => syncFollowButtonsUI(u.id));
  });

  // ==========================================================================
  // 9. PUBLIC API CHO KIỂM THỬ VÀ MỞ RỘNG
  // ==========================================================================

  window.CookShareFollow = {
    DEMO_USERS,
    CURRENT_DEMO_USER_ID,
    getFollowRelationships,
    saveFollowRelationships,
    getUserById,
    isFollowing,
    followUser,
    unfollowUser,
    toggleFollow,
    getFollowers,
    getFollowing,
    getFollowerCount,
    getFollowingCount,
    renderFollowButton,
    renderUserCard,
    initializeProfilePage,
    initializeHomepageAuthors,
    syncFullUI
  };
})();
