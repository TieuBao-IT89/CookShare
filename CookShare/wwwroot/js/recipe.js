/**
 * COOKSHARE - RECIPE MANAGEMENT MODULE
 * Quản lý dữ liệu công thức nấu ăn bằng localStorage
 */

const RECIPE_STORAGE_KEY = "cookshare_recipes";
let recipeStorageReadable = true;

// Dữ liệu mẫu khởi tạo ban đầu nếu localStorage hoàn toàn trống
const DEFAULT_SAMPLE_RECIPES = [
  {
    id: 1,
    name: "Phở Bò Hà Nội Truyền Thống",
    description: "Nước dùng trong veo thơm nồng hương quế hồi, hòa quyện cùng bánh phở mềm mượt và thịt bò tái chín đậm đà chuẩn phong vị thủ đô.",
    image: "https://images.unsplash.com/photo-1582878826629-29b7ad1cdc43?auto=format&fit=crop&w=900&q=85",
    category: "Món Việt",
    ingredients: "1kg Xương ống bò\n500g Bánh phở tươi\n400g Thịt bắp bò / thăn bò\n1 củ Gừng tươi & 3 củ Hành tím (nướng thơm)\nHoa hồi, quế thanh, thảo quả, hạt mùi\nHành lá, rau mùi, húng quế, chanh ớt\nGia vị: Nước mắm ngon, muối hạt, đường phèn",
    instructions: "Bước 1: Rửa sạch xương bò với nước muối, chần qua nước sôi 5 phút để khử mùi hôi rồi rửa lại thật sạch.\nBước 2: Nướng thơm gừng, hành tím, hoa hồi, quế, thảo quả rồi bọc vào túi lọc vải.\nBước 3: Ninh xương bò với 3.5 lít nước trong ít nhất 2 - 3 tiếng, thả túi hương liệu và nêm đường phèn, muối, nước mắm ngon.\nBước 4: Chần bánh phở qua nước sôi, xếp vào tô, thêm thịt bò thái lát mỏng, hành hoa chẻ và chan nước dùng sôi sùng sục.\nBước 5: Thưởng thức nóng kèm quẩy giòn, chanh, ớt tươi và rau thơm."
  },
  {
    id: 2,
    name: "Cơm Gà Xé Hội An",
    description: "Cơm nấu nước luộc gà óng vàng màu nghệ, thịt gà ta dai ngọt xé phay trộn gỏi hành tây chua ngọt đậm vị phố cổ.",
    image: "https://images.unsplash.com/photo-1569058242253-92a9c755a0ec?auto=format&fit=crop&w=900&q=85",
    category: "Món Việt",
    ingredients: "1 con Gà ta thả vườn (khoảng 1.3 - 1.5kg)\n300g Gạo tẻ dẻo thơm\n1 nhánh Nghệ tươi giã lấy nước cốt\n1 củ Hành tây & 1 mớ Rau răm\n2 quả Tắc (quất), ớt tươi, tỏi băm\nGia vị: Nước mắm cốt, hạt tiêu xay, muối, đường",
    instructions: "Bước 1: Luộc gà cùng chút muối, gừng đập dập và nghệ tươi để da gà có màu vàng ươm đẹp mắt. Vớt gà để nguội rồi lọc xương xé sợi vừa ăn.\nBước 2: Vo sạch gạo, để ráo rồi xào sơ với mỡ gà và nước nghệ cho hạt gạo bóng bẩy. Nấu cơm bằng nước luộc gà.\nBước 3: Hành tây thái mỏng ngâm nước đá cho giòn và bớt hăng. Trộn thịt gà xé với hành tây, rau răm, nước tắc, mắm, tiêu, đường.\nBước 4: Xới cơm ra đĩa, xếp gỏi gà lên trên, ăn kèm bát canh lòng gà nóng hổi và tương ớt Hội An."
  },
  {
    id: 3,
    name: "Mì Ý Sốt Cà Chua Bò Bằm (Bolognese)",
    description: "Sợi mì Ý dai chuẩn phong cách Al Dente quyện trong lớp sốt thịt bò bằm cà chua tươi sánh mịn và phô mai Parmesan béo ngậy.",
    image: "https://images.unsplash.com/photo-1621996346565-e3dbc646d9a9?auto=format&fit=crop&w=900&q=85",
    category: "Món Âu",
    ingredients: "250g Mì Spaghetti Ý\n300g Thịt bò bằm tươi\n4 quả Cà chua chín mọng (hoặc 1 hộp cà chua bằm)\n1/2 củ Hành tây băm nhỏ\n2 tép Tỏi băm & Lá húng quế tươi (Basil)\nPhô mai bột Parmesan\nDầu ô liu, muối, tiêu xay, bơ lạt",
    instructions: "Bước 1: Đun sôi nồi nước với chút muối, luộc mì Spaghetti trong 8 - 10 phút đến khi chín tới (Al Dente), vớt ra trộn 1 thìa dầu ô liu.\nBước 2: Phi thơm tỏi và hành tây với dầu ô liu, cho thịt bò bằm vào xào săn cùng chút muối tiêu.\nBước 3: Cho cà chua băm nhuyễn vào đun nhỏ lửa 15 phút đến khi sốt sánh đặc, nêm nếm gia vị vừa ăn và thả lá húng quế.\nBước 4: Cho mì ra đĩa, rưới đẫm sốt bò bằm lên trên, rắc phô mai Parmesan bào mịn và thưởng thức khi còn nóng hổi."
  },
  {
    id: 4,
    name: "Salad Bơ Trứng & Rau Củ Thanh Nhiệt",
    description: "Món salad tươi mát giàu dinh dưỡng từ bơ sáp béo bùi, rau rocket, cà chua bi giòn ngọt và sốt mè rang thanh nhẹ cho ngày bận rộn.",
    image: "https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&w=900&q=85",
    category: "Món chay",
    ingredients: "1 quả Bơ sáp chín tới\n2 quả Trứng gà luộc lòng đào\n100g Xà lách Romaine & Rau mầm\n10 quả Cà chua bi đỏ ngọt\n1/2 quả Dưa leo baby\n3 muỗng sốt mè rang (hoặc sốt dầu giấm chanh vàng)",
    instructions: "Bước 1: Rửa sạch các loại rau củ với nước muối loãng, vẩy ráo nước. Xà lách cắt khúc vừa ăn, cà chua bi bổ đôi.\nBước 2: Bơ bỏ vỏ, cắt miếng vuông quân cờ hoặc lát mỏng đẹp mắt. Trứng gà luộc bổ múi cau.\nBước 3: Xếp lớp xà lách, dưa leo, cà chua bi và bơ vào đĩa sâu lòng, đặt trứng luộc lên trên cùng.\nBước 4: Rưới nước sốt mè rang đều khắp đĩa ngay trước khi dùng và trộn nhẹ nhàng."
  },
  {
    id: 5,
    name: "Bánh Tiramisu Cà Phê Ý",
    description: "Lớp kem Mascarpone béo mịn ngậy ngậy xen kẽ từng tầng bánh quy Savoiardi thấm đẫm cà phê Espresso và bột ca cao thơm đắng quyến rũ.",
    image: "https://images.unsplash.com/photo-1571877227200-a0d98ea607e9?auto=format&fit=crop&w=900&q=85",
    category: "Món tráng miệng",
    ingredients: "250g Phô mai Mascarpone\n200ml Kem tươi Whipping Cream\n1 gói Bánh sâm-panh (Ladyfingers/Savoiardi)\n2 lòng đỏ Trứng gà tươi & 50g Đường cát\n100ml Cà phê Espresso đậm đặc\n20ml Rượu Rum / Kahlua (tùy chọn)\nBột ca cao nguyên chất phủ mặt",
    instructions: "Bước 1: Đánh bông lòng đỏ trứng với đường cách thủy đến khi hỗn hợp chuyển màu vàng nhạt sánh mịn.\nBước 2: Đánh nhuyễn Mascarpone rồi trộn đều vào lòng đỏ trứng. Đánh bông nhẹ Whipping Cream rồi fold đều vào hỗn hợp phô mai.\nBước 3: Pha cà phê Espresso ấm cùng rượu Rum. Nhúng nhanh bánh quy sâm-panh vào cà phê (không ngâm quá lâu tránh nát bánh).\nBước 4: Xếp một lớp bánh quy vào khuôn, phủ lớp kem Mascarpone lên, lặp lại thêm một tầng nữa.\nBước 5: Bọc kín để tủ lạnh ít nhất 4 - 6 tiếng. Trước khi ăn rây đều bột ca cao lên mặt bánh."
  },
  {
    id: 6,
    name: "Trà Đào Cam Sả Mát Lạnh",
    description: "Thức uống giải nhiệt sảng khoái với vị chát nhẹ của trà đen, hương thơm thư thái của sả tươi và đào miếng giòn ngọt khó cưỡng.",
    image: "https://images.unsplash.com/photo-1499638673689-79a0b5115d87?auto=format&fit=crop&w=900&q=85",
    category: "Đồ uống",
    ingredients: "2 gói Trà túi lọc Earl Grey hoặc Hồng trà\n3 cây Sả tươi đập dập\n1 quả Cam vàng (vắt lấy nước cốt 1/2 quả, 1/2 quả cắt lát trang trí)\n3 - 4 miếng Đào ngâm giòn\n30ml Nước cốt ngâm đào & 25ml Nước đường\nĐá viên tinh khiết",
    instructions: "Bước 1: Đun sôi 300ml nước với sả đập dập trong 3 phút để tinh dầu sả tiết ra thơm nồng.\nBước 2: Tắt bếp, thả 2 gói trà túi lọc vào ủ trong 5 - 7 phút rồi vớt bỏ túi trà và cọng sả, để nước trà nguội bớt.\nBước 3: Cho vào bình lắc (shaker): nước cốt trà sả, nước cam tươi, nước đường, siro đào và nhiều đá viên.\nBước 4: Lắc đều tay khoảng 15 giây cho thức uống hòa quyện và lạnh sâu.\nBước 5: Rót ra ly cao, thêm các lát cam tươi, đào ngâm giòn và nhánh sả trang trí lên trên."
  }
];

