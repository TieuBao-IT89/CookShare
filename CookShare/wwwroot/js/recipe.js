/**
 * COOKSHARE - RECIPE MANAGEMENT MODULE
 * Quản lý dữ liệu công thức nấu ăn bằng localStorage
 */

const RECIPE_STORAGE_KEY = "cookshare_recipes";

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

function getRecipes() {
  const data = localStorage.getItem(RECIPE_STORAGE_KEY);

  if (data === null) {
    // Nếu chưa từng có trong localStorage, khởi tạo dữ liệu mẫu mặc định
    saveRecipes(DEFAULT_SAMPLE_RECIPES);
    return DEFAULT_SAMPLE_RECIPES;
  }

  try {
    const parsed = JSON.parse(data);
    return Array.isArray(parsed) ? parsed : [];
  } catch (err) {
    console.error("Lỗi parse dữ liệu recipe từ localStorage:", err);
    return [];
  }
}

function saveRecipes(recipes) {
  try {
    localStorage.setItem(RECIPE_STORAGE_KEY, JSON.stringify(recipes));
  } catch (err) {
    console.error("Lỗi ghi dữ liệu recipe vào localStorage:", err);
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

  let searchText = "";
  if (searchInput && searchInput.value) {
    searchText = searchInput.value.toLowerCase().trim();
  } else if (headerSearch && headerSearch.value) {
    searchText = headerSearch.value.toLowerCase().trim();
  }

  const category = currentSelectedCategory || (categoryFilter ? categoryFilter.value : "");

  const recipes = getRecipes();

  const filteredRecipes = recipes.filter((recipe) => {
    const nameMatch = recipe.name && recipe.name.toLowerCase().includes(searchText);
    const descMatch = recipe.description && recipe.description.toLowerCase().includes(searchText);
    const ingredientMatch = recipe.ingredients && recipe.ingredients.toLowerCase().includes(searchText);
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

  filteredRecipes.forEach((recipe) => {
    const card = document.createElement("article");
    card.className = "recipe-card";

    const imageHtml = recipe.image
      ? `<img src="${recipe.image}" alt="${escapeHtml(recipe.name)}" loading="lazy">`
      : `<div class="recipe-no-image"><span class="recipe-no-image-icon">🍳</span><span class="recipe-no-image-text">CookShare Recipe</span></div>`;

    card.innerHTML = `
      <div class="recipe-image">
        ${imageHtml}
        <span class="recipe-category-badge">${escapeHtml(recipe.category || "Công thức")}</span>
      </div>
      <div class="recipe-card-body">
        <h3 class="recipe-card-title">
          <a href="/Recipe/Details/${recipe.id}" title="${escapeHtml(recipe.name)}">${escapeHtml(recipe.name)}</a>
        </h3>
        <p class="recipe-card-desc">
          ${escapeHtml(recipe.description || "Công thức món ngon hấp dẫn từ cộng đồng CookShare...")}
        </p>
        <div class="recipe-card-footer">
          <div class="recipe-actions">
            <a href="/Recipe/Details/${recipe.id}" class="btn-action-view" title="Xem chi tiết">
              <span>👁 Xem</span>
            </a>
            <a href="/Recipe/Edit/${recipe.id}" class="btn-action-edit" title="Chỉnh sửa">
              <span>✏ Sửa</span>
            </a>
            <button type="button" class="btn-action-delete" onclick="deleteRecipe(${recipe.id})" title="Xóa công thức">
              <span>🗑 Xóa</span>
            </button>
          </div>
        </div>
      </div>
    `;

    recipeList.appendChild(card);
  });
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

  if (buttonElem) {
    buttonElem.classList.add("active");
  }

  displayRecipes();
}

function deleteRecipe(id) {
  const recipes = getRecipes();
  const recipe = recipes.find((r) => Number(r.id) === Number(id));

  if (!recipe) {
    alert("Không tìm thấy công thức cần xóa.");
    return;
  }

  const confirmed = confirm(`Bạn có chắc chắn muốn xóa công thức "${recipe.name}" không? Thao tác này không thể hoàn tác.`);
  if (!confirmed) return;

  const newRecipes = recipes.filter((r) => Number(r.id) !== Number(id));
  saveRecipes(newRecipes);

  displayRecipes();
}

function initializeIndexPage() {
  const recipeList = document.getElementById("recipeList");
  if (!recipeList) return;

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

    if (!file.type.startsWith("image/")) {
      alert("Vui lòng chọn file định dạng hình ảnh (JPG, PNG, WEBP).");
      return;
    }

    if (file.size > 3 * 1024 * 1024) {
      alert("Dung lượng ảnh tối đa là 3MB. Vui lòng chọn ảnh nhỏ hơn.");
      return;
    }

    const reader = new FileReader();
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
        <img src="${src}" alt="Ảnh xem trước">
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
        firstError.scrollIntoView({ behavior: "smooth", block: "center" });
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
    saveRecipes(recipes);

    alert(`🎉 Chúc mừng! Công thức "${name}" đã được lưu thành công vào CookShare!`);
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
    alert("Không tìm thấy công thức cần chỉnh sửa.");
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
        <img src="${src}" alt="${escapeHtml(recipe.name)}">
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
    if (!file.type.startsWith("image/")) {
      alert("Vui lòng chọn file hình ảnh (JPG, PNG, WEBP).");
      return;
    }
    if (file.size > 3 * 1024 * 1024) {
      alert("Dung lượng ảnh tối đa là 3MB.");
      return;
    }
    const reader = new FileReader();
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

    if (hasError) return;

    const allRecipes = getRecipes();
    const index = allRecipes.findIndex((r) => Number(r.id) === Number(recipeId));

    if (index === -1) {
      alert("Không tìm thấy công thức để cập nhật.");
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

    saveRecipes(allRecipes);

    alert(`✓ Cập nhật công thức "${name}" thành công!`);
    window.location.href = `/Recipe/Details/${recipeId}`;
  });
}

// ==========================================================================
// 4. DETAILS RECIPE CONTROLLER
// ==========================================================================

function initializeDetailsRecipe() {
  const detailsContainer = document.getElementById("recipeDetailsContainer");
  if (!detailsContainer) return;

  const idInput = document.getElementById("detailsRecipeId");
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
    detailsContainer.innerHTML = `
      <div class="recipe-empty-container">
        <div class="recipe-empty-icon-box">🔍</div>
        <h2 class="recipe-empty-title">Không tìm thấy công thức</h2>
        <p class="recipe-empty-desc">Công thức này có thể đã bị xóa hoặc đường dẫn không chính xác.</p>
        <a href="/Recipe" class="btn-primary-cookshare">← Quay lại danh sách công thức</a>
      </div>
    `;
    return;
  }

  // Cập nhật Breadcrumb & Title trang
  const breadcrumbCurrent = document.getElementById("detailsBreadcrumbCurrent");
  if (breadcrumbCurrent) breadcrumbCurrent.textContent = recipe.name;
  document.title = `${recipe.name} - CookShare`;

  // Tách dòng nguyên liệu
  const ingredientsLines = (recipe.ingredients || "")
    .split("\n")
    .map((line) => line.trim())
    .filter((line) => line.length > 0);

  const ingredientsHtml = ingredientsLines.length > 0
    ? ingredientsLines
        .map((ing, idx) => `
          <li class="ingredient-item" onclick="this.classList.toggle('checked')">
            <input type="checkbox" class="ingredient-checkbox" id="ing_${idx}">
            <label for="ing_${idx}">${escapeHtml(ing)}</label>
          </li>
        `)
        .join("")
    : `<li class="ingredient-item">Chưa có thông tin nguyên liệu</li>`;

  // Tách dòng hướng dẫn chế biến
  const instructionLines = (recipe.instructions || "")
    .split("\n")
    .map((line) => line.trim())
    .filter((line) => line.length > 0);

  const instructionsHtml = instructionLines.length > 0
    ? instructionLines
        .map((step, idx) => {
          let cleanStep = step;
          // Tự loại bỏ tiền tố Bước X: nếu đã có sẵn trong chuỗi
          cleanStep = cleanStep.replace(/^(Bước\s*\d+[:.-]?\s*)/i, "");
          return `
            <div class="instruction-step-item">
              <div class="step-number">${idx + 1}</div>
              <p class="step-text">${escapeHtml(cleanStep)}</p>
            </div>
          `;
        })
        .join("")
    : `<p class="step-text">Chưa có hướng dẫn chế biến.</p>`;

  const imageHtml = recipe.image
    ? `<div class="details-cover-image"><img src="${recipe.image}" alt="${escapeHtml(recipe.name)}"></div>`
    : ``;

  detailsContainer.innerHTML = `
    <!-- Top Details Hero -->
    <article class="details-hero-card">
      <div class="details-top-bar">
        <span class="details-category-tag">🏷 ${escapeHtml(recipe.category || "Món ăn")}</span>
        <div class="details-actions">
          <a href="/Recipe/Edit/${recipe.id}" class="btn-action-edit" style="padding: 9px 16px; font-size: 13px;">
            ✏ Chỉnh sửa công thức
          </a>
          <button type="button" class="btn-action-delete" style="padding: 9px 16px; font-size: 13px;" onclick="deleteRecipeInDetails(${recipe.id})">
            🗑 Xóa
          </button>
        </div>
      </div>

      <h1 class="details-title">${escapeHtml(recipe.name)}</h1>

      <div class="details-meta-row">
        <div class="details-meta-item">
          <span>👨‍🍳 Tác giả:</span> <b>Đầu bếp CookShare</b>
        </div>
        <div class="details-meta-item">
          <span>⏱ Chuẩn bị:</span> <b>Dễ thực hiện</b>
        </div>
        <div class="details-meta-item">
          <span>★ Đánh giá:</span> <b>5.0 / 5.0</b>
        </div>
      </div>

      ${recipe.description ? `<div class="details-desc-box">${escapeHtml(recipe.description)}</div>` : ""}

      ${imageHtml}
    </article>

    <!-- Two Columns: Ingredients & Instructions -->
    <div class="details-content-grid">
      <!-- Cột Nguyên liệu -->
      <section class="ingredients-card">
        <h2 class="section-card-title">
          <span class="title-icon">🛒</span> Nguyên liệu chuẩn bị
        </h2>
        <ul class="ingredient-list">
          ${ingredientsHtml}
        </ul>
      </section>

      <!-- Cột Hướng dẫn chế biến -->
      <section class="instructions-card">
        <h2 class="section-card-title">
          <span class="title-icon">👩‍🍳</span> Các bước thực hiện
        </h2>
        <div class="instruction-steps">
          ${instructionsHtml}
        </div>
      </section>
    </div>

    <!-- Bottom Navigation Bar -->
    <div class="details-footer-nav">
      <a href="/Recipe" class="btn-secondary-cookshare">
        ← Quay lại danh sách món ăn
      </a>
      <a href="/Recipe/Create" class="btn-primary-cookshare">
        + Chia sẻ công thức của bạn
      </a>
    </div>
  `;
}

function deleteRecipeInDetails(id) {
  const recipes = getRecipes();
  const recipe = recipes.find((r) => Number(r.id) === Number(id));

  if (!recipe) return;

  const confirmed = confirm(`Bạn có chắc muốn xóa công thức "${recipe.name}" không?`);
  if (!confirmed) return;

  const newRecipes = recipes.filter((r) => Number(r.id) !== Number(id));
  saveRecipes(newRecipes);

  alert("Đã xóa công thức thành công!");
  window.location.href = "/Recipe";
}

// ==========================================================================
// AUTO INITIALIZATION BASED ON PAGE DOM
// ==========================================================================

document.addEventListener("DOMContentLoaded", function () {
  initializeIndexPage();
  initializeCreateRecipe();
  initializeEditRecipe();
  initializeDetailsRecipe();
});
