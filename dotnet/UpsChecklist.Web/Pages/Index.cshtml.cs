using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UpsChecklist.Core;

namespace UpsChecklist.Web.Pages;

public class IndexModel : PageModel
{
    public string PositionsJson { get; private set; } = "[]";

    public void OnGet()
    {
        PositionsJson = JsonSerializer.Serialize(
            ChecklistPositions.All.Select(p => new
            {
                p.Id,
                p.Label,
                p.Title,
                p.ScheduleLabel,
                p.PackageLabel,
                p.Handover,
                p.SourceNotes,
                Sections = p.Sections.Select(s => new
                {
                    s.Id,
                    s.Title,
                    s.Note,
                    s.RemarksKey,
                    Items = s.Items.Select(i => new { i.Id, i.Text, i.Note }),
                    Images = s.Images?.Select(i => new { i.File, i.Label }),
                }),
            }),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}
