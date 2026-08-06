using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

public static class TrainingSessionLogger
{
    private const string SummarySheetName = "Session Summary";
    private const string RunsSheetName = "Training Runs";
    private const float RecencyDecay = 0.7f;

    private static readonly List<TrainingRunRecord> runRecords = new List<TrainingRunRecord>();

    private static string sessionId;
    private static DateTime sessionStartedAt;
    private static string outputDirectory;
    private static string workbookPath;
    private static bool isSessionActive;

    public static bool IsSessionActive => isSessionActive;
    public static string SessionId => sessionId;
    public static string OutputDirectory => outputDirectory;
    public static string WorkbookPath => workbookPath;

    public static void StartSession()
    {
        if (isSessionActive)
        {
            return;
        }

        sessionStartedAt = DateTime.Now;
        sessionId = UnityEngine.Random.Range(100000, 999999).ToString(CultureInfo.InvariantCulture);
        runRecords.Clear();

        string folderName = string.Format(
            CultureInfo.InvariantCulture,
            "{0}_TrainingSession_{1}",
            sessionStartedAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture),
            sessionId);

        outputDirectory = Path.Combine(GetProjectRootDirectory(), "output", folderName);
        Directory.CreateDirectory(outputDirectory);

        string dateStamp = sessionStartedAt.ToString("ddMMyyyy", CultureInfo.InvariantCulture);
        workbookPath = Path.Combine(outputDirectory, $"{sessionId}-{dateStamp}.xlsx");

