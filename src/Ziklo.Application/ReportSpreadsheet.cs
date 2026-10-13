using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Ziklo.Application;

public static class ReportSpreadsheet
{
    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static byte[] Create(ReportTable report)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Add(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="utf-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            Add(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="utf-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Add(zip, "xl/workbook.xml", $"""
                <?xml version="1.0" encoding="utf-8"?>
                <workbook xmlns="{Ns}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="{System.Security.SecurityElement.Escape(report.Title)}" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            Add(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="utf-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            using var stream = zip.CreateEntry("xl/worksheets/sheet1.xml",
                CompressionLevel.Optimal).Open();
            using var xml = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false), CloseOutput = false
            });
            xml.WriteStartDocument();
            xml.WriteStartElement("worksheet", Ns);
            xml.WriteStartElement("sheetViews", Ns);
            xml.WriteStartElement("sheetView", Ns);
            xml.WriteAttributeString("workbookViewId", "0");
            xml.WriteStartElement("pane", Ns);
            xml.WriteAttributeString("ySplit", "1");
            xml.WriteAttributeString("topLeftCell", "A2");
            xml.WriteAttributeString("state", "frozen");
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteStartElement("sheetData", Ns);
            Row(1, report.Headers, []);
            int rowNumber = 2;
            foreach (string[] cells in report.Rows)
                Row(rowNumber++, cells, report.NumericColumns);
            xml.WriteEndElement();
            if (rowNumber > 2)
            {
                xml.WriteStartElement("autoFilter", Ns);
                xml.WriteAttributeString("ref", $"A1:{Column(report.Headers.Length - 1)}{rowNumber - 1}");
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
            xml.WriteEndDocument();

            void Row(int number, string[] cells, int[] numericColumns)
            {
                xml.WriteStartElement("row", Ns);
                xml.WriteAttributeString("r", number.ToString(CultureInfo.InvariantCulture));
                for (int column = 0; column < cells.Length; column++)
                {
                    xml.WriteStartElement("c", Ns);
                    xml.WriteAttributeString("r", $"{Column(column)}{number}");
                    if (numericColumns.Contains(column) &&
                        decimal.TryParse(cells[column], NumberStyles.Number,
                            CultureInfo.InvariantCulture, out decimal numberValue))
                        xml.WriteElementString("v", Ns,
                            numberValue.ToString(CultureInfo.InvariantCulture));
                    else
                    {
                        xml.WriteAttributeString("t", "inlineStr");
                        xml.WriteStartElement("is", Ns);
                        xml.WriteElementString("t", Ns, cells[column]);
                        xml.WriteEndElement();
                    }
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
        }
        return output.ToArray();
    }

    private static string Column(int zeroBased)
    {
        if (zeroBased is < 0 or >= 26)
            throw new ArgumentOutOfRangeException(nameof(zeroBased));
        return ((char)('A' + zeroBased)).ToString();
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path,
            CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
