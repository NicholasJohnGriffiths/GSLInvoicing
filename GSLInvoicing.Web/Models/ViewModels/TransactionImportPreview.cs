namespace GSLInvoicing.Web.Models.ViewModels;

public class TransactionImportPreview
{
    public string TemplateName { get; set; } = string.Empty;

    public List<TransactionImportPreviewRow> Rows { get; set; } = [];
}

public class TransactionImportPreviewRow
{
    public int RowNumber { get; set; }

    public int? MatchedClientId { get; set; }

    public string? MatchedClientName { get; set; }

    public string? MatchReason { get; set; }

    public string? Error { get; set; }

    public DateTime? TransDate { get; set; }

    public string? TransType { get; set; }

    public string? Title { get; set; }

    public string? Detail { get; set; }

    public string? Particulars { get; set; }

    public string? Code { get; set; }

    public string? Reference { get; set; }

    public decimal? Amount { get; set; }

    public string? AccountNumber { get; set; }
}