// ==========================================================================
// STORAGE UTILITIES
// ==========================================================================

function reportRecipeStorageError(message) {
  window.CookShareUI.toast(message, "error");
  let notice = document.getElementById("recipeDataError");
  if (!notice) {
    notice = document.createElement("p");
    notice.id = "recipeDataError";
    notice.className = "notice container storage-error";
    notice.setAttribute("role", "alert");
    document.getElementById("main-content").prepend(notice);
  }
  notice.textContent = message;
}

function getRecipes() {
  try {
    const data = localStorage.getItem(RECIPE_STORAGE_KEY);
    recipeStorageReadable = true;
    if (data === null) {
      saveRecipes(DEFAULT_SAMPLE_RECIPES);
      return [...DEFAULT_SAMPLE_RECIPES];
    }
    const parsed = JSON.parse(data);
    if (!Array.isArray(parsed)) throw new Error("Invalid recipe collection");
    const valid = parsed.every(recipe => recipe && typeof recipe.name === "string" && Number.isSafeInteger(Number(recipe.id)) && Number(recipe.id) > 0);
    if (!valid) throw new Error("Invalid recipe data");
    return parsed;
  } catch {
    recipeStorageReadable = false;
    reportRecipeStorageError("Không thể đọc công thức trên thiết bị này. Hãy kiểm tra quyền lưu trữ của trình duyệt rồi tải lại trang. Dữ liệu hiện có chưa bị thay đổi.");
    return [];
  }
}

