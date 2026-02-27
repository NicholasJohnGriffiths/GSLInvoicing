using System;
using System.Collections.Generic;

namespace GSLInvoicing.Web.Models;

public partial class InvoiceItem
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public decimal Rate { get; set; }

    public double Hours { get; set; }

    public decimal Amount { get; set; }

    public decimal GST { get; set; }

    public decimal Total { get; set; }

    public string? Description { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;
}
