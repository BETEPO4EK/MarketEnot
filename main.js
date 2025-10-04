const API_URL = 'https://88aa94f4bf72.ngrok-free.app/api';
const ADMIN_ID = 464350533; // ЗАМЕНИ НА СВОЙ TELEGRAM ID

let tg = window.Telegram.WebApp;
let cart = [];
let products = [];
let categories = [];
let activeOrder = null;
let currentTab = 'all';
let isAdmin = false;

// Инициализация
tg.ready();
tg.expand();

// Проверка админа
const userId = tg.initDataUnsafe.user?.id || ADMIN_ID;
isAdmin = (userId === ADMIN_ID);

if (isAdmin) {
    document.getElementById('adminButton').style.display = 'block';
}

// === ЗАГРУЗКА ДАННЫХ ===

async function loadProducts() {
    try {
        const response = await fetch(`${API_URL}/products/discounts`, {
            headers: {
                'ngrok-skip-browser-warning': 'true',
                'User-Agent': 'TelegramBot'
            }
        });
        const data = await response.json();
        products = data.data;
        renderProducts();
        await loadActiveOrder();
    } catch (error) {
        console.error('Ошибка загрузки товаров:', error);
        document.getElementById('productsList').innerHTML = '<p class="col-span-2 text-center text-red-400">Ошибка загрузки товаров</p>';
    }
}

async function loadCategories() {
    try {
        const response = await fetch(`${API_URL}/categories`, {
            headers: {
                'ngrok-skip-browser-warning': 'true',
                'User-Agent': 'TelegramBot'
            }
        });
        const data = await response.json();
        categories = data.data;
        renderCategories();
        updateCategorySelect();
    } catch (error) {
        console.error('Ошибка загрузки категорий:', error);
    }
}

async function loadActiveOrder() {
    try {
        const response = await fetch(`${API_URL}/orders/${userId}`, {
            headers: {
                'ngrok-skip-browser-warning': 'true',
                'User-Agent': 'TelegramBot'
            }
        });
        const data = await response.json();
        
        if (data.success && data.data.length > 0) {
            activeOrder = data.data.find(o => o.status !== 'completed' && o.status !== 'cancelled');
            if (activeOrder) {
                showActiveOrderBanner(activeOrder);
            }
        }
    } catch (error) {
        console.error('Ошибка загрузки заказов:', error);
    }
}

// === ВКЛАДКИ ===

function showTab(tab) {
    currentTab = tab;
    document.getElementById('tabAll').classList.toggle('active', tab === 'all');
    document.getElementById('tabCategories').classList.toggle('active', tab === 'categories');
    
    document.getElementById('allProductsTab').style.display = tab === 'all' ? 'block' : 'none';
    document.getElementById('categoriesTab').style.display = tab === 'categories' ? 'block' : 'none';
    document.getElementById('categoryProductsView').style.display = 'none';

    if (tab === 'categories' && categories.length === 0) {
        loadCategories();
    }
}

// === ОТРИСОВКА ===

function renderProducts() {
    const container = document.getElementById('productsList');
    if (products.length === 0) {
        container.innerHTML = '<p class="col-span-2 text-center text-gray-400 py-8">Товаров пока нет</p>';
        return;
    }
    container.innerHTML = products.map((product, index) => `
        <div class="product-card glass rounded-xl p-4 cursor-pointer" onclick="addToCart(${product.id})" style="animation-delay: ${index * 0.1}s">
            ${product.imageUrl ? `<img src="${product.imageUrl}" class="w-full h-40 object-cover rounded-lg mb-3">` : '<div class="w-full h-40 bg-gradient-to-br from-orange-500/20 to-orange-600/20 rounded-lg mb-3 flex items-center justify-center text-5xl">📦</div>'}
            <h3 class="font-bold text-lg mb-1">${product.name}</h3>
            <p class="text-sm text-gray-400 mb-3 line-clamp-2">${product.description || ''}</p>
            <div class="flex justify-between items-center">
                ${product.discountPercent > 0 ? `
                    <div>
                        <span class="text-sm text-gray-500 line-through">${product.price}₽</span>
                        <span class="text-xl font-bold text-orange-400 ml-2">${product.finalPrice}₽</span>
                        <span class="text-xs bg-red-500 text-white px-2 py-1 rounded-full ml-1">-${product.discountPercent}%</span>
                    </div>
                ` : `
                    <span class="text-xl font-bold text-orange-400">${product.price}₽</span>
                `}
                <span class="text-xs text-gray-500 bg-white/5 px-2 py-1 rounded-full">Осталось: ${product.stock}</span>
            </div>
        </div>
    `).join('');
}

function renderCategories() {
    const container = document.getElementById('categoriesList');
    if (categories.length === 0) {
        container.innerHTML = '<p class="col-span-2 text-center text-gray-400 py-8">Категорий пока нет</p>';
        return;
    }
    container.innerHTML = categories.map((category, index) => `
        <div class="category-card glass rounded-xl p-6 cursor-pointer flex flex-col items-center justify-center" onclick="showCategoryProducts(${category.id}, '${category.name}')" style="animation-delay: ${index * 0.1}s">
            <div class="text-4xl mb-3">📁
            </div>
            <h3 class="font-bold text-lg">${category.name}</h3>
            ${category.description ? `<p class="text-sm text-gray-400 mt-1">${category.description}</p>` : ''}
        </div>
    `).join('');
}

