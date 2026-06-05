using System.IO;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace VenueHelper.Logic;

public enum ExportFormat
{
    Txt = 0,
    Csv = 1,
    Pdf = 2,
    Xlsx = 3,
}

public static class ExportFormatExtensions
{
    public static string Ext(this ExportFormat f) => f switch
    {
        ExportFormat.Txt => "txt",
        ExportFormat.Csv => "csv",
        ExportFormat.Pdf => "pdf",
        ExportFormat.Xlsx => "xlsx",
        _ => "txt",
    };

    public static string Label(this ExportFormat f) => f switch
    {
        ExportFormat.Txt => "TXT",
        ExportFormat.Csv => "CSV",
        ExportFormat.Pdf => "PDF",
        ExportFormat.Xlsx => "XLSX",
        _ => "TXT",
    };
}

// A simple tabular dataset any tab can build and hand to the exporter.
public class TableData
{
    public string Title;
    public IReadOnlyList<string> Headers;
    public IReadOnlyList<IReadOnlyList<string>> Rows;

    public TableData(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        Title = title;
        Headers = headers;
        Rows = rows;
    }
}

// Writes a TableData to disk in the chosen format and returns the path.
// TXT/CSV are plain text; XLSX uses ClosedXML; PDF uses QuestPDF.
public static class Exporter
{
    public static string Write(string directory, string baseName, ExportFormat format, TableData data)
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, $"VenueHelper_{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}.{format.Ext()}");

        switch (format)
        {
            case ExportFormat.Csv:
                File.WriteAllText(file, BuildDelimited(data, ","), new UTF8Encoding(true));
                break;
            case ExportFormat.Txt:
                File.WriteAllText(file, BuildText(data), new UTF8Encoding(false));
                break;
            case ExportFormat.Xlsx:
                WriteXlsx(file, data);
                break;
            case ExportFormat.Pdf:
                WritePdf(file, data);
                break;
        }

        return file;
    }

    // Tab-separated, human-readable text with a header row. Good default for
    // pasting into chat tools or wheelofnames.com (when the data is single-column
    // the header is omitted to keep it clean).
    public static string BuildText(TableData data)
    {
        var sb = new StringBuilder();
        var singleCol = data.Headers.Count == 1;

        if (!singleCol)
            sb.AppendLine(string.Join("\t", data.Headers));

        foreach (var row in data.Rows)
            sb.AppendLine(singleCol
                ? (row.Count > 0 ? row[0] ?? string.Empty : string.Empty)
                : string.Join("\t", row.Select(c => c ?? string.Empty)));

        return sb.ToString();
    }

    private static string BuildDelimited(TableData data, string sep)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(sep, data.Headers.Select(Esc)));
        foreach (var row in data.Rows)
            sb.AppendLine(string.Join(sep, row.Select(Esc)));
        return sb.ToString();
    }

    private static void WriteXlsx(string file, TableData data)
    {
        using var wb = new XLWorkbook();
        var sheetName = Sanitize(data.Title);
        var ws = wb.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Export" : sheetName);

        for (var c = 0; c < data.Headers.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = data.Headers[c];
            cell.Style.Font.Bold = true;
        }

        for (var r = 0; r < data.Rows.Count; r++)
        {
            var row = data.Rows[r];
            for (var c = 0; c < row.Count; c++)
            {
                // Write numbers as numbers when the whole cell is numeric, so
                // Excel can sort/sum; otherwise keep it as text.
                if (long.TryParse(row[c].Replace(",", string.Empty), out var num))
                    ws.Cell(r + 2, c + 1).Value = num;
                else
                    ws.Cell(r + 2, c + 1).Value = row[c];
            }
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(file);
    }

    private static void WritePdf(string file, TableData data)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Text(data.Title).FontSize(16).Bold();

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        for (var i = 0; i < data.Headers.Count; i++)
                            cols.RelativeColumn();
                    });

                    foreach (var h in data.Headers)
                        table.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(h).Bold();

                    foreach (var row in data.Rows)
                        foreach (var cell in row)
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cell);
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Venue Helper \u2014 ");
                    t.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                });
            });
        }).GeneratePdf(file);
    }

    private static string Esc(string? s)
    {
        s ??= string.Empty;
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    // Excel sheet names can't exceed 31 chars or contain []*/\?:
    private static string Sanitize(string s)
    {
        var clean = new string(s.Where(ch => !"[]*/\\?:".Contains(ch)).ToArray());
        return clean.Length > 31 ? clean[..31] : clean;
    }
}
