using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Ziklo.Application;

public static class PortfolioSpreadsheet
{
    private const string SpreadsheetNs =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static byte[] Create(PortfolioView portfolio)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            WriteText(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="utf-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            WriteText(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="utf-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteText(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="utf-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Cartera" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            WriteText(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="utf-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);

            var entry = zip.CreateEntry("xl/worksheets/sheet1.xml",
                CompressionLevel.Optimal);
            using var stream = entry.Open();
            using var xml = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false), CloseOutput = false
            });
            xml.WriteStartDocument();
            xml.WriteStartElement("worksheet", SpreadsheetNs);
            xml.WriteStartElement("sheetViews", SpreadsheetNs);
            xml.WriteStartElement("sheetView", SpreadsheetNs);
            xml.WriteAttributeString("workbookViewId", "0");
            xml.WriteStartElement("pane", SpreadsheetNs);
            xml.WriteAttributeString("ySplit", "1");
            xml.WriteAttributeString("topLeftCell", "A2");
            xml.WriteAttributeString("state", "frozen");
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteStartElement("cols", SpreadsheetNs);
            for (int column = 1; column <= 12; column++)
            {
                xml.WriteStartElement("col", SpreadsheetNs);
                xml.WriteAttributeString("min", column.ToString(CultureInfo.InvariantCulture));
                xml.WriteAttributeString("max", column.ToString(CultureInfo.InvariantCulture));
                xml.WriteAttributeString("width", column <= 2 ? "23" : "17");
                xml.WriteAttributeString("customWidth", "1");
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
            xml.WriteStartElement("sheetData", SpreadsheetNs);
            string[] headers = ["Préstamo", "Cliente", "Días de atraso", "Edad",
                "Moneda", "Capital", "Interés corriente", "Mora", "Seguro",
                "Cargos", "Saldo a favor", "Total"];
            Row(1, headers.Select(x => (object)x).ToArray());
            int rowNumber = 2;
            foreach (var loan in portfolio.Loans)
                Row(rowNumber++, [loan.LoanNumber, loan.ClientId.ToString(),
                    loan.DaysPastDue, loan.AgingBucket, loan.CurrencyCode,
                    loan.Principal, loan.CurrentInterest, loan.LateInterest,
                    loan.Insurance, loan.Charges, loan.Credit, loan.Total]);
            Row(rowNumber, ["TOTAL", "", "", "", "",
                portfolio.Principal, portfolio.CurrentInterest,
                portfolio.LateInterest, portfolio.Insurance, portfolio.Charges,
                portfolio.Credit, portfolio.Total]);
            xml.WriteEndElement();
            xml.WriteStartElement("autoFilter", SpreadsheetNs);
            xml.WriteAttributeString("ref", $"A1:L{rowNumber - 1}");
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndDocument();

            void Row(int number, object[] cells)
            {
                xml.WriteStartElement("row", SpreadsheetNs);
                xml.WriteAttributeString("r", number.ToString(CultureInfo.InvariantCulture));
                for (int column = 0; column < cells.Length; column++)
                {
                    string reference = $"{(char)('A' + column)}{number}";
                    xml.WriteStartElement("c", SpreadsheetNs);
                    xml.WriteAttributeString("r", reference);
                    if (cells[column] is string value)
                    {
                        xml.WriteAttributeString("t", "inlineStr");
                        xml.WriteStartElement("is", SpreadsheetNs);
                        xml.WriteElementString("t", SpreadsheetNs, value);
                        xml.WriteEndElement();
                    }
                    else
                    {
                        xml.WriteElementString("v", SpreadsheetNs,
                            Convert.ToString(cells[column], CultureInfo.InvariantCulture));
                    }
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
        }
        return output.ToArray();
    }

    private static void WriteText(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
