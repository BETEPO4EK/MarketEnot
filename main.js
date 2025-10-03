const API_URL = 'https://88aa94f4bf72.ngrok-free.app/api';
        let tg = window.Telegram.WebApp;
        let cart = [];
        let products = [];
        let activeOrder = null;

        tg.ready();
        tg.expand();

        async function loadProducts() {
            try {
                const response = await fetch(`${API_URL}/products`, {
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

        async function loadActiveOrder() {
            try {
                const userId = tg.initDataUnsafe.user?.id || 464350533;
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
                        <span class="text-xl font-bold text-orange-400">${product.price}₽</span>
                        <span class="text-xs text-gray-500 bg-white/5 px-2 py-1 rounded-full">Осталось: ${product.stock}</span>
                    </div>
                </div>
            `).join('');
        }

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

            document.getElementById('productsScreen').style.display = 'none';
            document.getElementById('cartScreen').style.display = 'block';
            renderCart();
        }

        function renderCart() {
            const container = document.getElementById('cartItems');
            let total = 0;

            container.innerHTML = cart.map(item => {
                const subtotal = item.product.price * item.quantity;
                total += subtotal;
                return `
                    <div class="glass rounded-xl p-4 flex items-center justify-between">
                        <div class="flex-1">
                            <h3 class="font-bold text-lg">${item.product.name}</h3>
                            <p class="text-sm text-gray-400 mt-1">${item.product.price}₽ × ${item.quantity} = ${subtotal}₽</p>
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
                showProducts();
            } else {
                renderCart();
            }
            updateCartBadge();
        }

        function showProducts() {
            document.getElementById('productsScreen').style.display = 'block';
            document.getElementById('cartScreen').style.display = 'none';
            document.getElementById('checkoutScreen').style.display = 'none';
        }

        function showCheckout() {
            document.getElementById('cartScreen').style.display = 'none';
            document.getElementById('checkoutScreen').style.display = 'block';
        }

        document.getElementById('checkoutForm').addEventListener('submit', async (e) => {
            e.preventDefault();

            const phone = document.getElementById('phone').value;
            const address = document.getElementById('address').value;
            const comment = document.getElementById('comment').value;

            const orderData = {
                telegramId: tg.initDataUnsafe.user?.id || 464350533,
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
                        showProducts();
                    }, 1500);
                } else {
                    tg.showAlert('❌ Ошибка: ' + result.error);
                }
            } catch (error) {
                tg.showAlert('❌ Ошибка отправки заказа');
                console.error(error);
            }
        });

        loadProducts();