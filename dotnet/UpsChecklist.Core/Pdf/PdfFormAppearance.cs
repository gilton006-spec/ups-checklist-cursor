using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;

namespace UpsChecklist.Core.Pdf;

internal static class PdfFormAppearance
{
    private const int TextFieldBorderWidth = 1;
    private const int CheckboxBorderWidth = 1;

    public static void Apply(Document document, Font font)
    {
        document.Form.DefaultAppearance = new DefaultAppearance(font, 10, System.Drawing.Color.Black);

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
        field.Border = new Border(field) { Width = TextFieldBorderWidth, Style = BorderStyle.Solid };
        field.Characteristics.Background = System.Drawing.Color.White;
        field.Characteristics.Border = PdfPalette.FieldBorderDrawing;
    }

    private static void StyleCheckBox(CheckboxField field)
    {
        field.Border = new Border(field) { Width = CheckboxBorderWidth, Style = BorderStyle.Solid };
        field.Characteristics.Background = System.Drawing.Color.White;
        field.Characteristics.Border = PdfPalette.MutedDrawing;
    }
}
