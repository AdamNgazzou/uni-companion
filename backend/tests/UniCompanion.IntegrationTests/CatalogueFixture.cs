using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UniCompanion.IntegrationTests;

// Generated, public test content: no university snapshot or network access required.
internal sealed class CatalogueFixture : IDisposable
{
    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("companion-catalogue-");
    public string Path { get; }
    public string PdfHash { get; }

    public CatalogueFixture()
    {
        Path = System.IO.Path.Combine(root.FullName, "catalogue.json");
        var pdf = CreatePdf();
        var csv = Encoding.UTF8.GetBytes("topic,score\nfixtures,100\n");
        PdfHash = Convert.ToHexStringLower(SHA256.HashData(pdf));
        File.WriteAllBytes(System.IO.Path.Combine(root.FullName, "lesson.pdf"), pdf);
        File.WriteAllBytes(System.IO.Path.Combine(root.FullName, "exercise.csv"), csv);
        var catalogue = new
        {
            courses = new[]
            {
                new
                {
                    id = "fixture-course", title = "Synthetic course", description = "Public test fixture",
                    chapters = new[]
                    {
                        new { id = "fixture-chapter", title = "Introduction", components = new[] { new { id = "fixture-component" } } }
                    }
                }
            },
            files = new[]
            {
                Entry("lesson.pdf", pdf, "Lesson"),
                Entry("exercise.csv", csv, "Exercise")
            }
        };
        File.WriteAllText(Path, JsonSerializer.Serialize(catalogue));
    }

    private static object Entry(string file, byte[] bytes, string title) => new
    {
        courseId = "fixture-course",
        componentId = "fixture-component",
        status = "downloaded",
        file,
        title,
        bytes = bytes.Length,
        sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes))
    };

    private static byte[] CreatePdf()
    {
        // A complete one-page PDF, including byte offsets and trailer.
        var pdf = new StringBuilder("%PDF-1.4\n");
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>"
        };
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length); // All fixture characters are ASCII.
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = pdf.Length;
        pdf.Append("xref\n0 4\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        pdf.Append($"trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    public void Dispose() => root.Delete(recursive: true);
}
