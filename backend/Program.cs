using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

var builder = WebApplication.CreateBuilder(args);

// Добавляем сервисы
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS для фронта
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Регистрируем DatabaseService как Singleton
builder.Services.AddSingleton<DatabaseService>(sp =>
{
    return new DatabaseService(
        host: "aws-1-eu-north-1.pooler.supabase.com",
        database: "postgres",
        user: "postgres.zmxtwdciwvwamwlcdlsb",
        password: "Egorik40Pik2003",
        port: 6543
    );
});

// Регистрируем Telegram бота
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    return new TelegramBotClient("8437419834:AAGXtz9YqE8Pan-VOXx11QgWbjbtoLumYbU");
});

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseRouting();
app.MapControllers();

// Запускаем Telegram бота в фоне
var bot = app.Services.GetRequiredService<ITelegramBotClient>();
var db = app.Services.GetRequiredService<DatabaseService>();

var host = new Host(bot, db);
host.Start();

Console.WriteLine("🚀 API запущен на http://localhost:5000");
Console.WriteLine("🤖 Telegram бот запущен!");

app.Run("http://localhost:80");

// Класс для бота
public class Host
{
    private readonly ITelegramBotClient _bot;
    private readonly DatabaseService _db;

    private static readonly long[] ADMIN_IDS = { 464350533, 123456789 };

    private Dictionary<long, string> adminStates = new();

    public Host(ITelegramBotClient bot, DatabaseService db)
    {
        _bot = bot;
        _db = db;
    }

    public void Start()
    {
        _bot.StartReceiving(UpdateHandler, ErrorHandler);
    }

    private async Task ErrorHandler(ITelegramBotClient client, Exception exception, CancellationToken token)
    {
        Console.WriteLine("❌ Ошибка: " + exception.Message);
        await Task.CompletedTask;
    }

