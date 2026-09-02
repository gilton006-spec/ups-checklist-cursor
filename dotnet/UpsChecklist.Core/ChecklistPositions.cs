using System.Text.Json;
using UpsChecklist.Core.Models;

namespace UpsChecklist.Core;

public static class ChecklistPositions
{
    private static readonly Lazy<IReadOnlyList<Position>> Positions = new(BuildPositions);

    public static IReadOnlyList<Position> All => Positions.Value;

    public static Position? Get(string id) => All.FirstOrDefault(p => p.Id == id);

    public static IReadOnlyList<CheckItem> AllItems(Position position) =>
        position.Sections.SelectMany(s => s.Items).ToList();

    public static string? ScheduleWarning(Position position, string date)
    {
        if (string.IsNullOrEmpty(date) || position.Schedule is null)
            return null;

        var mondaySelected = DateTime.TryParse(date, out var parsed) && parsed.DayOfWeek == DayOfWeek.Monday;
        if (position.Schedule == "monday" && !mondaySelected)
            return "This combined PS1 / PS2 checklist is marked Monday only in the workbook. Check your selected date or assignment.";
        if (position.Schedule == "other-days" && mondaySelected)
        {
            return position.Id == "pd2"
                ? "PD2 is marked Not on Monday in the workbook. Confirm your assignment with your team leader."
                : "For Monday, the workbook provides the combined PS1 / PS2 checklist. Check your selected position.";
        }

        return null;
    }

