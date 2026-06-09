using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRM_beckend.Data;
using PRM_beckend.Models;
using System.Security.Claims;

namespace PRM_beckend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;

    public OrdersController(AppDbContext context)
    {
        _context = context;
    }

    // --- DTOs ---
    public record OrderItemRequest(int MenuItemId, int Quantity, string? Note);

    public record PlaceOrderRequest(int TableId, List<OrderItemRequest> Items, string? Note);

    public record AddItemsRequest(List<OrderItemRequest> Items);

    public record UpdateStatusRequest(int Status);

    public record OrderItemResponse(
        int OrderItemId, int MenuItemId, string MenuItemName,
        int Quantity, decimal UnitPrice, string? Note);

    public record OrderResponse(
        int OrderId, int TableId, int Status, string StatusLabel,
        decimal TotalAmount, string? Note, DateTime CreatedAt, DateTime? UpdatedAt,
        List<OrderItemResponse> Items);

    private static string GetStatusLabel(int status) => status switch
    {
        1 => "Pending",
        2 => "Confirmed",
        3 => "Serving",
        4 => "Completed",
        5 => "Cancelled",
        _ => "Unknown"
    };

    private static async Task<OrderResponse> BuildResponse(AppDbContext ctx, Order order)
    {
        var items = await ctx.OrderItems
            .Where(oi => oi.OrderId == order.OrderId)
            .Include(oi => oi.MenuItem)
            .Select(oi => new OrderItemResponse(
                oi.OrderItemId, oi.MenuItemId, oi.MenuItem.Name,
                oi.Quantity, oi.UnitPrice, oi.Note))
            .ToListAsync();

        return new OrderResponse(
            order.OrderId, order.TableId, order.Status,
            GetStatusLabel(order.Status), order.TotalAmount,
            order.Note, order.CreatedAt, order.UpdatedAt, items);
    }

    // POST /api/orders — Bàn đặt món mới (Public — khách quét QR)
    /// <summary>
    /// Tạo order mới cho bàn. Public, dùng khi khách quét QR và đặt món.
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return BadRequest("Order must have at least one item.");

        var table = await _context.Tables.FindAsync(request.TableId);
        if (table == null) return NotFound($"Table {request.TableId} not found.");

        // Lấy giá từ DB để tránh giả mạo giá
        var menuItemIds = request.Items.Select(i => i.MenuItemId).ToList();
        var menuItems = await _context.MenuItems
            .Where(m => menuItemIds.Contains(m.MenuItemId) && m.IsAvailable)
            .ToDictionaryAsync(m => m.MenuItemId);

        foreach (var item in request.Items)
        {
            if (!menuItems.ContainsKey(item.MenuItemId))
                return BadRequest($"Menu item {item.MenuItemId} not found or not available.");
            if (item.Quantity <= 0)
                return BadRequest($"Quantity for item {item.MenuItemId} must be greater than 0.");
        }

        // Tính tổng tiền
        var totalAmount = request.Items.Sum(i => menuItems[i.MenuItemId].Price * i.Quantity);

        var order = new Order
        {
            TableId = request.TableId,
            Status = 1, // Pending
            TotalAmount = totalAmount,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Tạo các order items
        var orderItems = request.Items.Select(i => new OrderItem
        {
            OrderId = order.OrderId,
            MenuItemId = i.MenuItemId,
            Quantity = i.Quantity,
            UnitPrice = menuItems[i.MenuItemId].Price,
            Note = i.Note
        }).ToList();

        _context.OrderItems.AddRange(orderItems);

        // Cập nhật trạng thái bàn sang Occupied
        table.Status = 2;

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = order.OrderId },
            await BuildResponse(_context, order));
    }

    // POST /api/orders/{id}/items — Thêm món vào đơn đang mở (Public)
    /// <summary>
    /// Thêm món vào order đang mở. Public, dùng khi khách gọi thêm món.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("{id}/items")]
    public async Task<ActionResult<OrderResponse>> AddItems(int id, [FromBody] AddItemsRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return BadRequest("Must provide at least one item.");

        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound($"Order {id} not found.");
        if (order.Status >= 4)
            return Conflict("Cannot add items to a completed or cancelled order.");

        var menuItemIds = request.Items.Select(i => i.MenuItemId).ToList();
        var menuItems = await _context.MenuItems
            .Where(m => menuItemIds.Contains(m.MenuItemId) && m.IsAvailable)
            .ToDictionaryAsync(m => m.MenuItemId);

        foreach (var item in request.Items)
        {
            if (!menuItems.ContainsKey(item.MenuItemId))
                return BadRequest($"Menu item {item.MenuItemId} not found or not available.");
            if (item.Quantity <= 0)
                return BadRequest($"Quantity for item {item.MenuItemId} must be greater than 0.");
        }

        var newItems = request.Items.Select(i => new OrderItem
        {
            OrderId = id,
            MenuItemId = i.MenuItemId,
            Quantity = i.Quantity,
            UnitPrice = menuItems[i.MenuItemId].Price,
            Note = i.Note
        }).ToList();

        _context.OrderItems.AddRange(newItems);

        // Cập nhật lại tổng tiền
        var addedAmount = request.Items.Sum(i => menuItems[i.MenuItemId].Price * i.Quantity);
        order.TotalAmount += addedAmount;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(await BuildResponse(_context, order));
    }

    // GET /api/orders — Lấy tất cả đơn hàng (Staff, Admin)
    /// <summary>
    /// Lấy danh sách order, có thể lọc theo status. Staff hoặc Admin được truy cập.
    /// </summary>
    [Authorize(Roles = "1,2")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetAll([FromQuery] int? status)
    {
        var query = _context.Orders.AsQueryable();

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var responses = new List<OrderResponse>();
        foreach (var order in orders)
            responses.Add(await BuildResponse(_context, order));

        return Ok(responses);
    }

    // GET /api/orders/{id} — Chi tiết 1 đơn (Staff, Admin)
    /// <summary>
    /// Lấy chi tiết một order theo id. Staff hoặc Admin được truy cập.
    /// </summary>
    [Authorize(Roles = "1,2")]
    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponse>> GetById(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound($"Order {id} not found.");
        return Ok(await BuildResponse(_context, order));
    }

    // GET /api/orders/table/{tableId} — Đơn đang active của bàn (Public)
    /// <summary>
    /// Lấy các order đang active của một bàn. Public, dùng để xem đơn hiện tại của bàn.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("table/{tableId}")]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetByTable(int tableId)
    {
        var orders = await _context.Orders
            .Where(o => o.TableId == tableId && o.Status < 4)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var responses = new List<OrderResponse>();
        foreach (var order in orders)
            responses.Add(await BuildResponse(_context, order));

        return Ok(responses);
    }

    // PATCH /api/orders/{id}/status — Đổi trạng thái đơn (Staff, Admin)
    /// <summary>
    /// Cập nhật trạng thái order. Staff hoặc Admin được thực hiện.
    /// </summary>
    [Authorize(Roles = "1,2")]
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<OrderResponse>> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        if (request.Status < 1 || request.Status > 5)
            return BadRequest("Status must be between 1 (Pending) and 5 (Cancelled).");

        var order = await _context.Orders
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        if (order == null) return NotFound($"Order {id} not found.");

        // Lấy accountId từ JWT token
        var accountIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                          ?? User.FindFirst("sub");
        if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var accountId))
            order.HandledBy = accountId;

        order.Status = request.Status;
        order.UpdatedAt = DateTime.UtcNow;

        // Nếu đơn hoàn thành hoặc hủy, kiểm tra xem bàn còn đơn active không
        if (request.Status == 4 || request.Status == 5)
        {
            var hasOtherActiveOrders = await _context.Orders
                .AnyAsync(o => o.TableId == order.TableId && o.OrderId != id && o.Status < 4);

            if (!hasOtherActiveOrders && order.Table != null)
                order.Table.Status = 1; // Bàn trở lại Available
        }

        await _context.SaveChangesAsync();
        return Ok(await BuildResponse(_context, order));
    }
}
