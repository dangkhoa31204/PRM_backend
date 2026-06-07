using System;
using System.Collections.Generic;

namespace PRM_beckend.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public int TableId { get; set; }

    public int? HandledBy { get; set; }

    public int Status { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Account? HandledByNavigation { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Table Table { get; set; } = null!;
}