function saveRecipes(recipes) {
  if (!recipeStorageReadable) {
    reportRecipeStorageError("Chưa thể lưu vì dữ liệu công thức hiện tại không đọc được. Hãy kiểm tra bộ nhớ trình duyệt để giữ lại dữ liệu của bạn.");
    return false;
  }
  try {
    localStorage.setItem(RECIPE_STORAGE_KEY, JSON.stringify(recipes));
    return true;
  } catch {
    reportRecipeStorageError("Chưa thể lưu công thức. Bộ nhớ có thể đã đầy hoặc bị chặn. Hãy thử ảnh nhỏ hơn hoặc kiểm tra cài đặt trình duyệt.");
    return false;
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

// One Razor template for Home, Explore, Saved and Profile.
function recipeImageSource(value) {
  if (typeof value !== "string" || !value) return "/img/recipe-placeholder.svg";
  if (/^data:image\/(png|jpe?g|webp|gif|avif);base64,/i.test(value)) return value;
  try {
    const url = new URL(value, location.origin);
    // Keep existing localStorage records working while serving bundled sample photos.
    const sampleImages = {
      "photo-1582878826629-29b7ad1cdc43": "pho",
      "photo-1569058242253-92a9c755a0ec": "chicken-rice",
      "photo-1621996346565-e3dbc646d9a9": "pasta",
      "photo-1512621776951-a57141f2eefd": "salad",
      "photo-1571877227200-a0d98ea607e9": "tiramisu",
      "photo-1499638673689-79a0b5115d87": "peach-tea"
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
  const detailsUrl = `/Recipe/Details/${id}`;
  card.dataset.id = id;
  if (options.saved) card.dataset.savedRecipeId = id;
  const image = card.querySelector("img");
  image.src = recipeImageSource(recipe.image);
  image.alt = recipe.name;
  card.querySelector(".recipe-image-link").href = detailsUrl;
  card.querySelector(".recipe-image-link").setAttribute("aria-label", `Xem công thức ${recipe.name}`);
  card.querySelector(".recipe-category-badge").textContent = recipe.category || "Món ngon";
  card.querySelector(".card-category").textContent = recipe.category || "Công thức cộng đồng";
  const count = String(recipe.ingredients || "").split("\n").filter(line => line.trim()).length;
  card.querySelector(".card-ingredient-count").textContent = `${count} nguyên liệu`;
  const title = card.querySelector(".recipe-card-title a");
  title.textContent = recipe.name;
  title.href = detailsUrl;
  card.querySelector(".recipe-card-desc").textContent = recipe.description || "Món ngon được chia sẻ từ căn bếp CookShare.";
  card.querySelector(".card-author-name").textContent = recipe.author || "Đầu bếp CookShare";
  card.querySelector(".recipe-link").href = detailsUrl;
  card.querySelectorAll(".recipe-save-btn, .recipe-like-btn, .recipe-like-count-display").forEach(element => {
    element.dataset.recipeId = id;
    if (element.matches("button")) {
      element.dataset.recipeName = recipe.name;
      element.setAttribute("aria-label", `${element.classList.contains("recipe-like-btn") ? "Thích" : "Lưu"} ${recipe.name}`);
    }
  });
  card.querySelector(".recipe-manage-actions").hidden = !options.manage;
  card.querySelector(".btn-action-edit").href = `/Recipe/Edit/${id}`;
  card.querySelector(".btn-action-delete").dataset.deleteRecipe = id;
  card.querySelector(".saved-card-unsave-btn").hidden = !options.saved;
  return card.outerHTML;
}

function syncRecipeCards() {
  window.CookShareLikes?.syncAllRecipeLikesUI();
  window.CookShareSaved?.syncSavedRecipeUI();
}

// ==========================================================================
// 1. INDEX PAGE CONTROLLER
// ==========================================================================

let currentSelectedCategory = "";

function displayRecipes() {
  const recipeList = document.getElementById("recipeList");
  const emptyRecipe = document.getElementById("emptyRecipe");
  const resultCount = document.getElementById("recipeResultsCount");

  if (!recipeList) return;

  const searchInput = document.getElementById("searchRecipe");
  const headerSearch = document.getElementById("headerGlobalSearch");
  const categoryFilter = document.getElementById("categoryFilter");

  const searchText = (searchInput ? searchInput.value : headerSearch?.value || "").toLowerCase().trim();

  const category = currentSelectedCategory || (categoryFilter ? categoryFilter.value : "");

  const recipes = getRecipes();

  const filteredRecipes = recipes.filter((recipe) => {
    const nameMatch = recipe.name && recipe.name.toLowerCase().includes(searchText);
    const descMatch = String(recipe.description || "").toLowerCase().includes(searchText);
    const ingredientMatch = String(recipe.ingredients || "").toLowerCase().includes(searchText);
    const matchesSearch = !searchText || nameMatch || descMatch || ingredientMatch;

    const matchesCategory = !category || recipe.category === category;

    return matchesSearch && matchesCategory;
  });

  if (resultCount) {
    resultCount.innerHTML = `Hiển thị <strong>${filteredRecipes.length}</strong> / <strong>${recipes.length}</strong> công thức`;
  }

  recipeList.innerHTML = "";

  if (filteredRecipes.length === 0) {
    if (emptyRecipe) {
      emptyRecipe.style.display = "block";
      const emptyTitle = emptyRecipe.querySelector(".recipe-empty-title");
      const emptyDesc = emptyRecipe.querySelector(".recipe-empty-desc");
      if (emptyTitle && emptyDesc) {
        if (searchText || category) {
          emptyTitle.textContent = "Không tìm thấy công thức phù hợp";
          emptyDesc.textContent = "Thử thay đổi từ khóa tìm kiếm hoặc chọn danh mục khác nhé!";
        } else {
          emptyTitle.textContent = "Chưa có công thức nào";
          emptyDesc.textContent = "Hãy là người đầu tiên chia sẻ món ăn ngon đến cộng đồng CookShare!";
        }
      }
    }
    return;
  }

  if (emptyRecipe) {
    emptyRecipe.style.display = "none";
  }

  recipeList.innerHTML = filteredRecipes.map(recipe => renderRecipeCard(recipe, { manage: true })).join("");

  // Đồng bộ trạng thái Like cho danh sách công thức vừa render
  if (window.CookShareLikes && typeof window.CookShareLikes.syncAllRecipeLikesUI === "function") {
    window.CookShareLikes.syncAllRecipeLikesUI();
  }
  // Đồng bộ trạng thái Lưu cho danh sách công thức vừa render
  if (window.CookShareSaved && typeof window.CookShareSaved.syncSavedRecipeUI === "function") {
    window.CookShareSaved.syncSavedRecipeUI();
  }
}

function selectCategoryPill(catName, buttonElem) {
  currentSelectedCategory = catName;
  const categoryFilter = document.getElementById("categoryFilter");
  if (categoryFilter) {
    categoryFilter.value = catName;
  }

  document.querySelectorAll(".category-pill").forEach((pill) => {
    pill.classList.remove("active");
  });

  if (buttonElem) buttonElem.classList.add("active");
  document.querySelectorAll(".category-pill").forEach(pill => pill.setAttribute("aria-pressed", String(pill.classList.contains("active"))));

  displayRecipes();
}

async function deleteRecipe(id) {
  const recipes = getRecipes();
  const recipe = recipes.find((r) => Number(r.id) === Number(id));

  if (!recipe) {
    window.CookShareUI.toast("Không tìm thấy công thức cần xóa.", "error");
    return;
  }

  const confirmed = await window.CookShareUI.confirmDelete(`Công thức "${recipe.name}" sẽ được xóa khỏi thiết bị này. Thao tác này không thể hoàn tác.`);
  if (!confirmed) return;

  const newRecipes = recipes.filter((r) => Number(r.id) !== Number(id));
  if (!saveRecipes(newRecipes)) return;
  displayRecipes();
  initializeProfilePage();
  window.CookShareUI.toast("Đã xóa công thức.");
}

function initializeIndexPage() {
  const recipeList = document.getElementById("recipeList");
  if (!recipeList) return;

  const parameters = new URLSearchParams(location.search);
  const initialSearch = document.getElementById("searchRecipe");
  const initialCategory = document.getElementById("categoryFilter");
  if (initialSearch) initialSearch.value = parameters.get("q") || "";
  const categoryParam = parameters.get("category") || "";
  if (initialCategory && Array.from(initialCategory.options).some(option => option.value === categoryParam)) {
    initialCategory.value = categoryParam;
    currentSelectedCategory = categoryParam;
  }
  document.querySelectorAll(".category-pill").forEach(pill => {
    const active = pill.dataset.category === currentSelectedCategory;
    pill.classList.toggle("active", active);
    pill.setAttribute("aria-pressed", String(active));
  });
  document.getElementById("resetRecipeFilters")?.addEventListener("click", () => {
    initialSearch.value = "";
    document.getElementById("headerGlobalSearch").value = "";
    selectCategoryPill("", document.querySelector('.category-pill[data-category=""]'));
    history.replaceState(null, "", location.pathname);
    initialSearch.focus();
  });
  displayRecipes();

  const searchInput = document.getElementById("searchRecipe");
  const headerSearch = document.getElementById("headerGlobalSearch");
  const categoryFilter = document.getElementById("categoryFilter");

  if (searchInput) {
    searchInput.addEventListener("input", displayRecipes);
  }

  if (headerSearch) {
    headerSearch.addEventListener("input", () => {
      if (searchInput) searchInput.value = headerSearch.value;
      displayRecipes();
    });
  }

  if (categoryFilter) {
    categoryFilter.addEventListener("change", function () {
      currentSelectedCategory = this.value;
      document.querySelectorAll(".category-pill").forEach((pill) => {
        const pillCat = pill.getAttribute("data-category") || "";
        if (pillCat === currentSelectedCategory) {
          pill.classList.add("active");
        } else {
          pill.classList.remove("active");
        }
      });
      document.querySelectorAll(".category-pill").forEach(pill => pill.setAttribute("aria-pressed", String(pill.classList.contains("active"))));
      displayRecipes();
    });
  }

  // Khởi tạo Category Pills
  document.querySelectorAll(".category-pill").forEach((pill) => {
    pill.addEventListener("click", function () {
      const cat = this.getAttribute("data-category") || "";
      selectCategoryPill(cat, this);
    });
  });
}

// ==========================================================================
// 2. CREATE RECIPE CONTROLLER
// ==========================================================================

function initializeCreateRecipe() {
  const form = document.getElementById("createRecipeForm");
  if (!form) return;

  const imageInput = document.getElementById("recipeImage");
  const imageDropzone = document.getElementById("imageDropzone");
  const imagePreviewBox = document.getElementById("imagePreviewBox");
  let imageDataUrl = "";

  function handleImageFile(file) {
    if (!file) return;

    if (!["image/jpeg", "image/png", "image/webp"].includes(file.type)) {
      window.CookShareUI.toast("Vui lòng chọn ảnh JPG, PNG hoặc WEBP.", "error");
      return;
    }

    if (file.size > 3 * 1024 * 1024) {
      window.CookShareUI.toast("Ảnh tối đa 3 MB. Bạn hãy chọn ảnh nhỏ hơn nhé.", "error");
      return;
    }

    const reader = new FileReader();
    const submitButton = form.querySelector('[type="submit"]');
    if (submitButton) submitButton.disabled = true;
    reader.onerror = () => window.CookShareUI.toast("Chưa thể đọc ảnh. Vui lòng chọn lại.", "error");
    reader.onloadend = () => { if (submitButton) submitButton.disabled = false; };
    reader.onload = function (event) {
      imageDataUrl = event.target.result;
      renderImagePreview(imageDataUrl);
    };
    reader.readAsDataURL(file);
  }

  function renderImagePreview(src) {
    if (!imagePreviewBox) return;
    if (!src) {
      imagePreviewBox.innerHTML = "";
      imagePreviewBox.style.display = "none";
      if (imageDropzone) imageDropzone.style.display = "flex";
      return;
    }

    imagePreviewBox.style.display = "block";
    if (imageDropzone) imageDropzone.style.display = "none";

    imagePreviewBox.innerHTML = `
      <div class="image-preview-wrapper">
        <img src="${escapeHtml(recipeImageSource(src))}" alt="Ảnh xem trước">
        <div class="image-preview-overlay">
          <span>✓ Ảnh món ăn đã tải lên</span>
          <button type="button" class="btn-remove-image" id="btnRemoveImage">Thay ảnh khác</button>
        </div>
      </div>
    `;

    const removeBtn = document.getElementById("btnRemoveImage");
    if (removeBtn) {
      removeBtn.addEventListener("click", () => {
        imageDataUrl = "";
        if (imageInput) imageInput.value = "";
        renderImagePreview("");
      });
    }
  }

  if (imageInput) {
    imageInput.addEventListener("change", function () {
      if (this.files && this.files[0]) {
        handleImageFile(this.files[0]);
      }
    });
  }

  if (imageDropzone) {
    ["dragenter", "dragover"].forEach((eventName) => {
      imageDropzone.addEventListener(eventName, (e) => {
        e.preventDefault();
        e.stopPropagation();
        imageDropzone.classList.add("dragover");
      });
    });

    ["dragleave", "drop"].forEach((eventName) => {
      imageDropzone.addEventListener(eventName, (e) => {
        e.preventDefault();
        e.stopPropagation();
        imageDropzone.classList.remove("dragover");
      });
    });

    imageDropzone.addEventListener("drop", (e) => {
      const dt = e.dataTransfer;
      const files = dt.files;
      if (files && files[0]) {
        handleImageFile(files[0]);
      }
    });
  }

  // Submit Handler
  form.addEventListener("submit", function (e) {
    e.preventDefault();

    const nameInput = document.getElementById("recipeName");
    const categoryInput = document.getElementById("recipeCategory");
    const descInput = document.getElementById("recipeDescription");
    const ingredientsInput = document.getElementById("recipeIngredients");
    const instructionsInput = document.getElementById("recipeInstructions");

    const name = nameInput ? nameInput.value.trim() : "";
    const category = categoryInput ? categoryInput.value : "";
    const description = descInput ? descInput.value.trim() : "";
    const ingredients = ingredientsInput ? ingredientsInput.value.trim() : "";
    const instructions = instructionsInput ? instructionsInput.value.trim() : "";

    let hasError = false;

    function validateField(val, errorElemId, message) {
      const errElem = document.getElementById(errorElemId);
      const field = document.querySelector(`[aria-describedby~="${errorElemId}"]`);
      field?.setAttribute("aria-invalid", String(!val));
      if (!val) {
        if (errElem) {
          errElem.textContent = message;
          errElem.classList.add("visible");
        }
        hasError = true;
      } else {
        if (errElem) {
          errElem.textContent = "";
          errElem.classList.remove("visible");
        }
      }
    }

    validateField(name, "nameError", "Vui lòng nhập tên món ăn.");
    validateField(category, "categoryError", "Vui lòng chọn danh mục món ăn.");
    validateField(description, "descriptionError", "Vui lòng nhập mô tả ngắn về món ăn.");
    validateField(ingredients, "ingredientsError", "Vui lòng nhập danh sách nguyên liệu.");
    validateField(instructions, "instructionsError", "Vui lòng nhập hướng dẫn chế biến.");

    if (hasError) {
      const firstError = document.querySelector(".form-error.visible");
      if (firstError) {
        firstError.parentElement.querySelector("input, select, textarea")?.focus();
      }
      return;
    }

    const recipes = getRecipes();
    const newId = recipes.length > 0 ? Math.max(...recipes.map((r) => Number(r.id) || 0)) + 1 : 1;

    const newRecipe = {
      id: newId,
      name: name,
      category: category,
      description: description,
      image: imageDataUrl || "",
      ingredients: ingredients,
      instructions: instructions
    };

    recipes.unshift(newRecipe);
    if (!saveRecipes(recipes)) return;
    window.CookShareUI.flash(`Đã lưu công thức "${name}". Cảm ơn bạn đã chia sẻ!`);
    window.location.href = "/Recipe";
  });
}

// ==========================================================================
// 3. EDIT RECIPE CONTROLLER
// ==========================================================================

function initializeEditRecipe() {
  const form = document.getElementById("editRecipeForm");
  if (!form) return;

  const idInput = document.getElementById("editRecipeId");
  let recipeId = idInput ? Number(idInput.value) : 0;

  if (!recipeId) {
    const pathParts = window.location.pathname.split("/").filter(Boolean);
    const lastPart = pathParts[pathParts.length - 1];
    if (lastPart && !isNaN(Number(lastPart))) {
      recipeId = Number(lastPart);
    }
  }

  const recipes = getRecipes();
  const recipe = recipes.find((r) => Number(r.id) === Number(recipeId));

  if (!recipe) {
    window.CookShareUI.flash("Không tìm thấy công thức cần chỉnh sửa.");
    window.location.href = "/Recipe";
    return;
  }

  // Điền dữ liệu hiện tại vào form
  const nameInput = document.getElementById("recipeName");
  const categoryInput = document.getElementById("recipeCategory");
  const descInput = document.getElementById("recipeDescription");
  const ingredientsInput = document.getElementById("recipeIngredients");
  const instructionsInput = document.getElementById("recipeInstructions");

  if (nameInput) nameInput.value = recipe.name || "";
  if (categoryInput) categoryInput.value = recipe.category || "";
  if (descInput) descInput.value = recipe.description || "";
  if (ingredientsInput) ingredientsInput.value = recipe.ingredients || "";
  if (instructionsInput) instructionsInput.value = recipe.instructions || "";

  // Xử lý ảnh
  const imageInput = document.getElementById("recipeImage");
  const imageDropzone = document.getElementById("imageDropzone");
  const imagePreviewBox = document.getElementById("imagePreviewBox");
  let imageDataUrl = recipe.image || "";

  function renderImagePreview(src) {
    if (!imagePreviewBox) return;
    if (!src) {
      imagePreviewBox.innerHTML = "";
      imagePreviewBox.style.display = "none";
      if (imageDropzone) imageDropzone.style.display = "flex";
      return;
    }

    imagePreviewBox.style.display = "block";
    if (imageDropzone) imageDropzone.style.display = "none";

    imagePreviewBox.innerHTML = `
      <div class="image-preview-wrapper">
        <img src="${escapeHtml(recipeImageSource(src))}" alt="${escapeHtml(recipe.name)}">
        <div class="image-preview-overlay">
          <span>✓ Ảnh món ăn hiện tại</span>
          <button type="button" class="btn-remove-image" id="btnRemoveImage">Đổi ảnh khác</button>
        </div>
      </div>
    `;

    const removeBtn = document.getElementById("btnRemoveImage");
    if (removeBtn) {
      removeBtn.addEventListener("click", () => {
        imageDataUrl = "";
        if (imageInput) imageInput.value = "";
        renderImagePreview("");
      });
    }
  }

  if (imageDataUrl) {
    renderImagePreview(imageDataUrl);
  }

  function handleImageFile(file) {
    if (!file) return;
    if (!["image/jpeg", "image/png", "image/webp"].includes(file.type)) {
      window.CookShareUI.toast("Vui lòng chọn ảnh JPG, PNG hoặc WEBP.", "error");
      return;
    }
    if (file.size > 3 * 1024 * 1024) {
      window.CookShareUI.toast("Ảnh tối đa 3 MB. Bạn hãy chọn ảnh nhỏ hơn nhé.", "error");
      return;
    }
    const reader = new FileReader();
    const submitButton = form.querySelector('[type="submit"]');
    if (submitButton) submitButton.disabled = true;
    reader.onerror = () => window.CookShareUI.toast("Chưa thể đọc ảnh. Vui lòng chọn lại.", "error");
    reader.onloadend = () => { if (submitButton) submitButton.disabled = false; };
    reader.onload = function (event) {
      imageDataUrl = event.target.result;
      renderImagePreview(imageDataUrl);
    };
    reader.readAsDataURL(file);
  }

  if (imageInput) {
    imageInput.addEventListener("change", function () {
      if (this.files && this.files[0]) {
        handleImageFile(this.files[0]);
      }
    });
  }

  if (imageDropzone) {
    ["dragenter", "dragover"].forEach((eventName) => {
      imageDropzone.addEventListener(eventName, (e) => {
        e.preventDefault();
        imageDropzone.classList.add("dragover");
      });
    });
    ["dragleave", "drop"].forEach((eventName) => {
      imageDropzone.addEventListener(eventName, (e) => {
        e.preventDefault();
        imageDropzone.classList.remove("dragover");
      });
    });
    imageDropzone.addEventListener("drop", (e) => {
      const files = e.dataTransfer.files;
      if (files && files[0]) {
        handleImageFile(files[0]);
      }
    });
  }

  // Submit Handler
  form.addEventListener("submit", function (e) {
    e.preventDefault();

    const name = nameInput ? nameInput.value.trim() : "";
    const category = categoryInput ? categoryInput.value : "";
    const description = descInput ? descInput.value.trim() : "";
    const ingredients = ingredientsInput ? ingredientsInput.value.trim() : "";
    const instructions = instructionsInput ? instructionsInput.value.trim() : "";

    let hasError = false;

    function validateField(val, errorElemId, message) {
      const errElem = document.getElementById(errorElemId);
      const field = document.querySelector(`[aria-describedby~="${errorElemId}"]`);
      field?.setAttribute("aria-invalid", String(!val));
      if (!val) {
        if (errElem) {
          errElem.textContent = message;
          errElem.classList.add("visible");
        }
        hasError = true;
      } else {
        if (errElem) {
          errElem.textContent = "";
          errElem.classList.remove("visible");
        }
      }
    }

    validateField(name, "nameError", "Vui lòng nhập tên món ăn.");
    validateField(category, "categoryError", "Vui lòng chọn danh mục.");
    validateField(description, "descriptionError", "Vui lòng nhập mô tả món ăn.");
    validateField(ingredients, "ingredientsError", "Vui lòng nhập nguyên liệu.");
    validateField(instructions, "instructionsError", "Vui lòng nhập hướng dẫn chế biến.");

    if (hasError) {
      document.querySelector('.form-error.visible')?.parentElement.querySelector('input, select, textarea')?.focus();
      return;
    }

    const allRecipes = getRecipes();
    const index = allRecipes.findIndex((r) => Number(r.id) === Number(recipeId));

    if (index === -1) {
      window.CookShareUI.toast("Không tìm thấy công thức để cập nhật.", "error");
      return;
    }

    allRecipes[index] = {
      ...allRecipes[index],
      name: name,
      category: category,
      description: description,
      image: imageDataUrl || "",
      ingredients: ingredients,
      instructions: instructions
    };

    if (!saveRecipes(allRecipes)) return;
    window.CookShareUI.flash(`Đã cập nhật công thức "${name}".`);
    window.location.href = `/Recipe/Details/${recipeId}`;
  });
}

// ==========================================================================
// 4. DETAILS RECIPE CONTROLLER
// ==========================================================================

function initializeDetailsRecipe() {
  const container = document.getElementById("recipeDetailsContainer");
  if (!container) return;
  const id = Number(document.getElementById("detailsRecipeId").value);
  const recipe = getRecipes().find(item => Number(item.id) === id);
  if (!recipe) {
    container.innerHTML = `<div class="recipe-empty-container"><span class="state-icon"><svg class="icon"><use href="#i-search" /></svg></span><h1>Không tìm thấy công thức</h1><p>Công thức có thể đã được xóa hoặc đường dẫn chưa chính xác.</p><a class="btn" href="/Recipe">Khám phá món ngon khác</a></div>`;
    return;
  }
  document.getElementById("detailsBreadcrumbCurrent").textContent = recipe.name;
  document.title = `${recipe.name} · CookShare`;
  const ingredients = String(recipe.ingredients || "").split("\n").map(line => line.trim()).filter(Boolean);
  const instructions = String(recipe.instructions || "").split("\n").map(line => line.trim()).filter(Boolean);
  const ingredientHtml = ingredients.map((ingredient, index) => `
    <li class="ingredient-item"><label for="ingredient_${index}"><input type="checkbox" class="ingredient-checkbox" id="ingredient_${index}" /><span>${escapeHtml(ingredient)}</span></label></li>
  `).join("");
  const instructionHtml = instructions.map((step, index) => `
    <div class="instruction-step-item"><span class="step-number">${index + 1}</span><p class="step-text">${escapeHtml(step.replace(/^(Bước\s*\d+[:.-]?\s*)/i, ""))}</p></div>
  `).join("");
  container.innerHTML = `
    <article class="details-hero-card">
      <div class="details-top-bar">
        <span class="details-category-tag"><svg class="icon"><use href="#i-leaf" /></svg>${escapeHtml(recipe.category || "Món ngon")}</span>
        <div class="details-actions">
          <button type="button" class="btn btn-secondary recipe-like-btn" data-recipe-id="${id}" data-recipe-name="${escapeHtml(recipe.name)}" aria-pressed="false" aria-label="Thích ${escapeHtml(recipe.name)}"><span class="like-icon">♡</span><span class="like-text">Thích</span><span class="recipe-like-count-display" data-recipe-id="${id}" data-like-format="number">0</span></button>
          <button type="button" class="btn btn-secondary btn-details-save recipe-save-btn" data-recipe-id="${id}" data-recipe-name="${escapeHtml(recipe.name)}" aria-pressed="false" aria-label="Lưu ${escapeHtml(recipe.name)}"><span class="save-icon"><svg class="icon" aria-hidden="true"><use href="#i-bookmark" /></svg></span><span class="save-text">Lưu công thức</span></button>
        </div>
      </div>
      <h1 class="details-title">${escapeHtml(recipe.name)}</h1>
      <div class="details-meta-row">
        <span class="details-meta-item"><svg class="icon"><use href="#i-user" /></svg> Công thức từ <b>${escapeHtml(recipe.author || "Đầu bếp CookShare")}</b></span>
        <span class="details-meta-item"><svg class="icon"><use href="#i-grid" /></svg> ${ingredients.length} nguyên liệu · ${instructions.length} bước</span>
        <span class="details-meta-item"><svg class="icon"><use href="#i-bookmark" /></svg> Đã lưu: <b id="detailsSavedStatus">Chưa</b></span>
      </div>
      <p class="details-desc-box">${escapeHtml(recipe.description)}</p>
      <div class="details-cover-image"><img src="${escapeHtml(recipeImageSource(recipe.image))}" alt="${escapeHtml(recipe.name)}" width="1100" height="700" fetchpriority="high" /></div>
    </article>
    <div class="details-content-grid">
      <section class="ingredients-card" aria-labelledby="ingredientHeading"><h2 class="section-card-title" id="ingredientHeading"><svg class="icon"><use href="#i-leaf" /></svg> Nguyên liệu</h2><p class="ingredient-progress" role="status" aria-live="polite">Đã chuẩn bị <span id="ingredientCheckedCount">0</span> / ${ingredients.length} nguyên liệu</p><ul class="ingredient-list">${ingredientHtml || '<li>Chưa có thông tin nguyên liệu.</li>'}</ul></section>
      <section class="instructions-card" aria-labelledby="instructionHeading"><h2 class="section-card-title" id="instructionHeading"><svg class="icon"><use href="#i-chef" /></svg> Cùng vào bếp</h2><div class="instruction-steps">${instructionHtml || '<p>Chưa có hướng dẫn chế biến.</p>'}</div></section>
    </div>
    <div class="details-footer-nav"><a class="btn btn-secondary" href="/Recipe">← Khám phá thêm</a><div><a class="btn btn-secondary" href="/Recipe/Edit/${id}"><svg class="icon"><use href="#i-edit" /></svg> Chỉnh sửa</a><button class="btn btn-secondary delete-link" type="button" data-delete-details="${id}"><svg class="icon"><use href="#i-trash" /></svg> Xóa công thức</button></div></div>
  `;
  container.addEventListener("change", event => {
    if (!event.target.matches(".ingredient-checkbox")) return;
    event.target.closest(".ingredient-item").classList.toggle("checked", event.target.checked);
    document.getElementById("ingredientCheckedCount").textContent = container.querySelectorAll(".ingredient-checkbox:checked").length;
  });
  if (typeof initializeRecipeComments === "function") initializeRecipeComments(id);
  syncRecipeCards();
}

async function deleteRecipeInDetails(id) {
  const recipes = getRecipes();
  const recipe = recipes.find(item => Number(item.id) === Number(id));
  if (!recipe) return;
  const confirmed = await window.CookShareUI.confirmDelete(`Công thức "${recipe.name}" sẽ được xóa khỏi thiết bị này. Thao tác này không thể hoàn tác.`);
  if (!confirmed) return;
  if (!saveRecipes(recipes.filter(item => Number(item.id) !== Number(id)))) return;
  window.CookShareUI.flash("Đã xóa công thức.");
  location.href = "/Recipe";
}

function emptyRecipeCollection() {
  return '<div class="recipe-empty-container collection-empty"><span class="state-icon"><svg class="icon"><use href="#i-chef" /></svg></span><h2>Căn bếp đang chờ bạn</h2><p>Chia sẻ một công thức để bắt đầu câu chuyện món ngon.</p><a class="btn" href="/Recipe/Create">Chia sẻ món đầu tiên</a></div>';
}

function initializeHomePage() {
  const featured = document.getElementById("homeFeaturedGrid");
  const latest = document.getElementById("homeLatestGrid");
  if (!featured && !latest) return;
  const recipes = getRecipes();
  if (featured) featured.innerHTML = recipes.length ? recipes.slice(0, 3).map(recipe => renderRecipeCard(recipe)).join("") : emptyRecipeCollection();
  if (latest) latest.innerHTML = recipes.length ? (recipes.length > 3 ? recipes.slice(3, 6) : recipes).map(recipe => renderRecipeCard(recipe)).join("") : emptyRecipeCollection();
  const suggestion = document.querySelector(".hero-recipe-note");
  if (suggestion && !recipes.some(recipe => Number(recipe.id) === 4)) {
    suggestion.href = "/Recipe";
    suggestion.querySelector("strong").textContent = "Tìm món ngon của bạn";
  }
  syncRecipeCards();
}

function initializeProfilePage() {
  const grid = document.getElementById("profileRecipeGrid");
  if (!grid) return;
  const recipes = getRecipes();
  document.getElementById("profileRecipeCount").textContent = recipes.length;
  grid.innerHTML = recipes.length ? recipes.map(recipe => renderRecipeCard(recipe, { manage: true })).join("") : emptyRecipeCollection();
  syncRecipeCards();
}

document.addEventListener("click", event => {
  const button = event.target.closest("[data-delete-recipe], [data-delete-details]");
  if (!button) return;
  if (button.dataset.deleteDetails) deleteRecipeInDetails(Number(button.dataset.deleteDetails));
  else deleteRecipe(Number(button.dataset.deleteRecipe));
});

document.addEventListener("DOMContentLoaded", function () {
  initializeHomePage();
  initializeIndexPage();
  initializeCreateRecipe();
  initializeEditRecipe();
  initializeDetailsRecipe();
  initializeProfilePage();
});
