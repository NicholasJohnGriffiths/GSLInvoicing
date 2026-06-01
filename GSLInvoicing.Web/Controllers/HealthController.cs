using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSLInvoicing.Web.Controllers;

public class HealthController : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Version()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var version = informationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";

        return Json(new
        {
            service = "GSLInvoicing.Web",
            version,
            environment = HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.EnvironmentName,
            utc = DateTime.UtcNow
        });
    }
}
