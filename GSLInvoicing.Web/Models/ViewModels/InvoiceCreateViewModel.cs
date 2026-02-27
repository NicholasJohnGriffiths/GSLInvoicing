using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GSLInvoicing.Web.Models.ViewModels;

public class InvoiceCreateViewModel
{
    [Required]
    public int ClientId { get; set; }

    [DataType(DataType.Date)]
    public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [StringLength(255)]
    public string? PONumber { get; set; }

    [StringLength(255)]
    public string? Contact { get; set; }

    public string? Notes { get; set; }

    public List<SelectListItem> Clients { get; set; } = new();
}
