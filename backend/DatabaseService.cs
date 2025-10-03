using Npgsql;
using Dapper;

public class DatabaseService
{
    private readonly string _connectionString;

    // Используй прямую строку подключения из Supabase
    public DatabaseService(string supabaseConnectionString)
    {
        _connectionString = supabaseConnectionString;
    }

    // Или если хочешь по отдельности
    public DatabaseService(string host, string database, string user, string password, int port = 6543)
    {
        _connectionString = $"Host={host};Port={port};Database={database};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;Timeout=30;Command Timeout=30";
    }

    // Получить или создать пользователя
    public async Task<User?> GetOrCreateUser(long telegramId, string? username, string? firstName, string? lastName)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Сначала пробуем найти
            var existingUser = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM users WHERE telegram_id = @TelegramId",
                new { TelegramId = telegramId }
            );

            if (existingUser != null)
                return existingUser;

            // Если нет - создаём через INSERT ... ON CONFLICT
            return await connection.QueryFirstOrDefaultAsync<User>(
                @"INSERT INTO users (telegram_id, username, first_name, last_name) 
              VALUES (@TelegramId, @Username, @FirstName, @LastName) 
              ON CONFLICT (telegram_id) DO UPDATE 
              SET username = @Username, first_name = @FirstName, last_name = @LastName
              RETURNING *",
                new { TelegramId = telegramId, Username = username, FirstName = firstName, LastName = lastName }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DB Error in GetOrCreateUser: {ex.Message}");
            return null;
        }
    }

    // Получить все товары
    public async Task<List<Product>> GetAllProducts()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var products = await connection.QueryAsync<Product>(
            "SELECT * FROM products WHERE is_available = true ORDER BY id"
        );
        return products.ToList();
    }

    // Получить товар по ID
    public async Task<Product?> GetProductById(int productId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<Product>(
            "SELECT * FROM products WHERE id = @Id",
            new { Id = productId }
        );
    }

    // Создать заказ
    public async Task<int> CreateOrder(long telegramId, decimal totalPrice, string? phone, string? address, string? comment)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var orderId = await connection.QuerySingleAsync<int>(
            @"INSERT INTO orders (user_id, total_price, phone_number, delivery_address, comment, status) 
              VALUES (@UserId, @TotalPrice, @Phone, @Address, @Comment, 'pending') 
              RETURNING id",
            new { UserId = telegramId, TotalPrice = totalPrice, Phone = phone, Address = address, Comment = comment }
        );

        return orderId;
    }

    // Добавить товары в заказ
    public async Task AddOrderItems(int orderId, List<OrderItem> items)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        foreach (var item in items)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO order_items (order_id, product_id, quantity, price) 
                  VALUES (@OrderId, @ProductId, @Quantity, @Price)",
                new { OrderId = orderId, item.ProductId, item.Quantity, item.Price }
            );
        }
    }

    // Получить заказ по ID
    public async Task<Order?> GetOrderById(int orderId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<Order>(
            "SELECT * FROM orders WHERE id = @Id",
            new { Id = orderId }
        );
    }

    // Обновить статус заказа
    public async Task UpdateOrderStatus(int orderId, string status)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(
            "UPDATE orders SET status = @Status WHERE id = @Id",
            new { Status = status, Id = orderId }
        );
    }

    // Получить товары заказа
    public async Task<List<OrderItemDetail>> GetOrderItems(int orderId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var items = await connection.QueryAsync<OrderItemDetail>(
            @"SELECT oi.*, p.name as product_name 
              FROM order_items oi 
              JOIN products p ON oi.product_id = p.id 
              WHERE oi.order_id = @OrderId",
            new { OrderId = orderId }
        );
        return items.ToList();
    }

    // Получить все заказы пользователя
    public async Task<List<Order>> GetUserOrders(long telegramId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var orders = await connection.QueryAsync<Order>(
            "SELECT * FROM orders WHERE user_id = @TelegramId ORDER BY created_at DESC",
            new { TelegramId = telegramId }
        );
        return orders.ToList();
    }
    
    // Добавить товар
public async Task AddProduct(string name, string description, decimal price, int stock)
{
    using var connection = new NpgsqlConnection(_connectionString);
    await connection.ExecuteAsync(
        @"INSERT INTO products (name, description, price, stock, is_available) 
          VALUES (@Name, @Description, @Price, @Stock, true)",
        new { Name = name, Description = description, Price = price, Stock = stock }
    );
}

// Изменить цену
public async Task UpdateProductPrice(int productId, decimal price)
{
    using var connection = new NpgsqlConnection(_connectionString);
    await connection.ExecuteAsync(
        "UPDATE products SET price = @Price WHERE id = @Id",
        new { Price = price, Id = productId }
    );
}

// Изменить остаток
public async Task UpdateProductStock(int productId, int stock)
{
    using var connection = new NpgsqlConnection(_connectionString);
    await connection.ExecuteAsync(
        "UPDATE products SET stock = @Stock WHERE id = @Id",
        new { Stock = stock, Id = productId }
    );
}

// Удалить товар
public async Task DeleteProduct(int productId)
{
    using var connection = new NpgsqlConnection(_connectionString);
    await connection.ExecuteAsync(
        "DELETE FROM products WHERE id = @Id",
        new { Id = productId }
    );
}

// Получить все заказы
public async Task<List<Order>> GetAllOrders()
{
    using var connection = new NpgsqlConnection(_connectionString);
    var orders = await connection.QueryAsync<Order>(
        "SELECT * FROM orders ORDER BY created_at DESC LIMIT 50"
    );
    return orders.ToList();
}
}

// Модели данных
public class User
{
    public long Id { get; set; }
    public long TelegramId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public int? CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public int Stock { get; set; }
    public bool IsAvailable { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = "pending";
    public string? PaymentMethod { get; set; }
    public string? PhoneNumber { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class OrderItemDetail : OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string? ProductName { get; set; }
}