using ACXIOMCRM.Models.ViewModels;
using System.Globalization;
using System.Text;

namespace ACXIOMCRM.Security;

public static class PdfReportWriter
{
    public static byte[] Write(ReportsViewModel report)
    {
        var lines = new List<string>
        {
            report.Title,
            $"Generated: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC",
            $"Date range: {report.DateFrom?.ToString("yyyy-MM-dd") ?? "Any"} to {report.DateTo?.ToString("yyyy-MM-dd") ?? "Any"}",
            $"Search: {report.Search ?? "Any"}",
            $"Matching records: {report.TotalRows:N0}",
            string.Empty
        };
        lines.AddRange(report.Kpis.Select(kpi => $"{kpi.Label}: {kpi.Value}"));
        lines.Add(string.Empty);
        lines.Add(string.Join(" | ", report.Columns.Select(column => column.Header)));
        lines.Add(new string('-', 100));
        foreach (var row in report.Rows)
        {
            var line = string.Join(" | ", row.Cells);
            lines.AddRange(Wrap(SanitizeAscii(line), 105));
        }

        const int linesPerPage = 58;
        var pages = lines.Chunk(linesPerPage).ToArray();
        if (pages.Length == 0) pages = [Array.Empty<string>()];

        var objects = new List<byte[]>();
        objects.Add(AsciiBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(Array.Empty<byte>());
        objects.Add(AsciiBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));

        var pageObjectIds = new List<int>();
        for (var index = 0; index < pages.Length; index++)
        {
            var pageId = 4 + index * 2;
            var contentId = pageId + 1;
            pageObjectIds.Add(pageId);
            var pageContent = new StringBuilder("BT\n/F1 9 Tf\n42 800 Td\n12 TL\n");
            foreach (var line in pages[index])
                pageContent.Append('(').Append(PdfEscape(line)).Append(") Tj\nT*\n");
            pageContent.Append("ET");
            var streamBytes = AsciiBytes(pageContent.ToString());
            var stream = AsciiBytes($"<< /Length {streamBytes.Length} >>\nstream\n").Concat(streamBytes)
                .Concat(AsciiBytes("\nendstream")).ToArray();
            objects.Add(AsciiBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentId} 0 R >>"));
            objects.Add(stream);
        }
        objects[1] = AsciiBytes($"<< /Type /Pages /Kids [{string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pages.Length} >>");

        using var output = new MemoryStream();
        WriteAscii(output, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(output.Position);
            WriteAscii(output, $"{index + 1} 0 obj\n");
            output.Write(objects[index]);
            WriteAscii(output, "\nendobj\n");
        }
        var xrefOffset = output.Position;
        WriteAscii(output, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++)
            WriteAscii(output, $"{offsets[index]:D10} 00000 n \n");
        WriteAscii(output, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        return output.ToArray();
    }

    private static IEnumerable<string> Wrap(string line, int width)
    {
        if (line.Length == 0) return [string.Empty];
        var result = new List<string>();
        for (var start = 0; start < line.Length; start += width)
            result.Add(line.Substring(start, Math.Min(width, line.Length - start)));
        return result;
    }

    private static string SanitizeAscii(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .Select(character => character is >= ' ' and <= '~' ? character : '?').ToArray());
    }

    private static string PdfEscape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static byte[] AsciiBytes(string value) => Encoding.ASCII.GetBytes(value);

    private static void WriteAscii(Stream output, string value) => output.Write(AsciiBytes(value));
}