    private static IReadOnlyList<Position> BuildPositions()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "Data", "workbook-source.json");
        var source = JsonSerializer.Deserialize<WorkbookSource>(
            File.ReadAllText(sourcePath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Missing workbook source.");

        string Cell(string sheet, string address)
        {
            var found = source.Sheets.FirstOrDefault(s => s.Name == sheet)
                ?? throw new InvalidOperationException($"Missing source sheet: {sheet}");
            if (!found.Cells.TryGetValue(address, out var value) || string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Missing source instruction: {sheet}!{address}");
            return value.Trim();
        }

        CheckItem Item(string sheet, string address, string? noteAddress = null) =>
            new(address.ToLowerInvariant(), Cell(sheet, address), noteAddress is null ? null : Cell(sheet, noteAddress), address);

        ReferenceImage Ref(string file, string label) => new(file, label);

        CheckSection Before(string sheet) => new(
            "before",
            "Before the sort",
            [
                Item(sheet, "B8"),
                Item(sheet, "B10"),
                Item(sheet, "B12", sheet == "Ps1-Ps2 monday only" ? "F12" : null),
            ]);

        (string id, string label, string title, string sourceSheet) Base(string sheet, string id, string label) =>
            (id, label, Cell(sheet, "E3"), sheet);

        Position Ps(string sheet, string id, string label, string image, int firstRow)
        {
            var b = Base(sheet, id, label);
            return new Position(
                b.id,
                b.label,
                b.title,
                b.sourceSheet,
                [
                    Before(sheet),
                    new CheckSection(
                        "after",
                        "After the sort",
                        [
                            Item(sheet, $"B{firstRow}"),
                            Item(sheet, $"B{firstRow + 2}"),
                            Item(sheet, $"B{firstRow + 4}"),
                        ],
                        [Ref(image, $"{label} conveyor diagram")]),
                ],
                Cell(sheet, $"B{firstRow + 6}"),
                Schedule: "other-days",
                ScheduleLabel: "Other days");
        }

        Position Pd(string sheet, string id, string label, string image)
        {
            var finish = id == "pd3" ? 37 : 36;
            var b = Base(sheet, id, label);
            return new Position(
                b.id,
                b.label,
                b.title,
                b.sourceSheet,
                [
                    Before(sheet),
                    new CheckSection(
                        "after",
                        "After the sort",
                        [Item(sheet, "B25"), Item(sheet, "F25")],
                        [Ref(image, $"{label} conveyor diagram")]),
                    new CheckSection(
                        "floor",
                        "Work floor",
                        [Item(sheet, "B27")],
                        [Ref("image4.png", "First part of the metro belt")]),
                    new CheckSection(
                        "finish",
                        "Finish the checks",
                        [Item(sheet, $"B{finish}"), Item(sheet, $"B{finish + 2}")]),
                ],
                Cell(sheet, "G29"),
                Schedule: id == "pd2" ? "other-days" : null,
                ScheduleLabel: id == "pd2" ? "Not on Monday" : null);
        }

        const string monday = "Ps1-Ps2 monday only";
        const string smalls = "Smalls";
        const string matrix = "Matrix";

        var mondayBase = Base(monday, "ps1-ps2-monday", "PS1 / PS2");
        var psMonday = new Position(
            mondayBase.id,
            mondayBase.label,
            mondayBase.title,
            mondayBase.sourceSheet,
            [
                Before(monday),
                new CheckSection(
                    "ps1",
                    "After the sort: PS1",
                    [Item(monday, "B20"), Item(monday, "B22")],
                    [Ref("image2.png", "PS1 conveyor diagram")]),
                new CheckSection(
                    "ps2",
                    "After the sort: PS2",
                    [Item(monday, "B30"), Item(monday, "B32")],
                    [Ref("image3.png", "PS2 conveyor diagram")]),
                new CheckSection(
                    "finish",
                    "Finish the checks",
                    [Item(monday, "B35", "F35")],
                    Note: "Confirm timing: the source labels this after-sort check ‘When you start’."),
            ],
            Cell(monday, "B37"),
            Schedule: "monday",
            ScheduleLabel: "Monday only",
            SourceNotes:
            [
                "The Monday sheet says \"When you start\" beside its after-sort clean-position check (B35/F35). This wording is preserved; confirm the intended timing with your team leader.",
            ]);

        var smallsBase = Base(smalls, "smalls", "Smalls");
        var smallsPosition = new Position(
            smallsBase.id,
            smallsBase.label,
            smallsBase.title,
            smallsBase.sourceSheet,
            [
                Before(smalls),
                new CheckSection(
                    "sls1",
                    "After the sort: SLS1",
                    [
                        Item(smalls, "B30"), Item(smalls, "F30"), Item(smalls, "B34"),
                        Item(smalls, "B36"), Item(smalls, "B38"), Item(smalls, "B40"),
                    ],
                    [Ref("image9.png", "SLS1 marked conveyor diagram")]),
                new CheckSection(
                    "sls1-under",
                    "SLS1: under and around",
                    [Item(smalls, "A32")],
                    [Ref("image11.png", "SLS1 marked area to check under and around")],
                    RemarksKey: "sls1"),
                new CheckSection(
                    "sls2",
                    "After the sort: SLS2",
                    new List<CheckItem>(new[] { 68, 70, 72, 74, 76, 78, 80 }.Select(row => Item(smalls, $"B{row}"))),
                    [Ref("image10.png", "SLS2 marked conveyor diagram")]),
            ],
            Cell(smalls, "B82"),
            Handover: Cell(smalls, "B90"),
            SourceNotes:
            [
                "The source places the walk-off instruction under Before the sort, although it explicitly says to check after the sort. The original wording is preserved.",
                "The paper handover instruction says to sign the first page, but the signature boxes are on the second page. Confirm the digital handover process; downloading does not send the report.",
            ]);

        var matrixBase = Base(matrix, "matrix", "Matrix");
        var matrixPosition = new Position(
            matrixBase.id,
            matrixBase.label,
            matrixBase.title,
            matrixBase.sourceSheet,
            [
                Before(matrix),
                new CheckSection(
                    "recirculation",
                    "After the sort: recirculation",
                    [
                        Item(matrix, "B30"), Item(matrix, "B32", "C32"),
                        Item(matrix, "B34", "C34"), Item(matrix, "B36", "C36"),
                    ],
                    [Ref("image12.png", "Matrix recirculation diagram")],
                    RemarksKey: "recirculation"),
                new CheckSection(
                    "da",
                    "DA platform",
                    new List<CheckItem>(new[] { 53, 55, 57, 59 }.Select(row => Item(matrix, $"B{row}"))),
                    [Ref("image13.png", "DA platform belt diagram")]),
                new CheckSection(
                    "chutes",
                    "Chutes and collector belts",
                    [Item(matrix, "B74"), Item(matrix, "B76"), Item(matrix, "B83")],
                    [Ref("image14.png", "Matrix chute reference photo")]),
            ],
            Cell(matrix, "E84"));

        return
        [
            psMonday,
            Ps("Ps1 other days", "ps1", "PS1", "image2.png", 20),
            Ps("Ps2 other days", "ps2", "PS2", "image3.png", 21),
            Pd("PD1", "pd1", "PD1", "image5.png"),
            Pd("PD2 Not on Monday", "pd2", "PD2", "image6.png"),
            Pd("PD3", "pd3", "PD3", "image7.png"),
            Pd("PD4", "pd4", "PD4", "image8.png"),
            smallsPosition,
            matrixPosition,
        ];
    }
}