async function showCategoryProducts(categoryId, categoryName) {
    try {
        const response = await fetch(`${API_URL}/categories/${categoryId}/products`, {
            headers: {
                'ngrok-skip-browser-warning': 'true',
                'User-Agent': 'TelegramBot'
            }
        });
        const data = await response.json();
        
        document.getElementById('categoriesTab').style.display = 'none';
        document.getElementById('categoryProductsView').style.display = 'block';
        document.getElementById('categoryTitle').textContent = categoryName;
        
        const container = document.getElementById('categoryProductsList');
        if (data.data.length === 0) {
            container.innerHTML = '<p class="col-span-2 text-center text-gray-400 py-8">В этой категории пока нет товаров</p>';
            return;
        }
        
        container.innerHTML = data.data.map((product, index) => `
            <div class="product-card glass rounded-xl p-4 cursor-pointer" onclick="addToCart(${product.id})" style="animation-delay: ${index * 0.1}s">
                ${product.imageUrl ? `<img src="${product.imageUrl}" class="w-full h-40 object-cover rounded-lg mb-3">` : '<div class="w-full h-40 bg-gradient-to-br from-orange-500/20 to-orange-600/20 rounded-lg mb-3 flex items-center justify-center text-5xl">📦</div>'}
                <h3 class="font-bold text-lg mb-1">${product.name}</h3>
                <p class="text-sm text-gray-400 mb-3 line-clamp-2">${product.description || ''}</p>
                <div class="flex justify-between items-center">
                    <span class="text-xl font-bold text-orange-400">${product.price}₽</span>
                    <span class="text-xs text-gray-500 bg-white/5 px-2 py-1 rounded-full">Осталось: ${product.stock}</span>
                </div>
            </div>
        `).join('');
    } catch (error) {
        console.error('Ошибка загрузки товаров категории:', error);
    }
}

function backToCategories() {
    document.getElementById('categoryProductsView').style.display = 'none';
    document.getElementById('categoriesTab').style.display = 'block';
}

// === КОРЗИНА ===

function addToCart(productId) {
    const product = products.find(p => p.id === productId);
    const cartItem = cart.find(item => item.productId === productId);

    if (cartItem) {
        if (cartItem.quantity < product.stock) {
            cartItem.quantity++;
        }
    } else {
        cart.push({ productId, quantity: 1, product });
    }

    updateCartBadge();
    tg.HapticFeedback.impactOccurred('light');
}

function updateCartBadge() {
    const totalItems = cart.reduce((sum, item) => sum + item.quantity, 0);
    document.getElementById('cartCount').textContent = totalItems;
    document.getElementById('cartBadge').style.display = totalItems > 0 ? 'flex' : 'none';
}

function showCart() {
    if (cart.length === 0) {
        tg.showAlert('Корзина пуста');
        return;
    }

    document.getElementById('mainScreen').style.display = 'none';
    document.getElementById('cartScreen').style.display = 'block';
    renderCart();
}

function renderCart() {
    const container = document.getElementById('cartItems');
    let total = 0;

    container.innerHTML = cart.map(item => {
        const price = item.product.finalPrice || item.product.price;
        const subtotal = price * item.quantity;
        total += subtotal;
        return `
            <div class="glass rounded-xl p-4 flex items-center justify-between">
                <div class="flex-1">
                    <h3 class="font-bold text-lg">${item.product.name}</h3>
                    <p class="text-sm text-gray-400 mt-1">${price}₽ × ${item.quantity} = ${subtotal}₽</p>
                </div>
                <div class="flex items-center gap-3">
                    <button onclick="changeQuantity(${item.productId}, -1)" class="qty-btn w-10 h-10 rounded-lg font-bold text-white">−</button>
                    <span class="font-bold text-xl text-orange-400 w-8 text-center">${item.quantity}</span>
                    <button onclick="changeQuantity(${item.productId}, 1)" class="qty-btn w-10 h-10 rounded-lg font-bold text-white">+</button>
                    <button onclick="removeFromCart(${item.productId})" class="text-red-400 ml-2 text-2xl hover:scale-110 transition-transform">🗑️</button>
                </div>
            </div>
        `;
    }).join('');

    document.getElementById('totalPrice').textContent = `${total}₽`;
}

function changeQuantity(productId, delta) {
    const cartItem = cart.find(item => item.productId === productId);
    const product = products.find(p => p.id === productId);

    if (cartItem) {
        cartItem.quantity += delta;
        if (cartItem.quantity <= 0) {
            removeFromCart(productId);
        } else if (cartItem.quantity > product.stock) {
            cartItem.quantity = product.stock;
            tg.showAlert('Недостаточно товара на складе');
        }
        renderCart();
        updateCartBadge();
    }
}

function removeFromCart(productId) {
    cart = cart.filter(item => item.productId !== productId);
    if (cart.length === 0) {
        showMain();
    } else {
        renderCart();
    }
    updateCartBadge();
}