        isSessionActive = true;
        SaveWorkbook();
        Debug.Log("Training session started: " + sessionId + " at " + workbookPath);
    }

    public static void EndSession()
    {
        if (!isSessionActive)
        {
            return;
        }

        SaveWorkbook();
        Debug.Log("Training session ended: " + sessionId);
        isSessionActive = false;
    }

    public static void RecordTrainingRun(TrainingRunRecord record)
    {
        if (!isSessionActive)
        {
            StartSession();
        }

        record.SessionId = sessionId;
        record.SessionStartedAt = sessionStartedAt;
        record.RunNumber = runRecords.Count + 1;
        runRecords.Add(record);
        if (SaveWorkbook())
        {
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "Recorded training run {0} ({1}) to {2}",
                record.RunNumber,
                record.TrainingType,
                workbookPath));
        }
    }

    private static bool SaveWorkbook()
    {
        if (string.IsNullOrEmpty(workbookPath))
        {
            return false;
        }

        string temporaryWorkbookPath = workbookPath + ".tmp";

        try
        {
            if (File.Exists(temporaryWorkbookPath))
            {
                File.Delete(temporaryWorkbookPath);
            }

            using (FileStream stream = new FileStream(temporaryWorkbookPath, FileMode.CreateNew, FileAccess.ReadWrite))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                AddEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
                AddEntry(archive, "_rels/.rels", BuildRootRelationshipsXml());
                AddEntry(archive, "docProps/app.xml", BuildAppPropertiesXml());
                AddEntry(archive, "docProps/core.xml", BuildCorePropertiesXml());
                AddEntry(archive, "xl/workbook.xml", BuildWorkbookXml());
                AddEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationshipsXml());
                AddEntry(archive, "xl/styles.xml", BuildStylesXml());
                AddEntry(archive, "xl/worksheets/sheet1.xml", BuildSummarySheetXml());
                AddEntry(archive, "xl/worksheets/sheet2.xml", BuildRunsSheetXml());
            }

            File.Copy(temporaryWorkbookPath, workbookPath, true);
            File.Delete(temporaryWorkbookPath);
            return true;
        }
        catch (IOException ex)
        {
            Debug.LogError("Could not update training workbook. Close it in Excel and run again. Path: " + workbookPath + "\n" + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.LogError("Could not update training workbook because access was denied. Path: " + workbookPath + "\n" + ex.Message);
        }

        return false;
    }

    private static string BuildSummarySheetXml()
    {
        WorksheetBuilder sheet = new WorksheetBuilder();
        sheet.SetColumnWidths(16, 16, 24, 22, 20, 20, 14);

        sheet.AddRow(1, new[]
        {
            Cell.Text("session id", 1),
            Cell.Text("date", 1),
            Cell.Text("auditory training length", 1),
            Cell.Text("visual training length", 1),
            Cell.Text("started", 1),
            Cell.Text("last updated", 1),
            Cell.Text("decay d", 1)
        });

        DateTime lastUpdatedAt = runRecords.Count > 0 ? runRecords[runRecords.Count - 1].EndedAt : DateTime.Now;

        sheet.AddRow(2, new[]
        {
            Cell.Text(sessionId),
            Cell.Text(sessionStartedAt.ToString("ddMMyyyy", CultureInfo.InvariantCulture)),
            Cell.Number(AverageDurationForType("auditory"), 2),
            Cell.Number(AverageDurationForType("visual"), 2),
            Cell.Text(sessionStartedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            Cell.Text(lastUpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            Cell.Number(RecencyDecay, 2)
        });

        sheet.AddRow(6, new[]
        {
            Cell.Blank(),
            Cell.Text("auditory accuracy", 1),
            Cell.Text("visual accuracy", 1),
            Cell.Text("auditory weighted", 1),
            Cell.Text("visual weighted", 1),
            Cell.Text("timestamp", 1)
        });

        int rowNumber = 7;
        foreach (TrainingRunRecord record in runRecords)
        {
            bool isAuditory = IsTrainingType(record.TrainingType, "auditory");
            bool isVisual = IsTrainingType(record.TrainingType, "visual");
            bool hasAuditoryRun = HasRunThrough("auditory", record.RunNumber);
            bool hasVisualRun = HasRunThrough("visual", record.RunNumber);
            float auditoryWeightedScore = GetWeightedScoreThroughRun("auditory", record.RunNumber);
            float visualWeightedScore = GetWeightedScoreThroughRun("visual", record.RunNumber);

            sheet.AddRow(rowNumber, new[]
            {
                Cell.Blank(),
                isAuditory ? Cell.Number(record.FocusedPercentage, 2) : Cell.Blank(),
                isVisual ? Cell.Number(record.FocusedPercentage, 2) : Cell.Blank(),
                hasAuditoryRun ? Cell.Number(auditoryWeightedScore, 2) : Cell.Blank(),
                hasVisualRun ? Cell.Number(visualWeightedScore, 2) : Cell.Blank(),
                Cell.Text(record.EndedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture))
            });
            rowNumber++;
        }

        if (runRecords.Count > 0)
        {
            rowNumber++;
            sheet.AddRow(rowNumber, new[]
            {
                Cell.Text("Raw average", 1),
                Cell.Number(AverageForType("auditory"), 2),
                Cell.Number(AverageForType("visual"), 2),
                Cell.Blank(),
                Cell.Blank(),
                Cell.Blank()
            });

            rowNumber++;
            sheet.AddRow(rowNumber, new[]
            {
                Cell.Text("Weighted average", 1),
                Cell.Blank(),
                Cell.Blank(),
                Cell.Number(GetWeightedScoreForType("auditory"), 2),
                Cell.Number(GetWeightedScoreForType("visual"), 2),
                Cell.Blank()
            });
        }

        return sheet.Build();
    }

    private static string BuildRunsSheetXml()
    {
        WorksheetBuilder sheet = new WorksheetBuilder();
        sheet.SetColumnWidths(14, 12, 18, 10, 22, 22, 16, 16, 18, 24, 22, 14);
        sheet.AddRow(1, new[]
        {
            Cell.Text("session id", 1),
            Cell.Text("date", 1),
            Cell.Text("training type", 1),
            Cell.Text("run", 1),
            Cell.Text("started timestamp", 1),
            Cell.Text("ended timestamp", 1),
            Cell.Text("duration seconds", 1),
            Cell.Text("focused seconds", 1),
            Cell.Text("focused percentage", 1),
            Cell.Text("auditory weighted average", 1),
            Cell.Text("visual weighted average", 1),
            Cell.Text("hrv threshold", 1)
        });

        int rowNumber = 2;
        foreach (TrainingRunRecord record in runRecords)
        {
            bool hasAuditoryRun = HasRunThrough("auditory", record.RunNumber);
            bool hasVisualRun = HasRunThrough("visual", record.RunNumber);

            sheet.AddRow(rowNumber, new[]
            {
                Cell.Text(record.SessionId),
                Cell.Text(record.SessionStartedAt.ToString("ddMMyyyy", CultureInfo.InvariantCulture)),
                Cell.Text(record.TrainingType),
                Cell.Number(record.RunNumber, 0),
                Cell.Text(record.StartedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                Cell.Text(record.EndedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                Cell.Number(record.DurationSeconds, 2),
                Cell.Number(record.FocusedSeconds, 2),
                Cell.Number(record.FocusedPercentage, 2),
                hasAuditoryRun ? Cell.Number(GetWeightedScoreThroughRun("auditory", record.RunNumber), 2) : Cell.Blank(),
                hasVisualRun ? Cell.Number(GetWeightedScoreThroughRun("visual", record.RunNumber), 2) : Cell.Blank(),
                Cell.Number(record.HrvThreshold, 2)
            });
            rowNumber++;
        }

        return sheet.Build();
    }

    private static bool IsTrainingType(string trainingType, string expected)
    {
        return trainingType.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static float AverageForType(string trainingType)
    {
        List<TrainingRunRecord> matchingRecords = runRecords
            .Where(record => IsTrainingType(record.TrainingType, trainingType))
            .ToList();

        if (matchingRecords.Count == 0)
        {
            return 0f;
        }

        return matchingRecords.Average(record => record.FocusedPercentage);
    }

    private static float GetWeightedScoreForType(string trainingType)
    {
        return GetWeightedScoreThroughRun(trainingType, int.MaxValue);
    }

    private static bool HasRunThrough(string trainingType, int throughRunNumber)
    {
        return runRecords.Any(record => record.RunNumber <= throughRunNumber && IsTrainingType(record.TrainingType, trainingType));
    }

    private static float GetWeightedScoreThroughRun(string trainingType, int throughRunNumber)
    {
        List<TrainingRunRecord> matchingRecords = runRecords
            .Where(record => record.RunNumber <= throughRunNumber && IsTrainingType(record.TrainingType, trainingType))
            .OrderBy(record => record.RunNumber)
            .ToList();

        return CalculateWeightedScore(matchingRecords);
    }

    private static float CalculateWeightedScore(List<TrainingRunRecord> matchingRecords)
    {
        if (matchingRecords.Count == 0)
        {
            return 0f;
        }

        float weightedSum = 0f;
        float weightSum = 0f;

        for (int i = 0; i < matchingRecords.Count; i++)
        {
            int age = matchingRecords.Count - 1 - i;
            float weight = Mathf.Pow(RecencyDecay, age);
            weightedSum += matchingRecords[i].FocusedPercentage * weight;
            weightSum += weight;
        }

        return weightSum <= 0f ? 0f : weightedSum / weightSum;
    }

    private static float AverageDurationForType(string trainingType)
    {
        List<TrainingRunRecord> matchingRecords = runRecords
            .Where(record => IsTrainingType(record.TrainingType, trainingType))
            .ToList();

        if (matchingRecords.Count == 0)
        {
            return 0f;
        }

        return matchingRecords.Average(record => record.DurationSeconds);
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path);
        using (StreamWriter writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
        {
            writer.Write(content);
        }
    }

    private static string BuildContentTypesXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
               "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
               "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
               "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
               "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
               "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
               "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
               "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>" +
               "<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>" +
               "</Types>";
    }

    private static string BuildRootRelationshipsXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
               "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
               "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
               "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/>" +
               "</Relationships>";
    }

    private static string BuildWorkbookRelationshipsXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
               "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
               "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>" +
               "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
               "</Relationships>";
    }

    private static string BuildWorkbookXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
               "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
               "<sheets>" +
               $"<sheet name=\"{EscapeAttribute(SummarySheetName)}\" sheetId=\"1\" r:id=\"rId1\"/>" +
               $"<sheet name=\"{EscapeAttribute(RunsSheetName)}\" sheetId=\"2\" r:id=\"rId2\"/>" +
               "</sheets>" +
               "</workbook>";
    }

    private static string BuildStylesXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
               "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
               "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
               "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
               "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
               "<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
               "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs>" +
               "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
               "</styleSheet>";
    }

    private static string BuildAppPropertiesXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" " +
               "xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">" +
               "<Application>Unity</Application>" +
               "</Properties>";
    }

    private static string BuildCorePropertiesXml()
    {
        string created = XmlConvert.ToString(DateTime.UtcNow, XmlDateTimeSerializationMode.Utc);
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
               "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" " +
               "xmlns:dcterms=\"http://purl.org/dc/terms/\" " +
               "xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" " +
               "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
               "<dc:title>Training session summary</dc:title>" +
               "<dc:creator>Unity Training Session Logger</dc:creator>" +
               $"<dcterms:created xsi:type=\"dcterms:W3CDTF\">{created}</dcterms:created>" +
               $"<dcterms:modified xsi:type=\"dcterms:W3CDTF\">{created}</dcterms:modified>" +
               "</cp:coreProperties>";
    }

    private static string GetProjectRootDirectory()
    {
        DirectoryInfo assetsDirectory = Directory.GetParent(Application.dataPath);
        return assetsDirectory != null ? assetsDirectory.FullName : Application.dataPath;
    }

    private static string EscapeAttribute(string value)
    {
        return SecurityElementEscape(value);
    }

    private static string EscapeText(string value)
    {
        return SecurityElementEscape(value);
    }

    private static string SecurityElementEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    public struct TrainingRunRecord
    {
        public string SessionId;
        public DateTime SessionStartedAt;
        public string TrainingType;
        public int RunNumber;
        public DateTime StartedAt;
        public DateTime EndedAt;
        public float DurationSeconds;
        public float FocusedSeconds;
        public float FocusedPercentage;
        public float HrvThreshold;
    }

    private struct Cell
    {
        private readonly string textValue;
        private readonly float numberValue;

        public CellKind Kind { get; }
        public int StyleIndex { get; }

        private Cell(CellKind kind, string textValue, float numberValue, int styleIndex)
        {
            Kind = kind;
            this.textValue = textValue;
            this.numberValue = numberValue;
            StyleIndex = styleIndex;
        }

        public static Cell Blank()
        {
            return new Cell(CellKind.Blank, string.Empty, 0f, 0);
        }

        public static Cell Text(string value, int styleIndex = 0)
        {
            return new Cell(CellKind.Text, value ?? string.Empty, 0f, styleIndex);
        }

        public static Cell Number(float value, int decimals, int styleIndex = 0)
        {
            string format = decimals <= 0 ? "0" : "0." + new string('0', decimals);
            float parsedValue = float.Parse(value.ToString(format, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            return new Cell(CellKind.Number, string.Empty, parsedValue, styleIndex);
        }

        public string ToXml(string reference)
        {
            if (Kind == CellKind.Blank)
            {
                return $"<c r=\"{reference}\"/>";
            }

            string style = StyleIndex > 0 ? $" s=\"{StyleIndex}\"" : string.Empty;
            if (Kind == CellKind.Number)
            {
                return $"<c r=\"{reference}\"{style}><v>{numberValue.ToString(CultureInfo.InvariantCulture)}</v></c>";
            }

            return $"<c r=\"{reference}\" t=\"inlineStr\"{style}><is><t>{EscapeText(textValue)}</t></is></c>";
        }
    }

    private enum CellKind
    {
        Blank,
        Text,
        Number
    }

    private class WorksheetBuilder
    {
        private readonly SortedDictionary<int, Cell[]> rows = new SortedDictionary<int, Cell[]>();
        private float[] columnWidths = new float[0];

        public void SetColumnWidths(params float[] widths)
        {
            columnWidths = widths ?? new float[0];
        }

        public void AddRow(int rowNumber, Cell[] cells)
        {
            rows[rowNumber] = cells;
        }

        public string Build()
        {
            StringBuilder xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            xml.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

            if (columnWidths.Length > 0)
            {
                xml.Append("<cols>");
                for (int i = 0; i < columnWidths.Length; i++)
                {
                    xml.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "<col min=\"{0}\" max=\"{0}\" width=\"{1}\" customWidth=\"1\"/>",
                        i + 1,
                        columnWidths[i]);
                }
                xml.Append("</cols>");
            }

            xml.Append("<sheetData>");
            foreach (KeyValuePair<int, Cell[]> row in rows)
            {
                xml.AppendFormat(CultureInfo.InvariantCulture, "<row r=\"{0}\">", row.Key);
                for (int columnIndex = 0; columnIndex < row.Value.Length; columnIndex++)
                {
                    string reference = GetCellReference(columnIndex + 1, row.Key);
                    xml.Append(row.Value[columnIndex].ToXml(reference));
                }
                xml.Append("</row>");
            }

            xml.Append("</sheetData>");
            xml.Append("</worksheet>");
            return xml.ToString();
        }

        private static string GetCellReference(int columnNumber, int rowNumber)
        {
            StringBuilder columnName = new StringBuilder();
            int dividend = columnNumber;

            while (dividend > 0)
            {
                int modulo = (dividend - 1) % 26;
                columnName.Insert(0, Convert.ToChar('A' + modulo));
                dividend = (dividend - modulo) / 26;
            }

            return columnName + rowNumber.ToString(CultureInfo.InvariantCulture);
        }
    }
}