    private async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
    {
        if (update.Message == null) return;

        Console.WriteLine($"💬 Сообщение: {update.Message.Text ?? "Не текст"}");

        string command = update.Message.Text ?? "";
        long chatId = update.Message.Chat.Id;
        
        //long ADMIN_ID = 464350533; // <-- получи через /myid
        bool isAdmin = ADMIN_IDS.Contains(chatId);

        switch (command)
        {
            case "/start":
                await client.SendMessage(
                    chatId: chatId,
                    text: "🛒 Добро пожаловать в магазин!\n\nОткрой Mini App чтобы посмотреть товары 👇",
                    replyMarkup: new InlineKeyboardMarkup(
                        InlineKeyboardButton.WithWebApp(
                            "🛍️ Открыть магазин",
                            new Telegram.Bot.Types.WebAppInfo("https://betepo4ek.github.io/MarketEnot/") // <-- СЮДА ВСТАВИШЬ ССЫЛКУ НА ФРОНТ
                        )
                    )
                );
                break;

            case "/help":
                await client.SendMessage(
                    chatId,
                    "ℹ️ Доступные команды:\n\n" +
                    "/start - Открыть магазин\n" +
                    "/myorders - Мои заказы\n" +
                    "/help - Помощь"
                );
                break;

            case "/myorders":
                var orders = await _db.GetUserOrders(chatId);
                if (orders.Count == 0)
                {
                    await client.SendMessage(chatId, "📋 У тебя пока нет заказов");
                    break;
                }

                string orderList = "📦 Твои заказы:\n\n";
                foreach (var order in orders)
                {
                    orderList += $"Заказ #{order.Id}\n";
                    orderList += $"Сумма: {order.TotalPrice}₽\n";
                    orderList += $"Статус: {GetStatusEmoji(order.Status)} {order.Status}\n";
                    orderList += $"Дата: {order.CreatedAt:dd.MM.yyyy HH:mm}\n\n";
                }

                await client.SendMessage(chatId, orderList);
                break;

            case string s when s.StartsWith("/setstatus"):
                // Формат: /setstatus 1 paid
                var parts = command.Split(' ');
                if (parts.Length == 3 && int.TryParse(parts[1], out int orderId))
                {
                    string newStatus = parts[2];
                    await _db.UpdateOrderStatus(orderId, newStatus);

                    // Уведомляем юзера о смене статуса
                    var order = await _db.GetOrderById(orderId);
                    if (order != null)
                    {
                        string statusMessage = $"📦 Статус заказа #{orderId} изменён\n\n";
                        statusMessage += $"Новый статус: {GetStatusEmoji(newStatus)} {newStatus}";

                        try
                        {
                            await client.SendMessage(order.UserId, statusMessage);
                        }
                        catch { }
                    }

                    await client.SendMessage(chatId, $"✅ Статус заказа #{orderId} изменён на {newStatus}");
                }
                else
                {
                    await client.SendMessage(chatId,
                        "Использование:\n/setstatus [номер_заказа] [статус]\n\n" +
                        "Статусы:\npending - ожидает оплаты\npaid - оплачен\nconfirmed - готовится\nshipped - в доставке\ncompleted - получен\ncancelled - отменён");
                }
                break;
            case "/admin":
                if (!isAdmin)
                {
                    await client.SendMessage(chatId, "❌ Доступ запрещён");
                    break;
                }
                await client.SendMessage(chatId,
                    "🔧 АДМИН ПАНЕЛЬ\n\n" +
                    "📦 Товары:\n" +
                    "/addproduct - Добавить товар\n" +
                    "/products - Список товаров\n" +
                    "/editprice [id] [цена] - Изменить цену\n" +
                    "/editstock [id] [количество] - Изменить остаток\n" +
                    "/deleteproduct [id] - Удалить товар\n\n" +
                    "📋 Заказы:\n" +
                    "/orders - Все заказы\n" +
                    "/setstatus [id] [статус] - Изменить статус\n\n" +
                    "Статусы: pending, paid, confirmed, shipped, completed, cancelled"
                );
                break;

            case "/addproduct":
                if (!isAdmin)
                {
                    await client.SendMessage(chatId, "❌ Доступ запрещён");
                    break;
                }
                await client.SendMessage(chatId,
                    "📝 Отправь данные товара в формате:\n\n" +
                    "Название\n" +
                    "Описание\n" +
                    "Цена\n" +
                    "Количество\n\n" +
                    "Пример:\n" +
                    "Наушники AirPods\n" +
                    "Беспроводные наушники\n" +
                    "15000\n" +
                    "10"
                );
                // Сохраняем что юзер в режиме добавления товара
                adminStates[chatId] = "adding_product";
                break;

            case "/products":
                if (!isAdmin)
                {
                    await client.SendMessage(chatId, "❌ Доступ запрещён");
                    break;
                }
                var allProducts = await _db.GetAllProducts();
                if (allProducts.Count == 0)
                {
                    await client.SendMessage(chatId, "📦 Товаров пока нет");
                    break;
                }

                string productsList = "📦 ТОВАРЫ:\n\n";
                foreach (var p in allProducts)
                {
                    productsList += $"ID: {p.Id}\n";
                    productsList += $"Название: {p.Name}\n";
                    productsList += $"Цена: {p.Price}₽\n";
                    productsList += $"Остаток: {p.Stock} шт.\n";
                    productsList += $"Доступен: {(p.IsAvailable ? "✅" : "❌")}\n\n";
                }
                await client.SendMessage(chatId, productsList);
                break;

            case string s when s.StartsWith("/editprice"):
                if (!isAdmin)
                {
                    await client.SendMessage(chatId, "❌ Доступ запрещён");
                    break;
                }
                var priceParts = command.Split(' ');
                if (priceParts.Length == 3 && int.TryParse(priceParts[1], out int priceProductId) && decimal.TryParse(priceParts[2], out decimal newPrice))
                {
                    await _db.UpdateProductPrice(priceProductId, newPrice);
                    await client.SendMessage(chatId, $"✅ Цена товара #{priceProductId} изменена на {newPrice}₽");
                }
                else
                {
                    await client.SendMessage(chatId, "Формат: /editprice [id] [цена]\nПример: /editprice 1 15000");
                }
                break;

            case string s when s.StartsWith("/editstock"):
                if (!isAdmin)
                {
                    await client.SendMessage(chatId, "❌ Доступ запрещён");
                    break;
                }
                var stockParts = command.Split(' ');
                if (stockParts.Length == 3 && int.TryParse(stockParts[1], out int stockProductId) && int.TryParse(stockParts[2], out int newStock))
                {
                    await _db.UpdateProductStock(stockProductId, newStock);
                    await client.SendMessage(chatId, $"✅ Остаток товара #{stockProductId} изменён на {newStock} шт.");
                }
                else
                {
                    await client.SendMessage(chatId, "Формат: /editstock [id] [количество]\nПример: /editstock 1 50");
                }
                break;

            case string s when s.StartsWith("/deleteproduct"):
                if (!isAdmin)
                {
                    await client.SendMessage(chatId, "❌ Доступ запрещён");
                    break;
                }
                var delParts = command.Split(' ');
                if (delParts.Length == 2 && int.TryParse(delParts[1], out int delProductId))
                {
                    await _db.DeleteProduct(delProductId);
                    await client.SendMessage(chatId, $"✅ Товар #{delProductId} удалён");
                }
                else
                {
                    await client.SendMessage(chatId, "Формат: /deleteproduct [id]\nПример: /deleteproduct 1");
                }
                break;

            case "/orders":
                if (!isAdmin)
                {
                    await client.SendMessage(chatId, "❌ Доступ запрещён");
                    break;
                }
                var allOrders = await _db.GetAllOrders();
                if (allOrders.Count == 0)
                {
                    await client.SendMessage(chatId, "📋 Заказов пока нет");
                    break;
                }

                string ordersList = "📋 ЗАКАЗЫ:\n\n";
                foreach (var o in allOrders)
                {
                    ordersList += $"#{o.Id} | {o.TotalPrice}₽ | {GetStatusEmoji(o.Status)} {o.Status}\n";
                    ordersList += $"Дата: {o.CreatedAt:dd.MM.yyyy HH:mm}\n\n";
                }
                await client.SendMessage(chatId, ordersList);
                break;
                default:
    // Проверяем состояние админа
        if (isAdmin && adminStates.ContainsKey(chatId))
    {
        if (adminStates[chatId] == "adding_product")
        {
            var lines = command.Split('\n');
            if (lines.Length >= 4)
            {
                string name = lines[0];
                string desc = lines[1];
                if (decimal.TryParse(lines[2], out decimal price) && int.TryParse(lines[3], out int stock))
                {
                    await _db.AddProduct(name, desc, price, stock);
                    await client.SendMessage(chatId, $"✅ Товар '{name}' добавлен!");
                    adminStates.Remove(chatId);
                }
                else
                {
                    await client.SendMessage(chatId, "❌ Неверный формат цены или количества");
                }
            }
            else
            {
                await client.SendMessage(chatId, "❌ Неверный формат. Нужно 4 строки.");
            }
        }
    }
    break;
        }
    }

    private string GetStatusEmoji(string status)
    {
        return status switch
        {
            "pending" => "⏳",
            "paid" => "✅",
            "confirmed" => "📦",
            "shipped" => "🚚",
            "completed" => "✅",
            "cancelled" => "❌",
            _ => "❓"
        };
    }
}

// Webhook для уведомлений о новых заказах (добавим позже)
public static class OrderNotifier
{
    public static async Task NotifyNewOrder(ITelegramBotClient bot, long adminChatId, int orderId, Order order, List<OrderItemDetail> items)
    {
        string message = $"🔔 НОВЫЙ ЗАКАЗ #{orderId}\n\n";
        message += $"💰 Сумма: {order.TotalPrice}₽\n";
        message += $"📞 Телефон: {order.PhoneNumber}\n";
        message += $"📍 Адрес: {order.DeliveryAddress}\n";
        message += $"💬 Комментарий: {order.Comment}\n\n";
        message += "📦 Товары:\n";

        foreach (var item in items)
        {
            message += $"• {item.ProductName} x{item.Quantity} = {item.Price * item.Quantity}₽\n";
        }

        message += "\n💳 Реквизиты для оплаты:\nСБП: +7 (987) 759-66-43";

        await bot.SendMessage(adminChatId, message);
    }
}