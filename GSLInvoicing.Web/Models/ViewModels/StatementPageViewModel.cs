using Microsoft.AspNetCore.Mvc.Rendering;

namespace GSLInvoicing.Web.Models.ViewModels;

public class StatementPageViewModel
{
    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public int? SelectedClientId { get; set; }

    public string? OutputFolderPath { get; set; }

    public string? StatusMessage { get; set; }

    public List<SelectListItem> ClientOptions { get; set; } = [];

    public string? ClientName { get; set; }

    public string? ClientContact { get; set; }

    public string? ClientEmail { get; set; }

    public List<StatementTransactionRow> Transactions { get; set; } = [];

    public decimal ClosingBalance { get; set; }

    public bool HasStatement => SelectedClientId.HasValue;
}

public class StatementTransactionRow
{
    public DateTime TransDate { get; set; }

    public string? TransType { get; set; }

    public string? Detail { get; set; }

    public string? Particulars { get; set; }

    public string? Code { get; set; }

    public string? Reference { get; set; }

    public decimal Amount { get; set; }

    public decimal RunningBalance { get; set; }
}
