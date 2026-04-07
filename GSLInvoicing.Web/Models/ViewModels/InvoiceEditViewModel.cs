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

public class AddInvoiceItemInput : IValidatableObject
{
    [Range(0.01, 1000000, ErrorMessage = "Hours must be greater than 0.")]
    public double? Hours { get; set; }

    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "Rate must be greater than 0.")]
    public decimal? Rate { get; set; }

    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "Amount must be greater than 0.")]
    public decimal? Amount { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasAmount = Amount.HasValue && Amount.Value > 0;
        var hasHoursAndRate = Hours.HasValue && Hours.Value > 0 && Rate.HasValue && Rate.Value > 0;

        if (!hasAmount && !hasHoursAndRate)
        {
            yield return new ValidationResult(
                "Enter either Amount or both Hours and Rate.",
                [nameof(Amount), nameof(Hours), nameof(Rate)]);
        }
    }
}
