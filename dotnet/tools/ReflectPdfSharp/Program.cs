using System.Reflection;
using Aspose.Pdf;
using Aspose.Pdf.Forms;

using var doc = Document.Create();
var page = doc.Pages.Add();

foreach (var ctor in typeof(TextBoxField).GetConstructors())
    Console.WriteLine("TextBoxField(" + string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.Name)) + ")");

foreach (var method in typeof(Form).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name.Contains("Add")))
    Console.WriteLine("Form." + method.Name + "(" + string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name)) + ")");

var field = new TextBoxField(page, new Rectangle(100, 700, 300, 722));
field.PartialName = "name";
field.Value = "Hello";
doc.Form.Add(field, 1);

using var stream = new MemoryStream();
doc.Save(stream);
Console.WriteLine("Saved " + stream.Length + ", fields " + doc.Form.Count);
