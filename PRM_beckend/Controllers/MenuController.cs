using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRM_beckend.Data;
using PRM_beckend.Models;

namespace PRM_beckend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController : ControllerBase
{
    private readonly AppDbContext _context;

    public MenuController(AppDbContext context)
    {
        _context = context;
    }

    // --- DTOs ---
    public record MenuItemResponse(
        int MenuItemId, string Name, string? Description,
        decimal Price, int Category, string? ImageUrl,
        bool IsAvailable, DateTime CreatedAt, DateTime? UpdatedAt);

    public record CreateMenuItemRequest(
        string Name, string? Description,
        decimal Price, int Category, string? ImageUrl);

    public record UpdateMenuItemRequest(
        string Name, string? Description,
        decimal Price, int Category, string? ImageUrl, bool IsAvailable);

    private static MenuItemResponse ToResponse(MenuItem m) =>
        new(m.MenuItemId, m.Name, m.Description, m.Price,
            m.Category, m.ImageUrl, m.IsAvailable, m.CreatedAt, m.UpdatedAt);

    // GET /api/menu — Lấy tất cả món đang hiển thị (Public)
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MenuItemResponse>>> GetAvailable()
    {
        var items = await _context.MenuItems
            .Where(m => m.IsAvailable)
            .OrderBy(m => m.Category).ThenBy(m => m.Name)
            .Select(m => new MenuItemResponse(m.MenuItemId, m.Name, m.Description,
                m.Price, m.Category, m.ImageUrl, m.IsAvailable, m.CreatedAt, m.UpdatedAt))
            .ToListAsync();
        return Ok(items);
    }

    // GET /api/menu/all — Lấy tất cả món kể cả ẩn (Admin)
    [Authorize(Roles = "1")]
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<MenuItemResponse>>> GetAll()
    {
        var items = await _context.MenuItems
            .OrderBy(m => m.Category).ThenBy(m => m.Name)
            .Select(m => new MenuItemResponse(m.MenuItemId, m.Name, m.Description,
                m.Price, m.Category, m.ImageUrl, m.IsAvailable, m.CreatedAt, m.UpdatedAt))
            .ToListAsync();
        return Ok(items);
    }

    // GET /api/menu/{id} — Chi tiết 1 món (Public)
    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<ActionResult<MenuItemResponse>> GetById(int id)
    {
        var item = await _context.MenuItems.FindAsync(id);
        if (item == null) return NotFound($"Menu item {id} not found.");
        return Ok(ToResponse(item));
    }

    // POST /api/menu — Tạo món mới (Admin)
    [Authorize(Roles = "1")]
    [HttpPost]
    public async Task<ActionResult<MenuItemResponse>> Create([FromBody] CreateMenuItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");
        if (request.Price <= 0)
            return BadRequest("Price must be greater than 0.");

        var item = new MenuItem
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            ImageUrl = request.ImageUrl,
            IsAvailable = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.MenuItems.Add(item);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = item.MenuItemId }, ToResponse(item));
    }

    // PUT /api/menu/{id} — Cập nhật món (Admin)
    [Authorize(Roles = "1")]
    [HttpPut("{id}")]
    public async Task<ActionResult<MenuItemResponse>> Update(int id, [FromBody] UpdateMenuItemRequest request)
    {
        var item = await _context.MenuItems.FindAsync(id);
        if (item == null) return NotFound($"Menu item {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");
        if (request.Price <= 0)
            return BadRequest("Price must be greater than 0.");

        item.Name = request.Name;
        item.Description = request.Description;
        item.Price = request.Price;
        item.Category = request.Category;
        item.ImageUrl = request.ImageUrl;
        item.IsAvailable = request.IsAvailable;
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(ToResponse(item));
    }

    // DELETE /api/menu/{id} — Xóa món (Admin)
    [Authorize(Roles = "1")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.MenuItems.FindAsync(id);
        if (item == null) return NotFound($"Menu item {id} not found.");

        var usedInOrders = await _context.OrderItems.AnyAsync(oi => oi.MenuItemId == id);
        if (usedInOrders)
            return Conflict("Cannot delete menu item that exists in orders. Hide it instead.");

        _context.MenuItems.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // PATCH /api/menu/{id}/toggle-availability — Ẩn/hiện món (Admin hoặc Staff)
    [Authorize(Roles = "1,2")]
    [HttpPatch("{id}/toggle-availability")]
    public async Task<ActionResult<MenuItemResponse>> ToggleAvailability(int id)
    {
        var item = await _context.MenuItems.FindAsync(id);
        if (item == null) return NotFound($"Menu item {id} not found.");

        item.IsAvailable = !item.IsAvailable;
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(ToResponse(item));
    }
}
