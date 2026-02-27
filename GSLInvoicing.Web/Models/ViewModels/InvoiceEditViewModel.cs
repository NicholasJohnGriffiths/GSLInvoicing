using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GSLInvoicing.Web.Models.ViewModels;

public class InvoiceEditViewModel
{
    public int Id { get; set; }

    [Required]
    public int ClientId { get; set; }

    [Required]
    [StringLength(255)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateOnly InvoiceDate { get; set; }

    [StringLength(255)]
    public string? PONumber { get; set; }

    [StringLength(255)]
    public string? Contact { get; set; }

    public string? Notes { get; set; }

    public string? ClientGstCode { get; set; }

    public decimal GstRatePercent { get; set; }

    public List<SelectListItem> Clients { get; set; } = new();

    public List<InvoiceItemDisplayViewModel> Items { get; set; } = new();

    public AddInvoiceItemInput NewItem { get; set; } = new();
}

public class InvoiceItemDisplayViewModel
{
    public int Id { get; set; }
    public string? Description { get; set; }
    public double Hours { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public decimal GST { get; set; }
    public decimal Total { get; set; }
}

public class AddInvoiceItemInput
{
    [Required]
    [Range(0.01, 1000000)]
    public double Hours { get; set; }

    [Required]
    [Range(0.01, 1000000)]
    public decimal Rate { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }
}
