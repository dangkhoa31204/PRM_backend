using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRM_beckend.Data;
using PRM_beckend.Models;
using QRCoder;

namespace PRM_beckend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TablesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public TablesController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // --- DTOs ---
    public record TableResponse(int TableId, int Capacity, int Status, string StatusLabel, DateTime CreatedAt);
    public record CreateTableRequest(int Capacity);
    public record UpdateTableRequest(int Capacity, int Status);

    private static string GetStatusLabel(int status) => status switch
    {
        1 => "Available",
        2 => "Occupied",
        3 => "Reserved",
        _ => "Unknown"
    };

    private TableResponse ToResponse(Table t) =>
        new(t.TableId, t.Capacity, t.Status, GetStatusLabel(t.Status), t.CreatedAt);

    // GET /api/tables — Lấy tất cả bàn (Admin)
    /// <summary>
    /// Lấy danh sách tất cả bàn. Chỉ Admin được truy cập.
    /// </summary>
    [Authorize(Roles = "1")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TableResponse>>> GetAll()
    {
        var tables = await _context.Tables
            .OrderBy(t => t.TableId)
            .Select(t => new TableResponse(t.TableId, t.Capacity, t.Status, GetStatusLabel(t.Status), t.CreatedAt))
            .ToListAsync();
        return Ok(tables);
    }

    // GET /api/tables/{id} — Chi tiết 1 bàn (Admin)
    /// <summary>
    /// Lấy chi tiết một bàn theo id. Chỉ Admin được truy cập.
    /// </summary>
    [Authorize(Roles = "1")]
    [HttpGet("{id}")]
    public async Task<ActionResult<TableResponse>> GetById(int id)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null) return NotFound($"Table {id} not found.");
        return Ok(ToResponse(table));
    }

    // POST /api/tables — Tạo bàn mới (Admin)
    /// <summary>
    /// Tạo bàn mới. Chỉ Admin được thực hiện.
    /// </summary>
    [Authorize(Roles = "1")]
    [HttpPost]
    public async Task<ActionResult<TableResponse>> Create([FromBody] CreateTableRequest request)
    {
        if (request.Capacity <= 0)
            return BadRequest("Capacity must be greater than 0.");

        var table = new Table
        {
            Capacity = request.Capacity,
            Status = 1, // Available
            CreatedAt = DateTime.UtcNow
        };

        _context.Tables.Add(table);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = table.TableId }, ToResponse(table));
    }

    // PUT /api/tables/{id} — Cập nhật bàn (Admin)
    /// <summary>
    /// Cập nhật sức chứa và trạng thái của bàn. Chỉ Admin được thực hiện.
    /// </summary>
    [Authorize(Roles = "1")]
    [HttpPut("{id}")]
    public async Task<ActionResult<TableResponse>> Update(int id, [FromBody] UpdateTableRequest request)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null) return NotFound($"Table {id} not found.");

        if (request.Capacity <= 0)
            return BadRequest("Capacity must be greater than 0.");

        if (request.Status < 1 || request.Status > 3)
            return BadRequest("Status must be 1 (Available), 2 (Occupied), or 3 (Reserved).");

        table.Capacity = request.Capacity;
        table.Status = request.Status;

        await _context.SaveChangesAsync();
        return Ok(ToResponse(table));
    }

    // DELETE /api/tables/{id} — Xóa bàn (Admin)
    /// <summary>
    /// Xóa bàn nếu bàn không có order đang active. Chỉ Admin được thực hiện.
    /// </summary>
    [Authorize(Roles = "1")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null) return NotFound($"Table {id} not found.");

        var hasActiveOrder = await _context.Orders
            .AnyAsync(o => o.TableId == id && o.Status < 4);
        if (hasActiveOrder)
            return Conflict("Cannot delete table with active orders.");

        _context.Tables.Remove(table);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/tables/{id}/qrcode — Tạo QR code cho bàn (Admin)
    /// <summary>
    /// Tạo và tải QR code cho một bàn. Chỉ Admin được truy cập.
    /// </summary>
    [Authorize(Roles = "1")]
    [HttpGet("{id}/qrcode")]
    public async Task<IActionResult> GetQrCode(int id)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null) return NotFound($"Table {id} not found.");

        // URL sẽ được mã hóa vào QR (mobile app / web sẽ đọc tableId từ đây)
        var baseUrl = _configuration["AppBaseUrl"] ?? "https://localhost:7227";
        var qrContent = $"{baseUrl}/?tableId={id}";

        var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        var pngBytes = qrCode.GetGraphic(10);

        return File(pngBytes, "image/png", $"table_{id}_qrcode.png");
    }
}
