using System;
using System.Collections.Generic;

namespace PRM_beckend.Models;

public partial class Table
{
    public int TableId { get; set; }

    public int Capacity { get; set; }

    public int Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
