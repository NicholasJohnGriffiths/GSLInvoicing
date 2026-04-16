using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GSLInvoicing.Web.Models;

public partial class Transaction
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public DateTime TransDate { get; set; }

    public string TransType { get; set; } = "Debit";

    public string? Title { get; set; }

    public string? Detail { get; set; }

    public string? Particulars { get; set; }

    public string? Code { get; set; }

    public string? Reference { get; set; }

    public decimal Amount { get; set; }

    public string? AccountNumber { get; set; }

    [ValidateNever]
    public virtual Client Client { get; set; } = null!;
}
