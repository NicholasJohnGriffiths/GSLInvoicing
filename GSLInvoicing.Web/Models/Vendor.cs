using System.Collections.Generic;

namespace GSLInvoicing.Web.Models;

public partial class Vendor
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    public string? BankDetails { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? GSTNumber { get; set; }

    public virtual ICollection<AppUser> AppUsers { get; set; } = new List<AppUser>();

    public virtual ICollection<Client> Clients { get; set; } = new List<Client>();
}