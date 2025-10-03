using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;              // <-- ДОБАВЬ ЭТУ СТРОКУ
using Telegram.Bot.Types;

namespace ShopBot.Controllers;

[ApiController]
[Route("api")]
public class ShopController : ControllerBase
{
    private readonly DatabaseService _db;

    public ShopController(DatabaseService db)
    {
        _db = db;
    }

    // Получить все товары
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        try
        {
            var products = await _db.GetAllProducts();
            return Ok(new { success = true, data = products });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    // Получить товар по ID
    [HttpGet("products/{id}")]
    public async Task<IActionResult> GetProduct(int id)
    {
        try
        {
            var product = await _db.GetProductById(id);
            if (product == null)
                return NotFound(new { success = false, error = "Товар не найден" });

            return Ok(new { success = true, data = product });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    // Создать заказ
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            await _db.GetOrCreateUser(request.TelegramId, null, null, null);
            // Проверяем что все товары существуют и доступны
            decimal totalPrice = 0;
            var orderItems = new List<OrderItem>();

            foreach (var item in request.Items)
            {
                var product = await _db.GetProductById(item.ProductId);
                if (product == null || !product.IsAvailable)
                    return BadRequest(new { success = false, error = $"Товар {item.ProductId} недоступен" });

                if (product.Stock < item.Quantity)
                    return BadRequest(new { success = false, error = $"Недостаточно {product.Name} на складе" });

                totalPrice += product.Price * item.Quantity;
                orderItems.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = product.Price
                });
            }

            // Создаём заказ
            var orderId = await _db.CreateOrder(
                request.TelegramId,
                totalPrice,
                request.Phone,
                request.Address,
                request.Comment
            );

            // Добавляем товары в заказ
            await _db.AddOrderItems(orderId, orderItems);

            // Получаем полную информацию о заказе
            var order = await _db.GetOrderById(orderId);
            var items = await _db.GetOrderItems(orderId);

            var bot = HttpContext.RequestServices.GetRequiredService<ITelegramBotClient>();
            await SendOrderNotification(bot, orderId, order!, items);

            await SendOrderConfirmationToUser(bot, request.TelegramId, orderId, order!);

            return Ok(new
            {
                success = true,
                data = new
                {
                    orderId = orderId,
                    order = order,
                    items = items
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    // Получить заказы пользователя
    [HttpGet("orders/{telegramId}")]
    public async Task<IActionResult> GetUserOrders(long telegramId)
    {
        try
        {
            var orders = await _db.GetUserOrders(telegramId);
            return Ok(new { success = true, data = orders });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    private async Task SendOrderNotification(ITelegramBotClient bot, int orderId, Order order, List<OrderItemDetail> items)
    {
        long ADMIN_CHAT_ID = 464350533; // <-- ЗАМЕНИ НА СВОЙ CHAT ID!

        string message = $"🔔 НОВЫЙ ЗАКАЗ #{orderId}\n\n";
        message += $"💰 Сумма: {order.TotalPrice}₽\n";
        message += $"📞 Телефон: {order.PhoneNumber}\n";
        message += $"📍 Адрес: {order.DeliveryAddress}\n";

        if (!string.IsNullOrEmpty(order.Comment))
            message += $"💬 Комментарий: {order.Comment}\n";

        message += "\n📦 Товары:\n";
        foreach (var item in items)
        {
            message += $"• {item.ProductName} x{item.Quantity} = {item.Price * item.Quantity}₽\n";
        }

        message += "\n💳 Реквизиты для оплаты СБП:\n";
        message += "+7 (987) 759-66-43"; // <-- ЗАМЕНИ НА СВОЙ НОМЕР

        try
        {
            await bot.SendMessage(ADMIN_CHAT_ID, message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка отправки уведомления: {ex.Message}");
        }
    }
    
    private async Task SendOrderConfirmationToUser(ITelegramBotClient bot, long userId, int orderId, Order order)
    {
        string message = $"✅ Заказ #{orderId} оформлен!\n\n";
        message += $"💰 Сумма: {order.TotalPrice}₽\n";
        message += $"📦 Статус: ⏳ Ожидает оплаты\n\n";
        message += $"💳 Реквизиты для оплаты СБП:\n";
        message += $"+7 (XXX) XXX-XX-XX\n\n"; // <-- ЗАМЕНИ НА СВОЙ НОМЕР
        message += $"После оплаты отправьте скриншот сюда или свяжитесь с продавцом.";

        try
        {
            await bot.SendMessage(userId, message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка отправки подтверждения юзеру: {ex.Message}");
        }
    }
}


// Модели запросов
public class CreateOrderRequest
{
    public long TelegramId { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Comment { get; set; }
    public List<OrderItemRequest> Items { get; set; } = new();
}

public class OrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

