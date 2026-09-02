using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;

namespace UpsChecklist.Core.Pdf;

internal static class PdfFormAppearance
{
    public const string FormFontName = "Helv";

    public static void Apply(Document document)
    {
        document.Form.DefaultAppearance = new DefaultAppearance(FormFontName, 10, System.Drawing.Color.Black);

        foreach (var entry in document.Form.Fields)
        {
            switch (entry)
            {
                case TextBoxField textBox:
                    StyleTextBox(textBox);
                    textBox.UpdateAppearances();
                    break;
                case CheckboxField checkBox:
                    StyleCheckBox(checkBox);
                    checkBox.UpdateAppearances();
                    break;
            }
        }
    }

    private static void StyleTextBox(TextBoxField field)
    {
        var size = field.DefaultAppearance?.FontSize ?? 10;
        if (size <= 0 || double.IsNaN(size))
            size = 10;

        field.DefaultAppearance = new DefaultAppearance(FormFontName, size, System.Drawing.Color.Black);
        field.Border = new Border(field) { Width = 1, Style = BorderStyle.Solid };
        field.Characteristics.Background = System.Drawing.Color.White;
        field.Characteristics.Border = PdfPalette.FieldBorderDrawing;
    }

    private static void StyleCheckBox(CheckboxField field)
    {
        field.Border = new Border(field) { Width = 1, Style = BorderStyle.Solid };
        field.Characteristics.Background = System.Drawing.Color.White;
        field.Characteristics.Border = PdfPalette.MutedDrawing;
    }
}
