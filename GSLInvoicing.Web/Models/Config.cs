using System;
using System.Collections.Generic;

namespace GSLInvoicing.Web.Models;

public partial class Config
{
    public int Id { get; set; }

    public string LastInvoiceNumber { get; set; } = null!;
}