// === НАВИГАЦИЯ ===

function showMain() {
    document.getElementById('mainScreen').style.display = 'block';
    document.getElementById('cartScreen').style.display = 'none';
    document.getElementById('checkoutScreen').style.display = 'none';
}

function showCheckout() {
    document.getElementById('cartScreen').style.display = 'none';
    document.getElementById('checkoutScreen').style.display = 'block';
}

function showActiveOrderBanner(order) {
    const banner = document.getElementById('activeOrderBanner');
    document.getElementById('orderNumber').textContent = order.id;
    document.getElementById('orderStatus').textContent = getStatusText(order.status);
    document.getElementById('orderTotal').textContent = order.totalPrice;
    banner.style.display = 'block';
}

function closeOrderBanner() {
    document.getElementById('activeOrderBanner').style.display = 'none';
}

function getStatusText(status) {
    const statuses = {
        'pending': '⏳ Ожидает оплаты',
        'paid': '✅ Оплачен',
        'confirmed': '📦 Готовится к отправке',
        'shipped': '🚚 Передан в доставку',
        'completed': '✅ Получен',
        'cancelled': '❌ Отменён'
    };
    return statuses[status] || status;
}

// === ОФОРМЛЕНИЕ ЗАКАЗА ===

document.getElementById('checkoutForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const phone = document.getElementById('phone').value;
    const address = document.getElementById('address').value;
    const comment = document.getElementById('comment').value;

    const orderData = {
        telegramId: userId,
        phone,
        address,
        comment,
        items: cart.map(item => ({
            productId: item.productId,
            quantity: item.quantity
        }))
    };

    try {
        const response = await fetch(`${API_URL}/orders`, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'ngrok-skip-browser-warning': 'true',
                'User-Agent': 'TelegramBot'
            },
            body: JSON.stringify(orderData)
        });

        const result = await response.json();

        if (result.success) {
            tg.showAlert('✅ Заказ оформлен! Вам придёт сообщение с реквизитами для оплаты.');
            cart = [];
            updateCartBadge();
            
            activeOrder = result.data.order;
            showActiveOrderBanner(activeOrder);
            
            setTimeout(() => {
                showMain();
            }, 1500);
        } else {
            tg.showAlert('❌ Ошибка: ' + result.error);
        }
    } catch (error) {
        tg.showAlert('❌ Ошибка отправки заказа');
        console.error(error);
    }
});

// === АДМИН-ПАНЕЛЬ ===

function toggleAdminPanel() {
    const panel = document.getElementById('adminPanel');
    panel.style.display = panel.style.display === 'none' ? 'block' : 'none';
}

function showAdminTab(tab) {
    document.getElementById('adminTabProduct').classList.toggle('active', tab === 'product');
    document.getElementById('adminTabCategory').classList.toggle('active', tab === 'category');
    document.getElementById('addProductForm').style.display = tab === 'product' ? 'block' : 'none';
    document.getElementById('addCategoryForm').style.display = tab === 'category' ? 'block' : 'none';
}

function updateCategorySelect() {
    const select = document.getElementById('productCategory');
    select.innerHTML = '<option value="">Без категории</option>' + 
        categories.map(cat => `<option value="${cat.id}">${cat.name}</option>`).join('');
}

// Добавление товара
document.getElementById('addProductForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const productData = {
        name: document.getElementById('productName').value,
        description: document.getElementById('productDesc').value,
        imageUrl: document.getElementById('productImage').value || null,
        categoryId: parseInt(document.getElementById('productCategory').value) || null,
        price: parseFloat(document.getElementById('productPrice').value),
        stock: parseInt(document.getElementById('productStock').value)
    };

    try {
        const response = await fetch(`${API_URL}/products`, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'ngrok-skip-browser-warning': 'true'
            },
            body: JSON.stringify(productData)
        });

        const result = await response.json();

        if (result.success) {
            tg.showAlert('✅ Товар добавлен!');
            document.getElementById('addProductForm').reset();
            toggleAdminPanel();
            loadProducts();
        } else {
            tg.showAlert('❌ Ошибка: ' + result.error);
        }
    } catch (error) {
        tg.showAlert('❌ Ошибка добавления товара');
        console.error(error);
    }
});

// Добавление категории
document.getElementById('addCategoryForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const categoryData = {
        name: document.getElementById('categoryName').value,
        description: document.getElementById('categoryDesc').value || null
    };

    try {
        const response = await fetch(`${API_URL}/categories`, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'ngrok-skip-browser-warning': 'true'
            },
            body: JSON.stringify(categoryData)
        });

        const result = await response.json();

        if (result.success) {
            tg.showAlert('✅ Категория добавлена!');
            document.getElementById('addCategoryForm').reset();
            toggleAdminPanel();
            loadCategories();
        } else {
            tg.showAlert('❌ Ошибка: ' + result.error);
        }
    } catch (error) {
        tg.showAlert('❌ Ошибка добавления категории');
        console.error(error);
    }
});

// Загружаем данные при старте
loadProducts();