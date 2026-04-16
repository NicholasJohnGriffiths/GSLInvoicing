namespace GSLInvoicing.Web.Models.ViewModels;

public class TransactionTemplateDefinition
{
    public string Name { get; set; } = string.Empty;

    public string? TransDateColumn { get; set; }

    public string? TransTypeColumn { get; set; }

    public string? DetailColumn { get; set; }

    public string? ParticularsColumn { get; set; }

    public string? CodeColumn { get; set; }

    public string? ReferenceColumn { get; set; }

    public string? AmountColumn { get; set; }

    public string? AccountNumberColumn { get; set; }
}
