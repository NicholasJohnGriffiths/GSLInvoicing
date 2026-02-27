using System;
using System.Collections.Generic;

namespace GSLInvoicing.Web.Models;

public partial class Invoice
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    public DateOnly InvoiceDate { get; set; }

    public string? PONumber { get; set; }

    public string? Contact { get; set; }

    public string? Notes { get; set; }

    public DateOnly DateCreated { get; set; }

    public virtual Client Client { get; set; } = null!;

    public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
}
