using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UpsChecklist.Core.Scanners;

namespace UpsChecklist.Web.Pages;

public sealed class ScannersModel(ScannerListOptions options) : PageModel
{
    public string ReturnInstruction => options.ReturnInstruction;
    // Default encoder escapes '<', so source strings cannot terminate the script tag.
    public string CatalogJson { get; private set; } = "[]";
    public void OnGet()
    {
        Response.Headers.CacheControl = "no-store";
        CatalogJson = JsonSerializer.Serialize(ScannerLists.All, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
