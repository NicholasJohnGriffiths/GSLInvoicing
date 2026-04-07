using System.ComponentModel.DataAnnotations;

namespace GSLInvoicing.Web.Models;

public partial class AppUser
{
    public int Id { get; set; }

    public string UserName { get; set; } = null!;

    public string Password { get; set; } = null!;

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [Phone]
    [StringLength(255)]
    public string? Phone { get; set; }

    public int VendorId { get; set; }

    public UserType UserType { get; set; } = UserType.General;

    public virtual Vendor Vendor { get; set; } = null!;
}