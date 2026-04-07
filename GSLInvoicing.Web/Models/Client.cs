using System;
using System.Collections.Generic;

namespace GSLInvoicing.Web.Models;

public partial class Client
{
    public int Id { get; set; }

    public int VendorId { get; set; }

    public string? CardId { get; set; }

    public string Name { get; set; } = null!;

    public string? Contact { get; set; }

    public string? Email { get; set; }

    public string? GSTCode { get; set; }

    public decimal Rate { get; set; }

    public DateOnly DateCreated { get; set; }

    public string? Street { get; set; }

    public string? Suburb { get; set; }

    public string? City { get; set; }

    public string? Postcode { get; set; }

    public string? Country { get; set; }

    public virtual Vendor Vendor { get; set; } = null!;

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
