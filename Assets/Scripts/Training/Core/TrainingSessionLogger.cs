using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using UnityEngine.SceneManagement;

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
        if (string.IsNullOrWhiteSpace(record.TrainingScene))
        {
            record.TrainingScene = SceneManager.GetActiveScene().name;
        }
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
        sheet.SetColumnWidths(16, 16, 24, 22, 20, 20, 14, 24, 20, 24, 20);

        sheet.AddRow(1, new[]
        {
            Cell.Text("session id", 1),
            Cell.Text("date", 1),
            Cell.Text("auditory training length", 1),
            Cell.Text("visual training length", 1),
            Cell.Text("started", 1),
            Cell.Text("last updated", 1),
            Cell.Text("decay d", 1),
            Cell.Text("auditory weighted sum", 1),
            Cell.Text("auditory weight sum", 1),
            Cell.Text("visual weighted sum", 1),
            Cell.Text("visual weight sum", 1)
        });

        DateTime lastUpdatedAt = runRecords.Count > 0 ? runRecords[runRecords.Count - 1].EndedAt : DateTime.Now;
        int lastRunSheetRow = runRecords.Count + 1;
        TrainingStatistics statistics = CalculateTrainingStatistics();

        sheet.AddRow(2, new[]
        {
            Cell.Text(sessionId),
            Cell.Text(sessionStartedAt.ToString("ddMMyyyy", CultureInfo.InvariantCulture)),
            Cell.Formula("IFERROR(AVERAGEIF('Training Runs'!$C:$C,\"*auditory*\",'Training Runs'!$H:$H),\"\")", statistics.AuditoryCount > 0 ? statistics.AuditoryDurationAverage : (float?)null),
            Cell.Formula("IFERROR(AVERAGEIF('Training Runs'!$C:$C,\"*visual*\",'Training Runs'!$H:$H),\"\")", statistics.VisualCount > 0 ? statistics.VisualDurationAverage : (float?)null),
            Cell.Text(sessionStartedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            Cell.Text(lastUpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            Cell.Number(RecencyDecay, 2),
            runRecords.Count > 0 ? Cell.Formula($"'Training Runs'!M{lastRunSheetRow}", statistics.AuditoryWeightedSum) : Cell.Blank(),
            runRecords.Count > 0 ? Cell.Formula($"'Training Runs'!N{lastRunSheetRow}", statistics.AuditoryWeightSum) : Cell.Blank(),
            runRecords.Count > 0 ? Cell.Formula($"'Training Runs'!P{lastRunSheetRow}", statistics.VisualWeightedSum) : Cell.Blank(),
            runRecords.Count > 0 ? Cell.Formula($"'Training Runs'!Q{lastRunSheetRow}", statistics.VisualWeightSum) : Cell.Blank()
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
            int runSheetRow = record.RunNumber + 1;
            float focusedPercentage = CalculateFocusedPercentage(record);
            bool isAuditory = IsTrainingMode(record.TrainingType, "auditory");
            bool isVisual = IsTrainingMode(record.TrainingType, "visual");
            RunningWeightedAverages running = CalculateRunningWeightedAverages(record.RunNumber);

            sheet.AddRow(rowNumber, new[]
            {
                Cell.Blank(),
                Cell.Formula($"IF(ISNUMBER(SEARCH(\"auditory\",'Training Runs'!C{runSheetRow})),'Training Runs'!J{runSheetRow},\"\")", isAuditory ? focusedPercentage : (float?)null),
                Cell.Formula($"IF(ISNUMBER(SEARCH(\"visual\",'Training Runs'!C{runSheetRow})),'Training Runs'!J{runSheetRow},\"\")", isVisual ? focusedPercentage : (float?)null),
                Cell.Formula($"'Training Runs'!O{runSheetRow}", running.AuditoryWeightSum > 0f ? running.AuditoryAverage : (float?)null),
                Cell.Formula($"'Training Runs'!R{runSheetRow}", running.VisualWeightSum > 0f ? running.VisualAverage : (float?)null),
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
                Cell.Formula("IFERROR(AVERAGEIF('Training Runs'!$C:$C,\"*auditory*\",'Training Runs'!$J:$J),\"\")", statistics.AuditoryCount > 0 ? statistics.AuditoryRawAverage : (float?)null),
                Cell.Formula("IFERROR(AVERAGEIF('Training Runs'!$C:$C,\"*visual*\",'Training Runs'!$J:$J),\"\")", statistics.VisualCount > 0 ? statistics.VisualRawAverage : (float?)null),
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
                Cell.Formula($"'Training Runs'!O{lastRunSheetRow}", statistics.AuditoryWeightSum > 0f ? statistics.AuditoryWeightedAverage : (float?)null),
                Cell.Formula($"'Training Runs'!R{lastRunSheetRow}", statistics.VisualWeightSum > 0f ? statistics.VisualWeightedAverage : (float?)null),
                Cell.Blank()
            });
        }

        return sheet.Build();
    }

    private static string BuildRunsSheetXml()
    {
        WorksheetBuilder sheet = new WorksheetBuilder();
        sheet.SetColumnWidths(14, 12, 18, 20, 10, 22, 22, 16, 16, 18, 18, 16, 24, 18, 26, 22, 18, 24, 14);
        sheet.AddRow(1, new[]
        {
            Cell.Text("session id", 1),
            Cell.Text("date", 1),
            Cell.Text("training type", 1),
            Cell.Text("training scene", 1),
            Cell.Text("run", 1),
            Cell.Text("started timestamp", 1),
            Cell.Text("ended timestamp", 1),
            Cell.Text("duration seconds", 1),
            Cell.Text("focused seconds", 1),
            Cell.Text("focused percentage", 1),
            Cell.Text("auditory run index", 1),
            Cell.Text("visual run index", 1),
            Cell.Text("auditory weighted sum", 1),
            Cell.Text("auditory weight sum", 1),
            Cell.Text("auditory weighted average", 1),
            Cell.Text("visual weighted sum", 1),
            Cell.Text("visual weight sum", 1),
            Cell.Text("visual weighted average", 1),
            Cell.Text("load z threshold", 1)
        });

        int rowNumber = 2;
        RunningWeightedAverages running = new RunningWeightedAverages();
        foreach (TrainingRunRecord record in runRecords)
        {
            float focusedPercentage = CalculateFocusedPercentage(record);
            bool isAuditory = IsTrainingMode(record.TrainingType, "auditory");
            bool isVisual = IsTrainingMode(record.TrainingType, "visual");

            if (isAuditory)
            {
                running.AuditoryWeightedSum = focusedPercentage + RecencyDecay * running.AuditoryWeightedSum;
                running.AuditoryWeightSum = 1f + RecencyDecay * running.AuditoryWeightSum;
            }

            if (isVisual)
            {
                running.VisualWeightedSum = focusedPercentage + RecencyDecay * running.VisualWeightedSum;
                running.VisualWeightSum = 1f + RecencyDecay * running.VisualWeightSum;
            }

            int previousRow = rowNumber - 1;
            string auditorySumFormula = rowNumber == 2
                ? $"IF(ISNUMBER(SEARCH(\"auditory\",C{rowNumber})),J{rowNumber},0)"
                : $"IF(ISNUMBER(SEARCH(\"auditory\",C{rowNumber})),J{rowNumber}+'Session Summary'!$G$2*M{previousRow},M{previousRow})";
            string auditoryWeightFormula = rowNumber == 2
                ? $"IF(ISNUMBER(SEARCH(\"auditory\",C{rowNumber})),1,0)"
                : $"IF(ISNUMBER(SEARCH(\"auditory\",C{rowNumber})),1+'Session Summary'!$G$2*N{previousRow},N{previousRow})";
            string visualSumFormula = rowNumber == 2
                ? $"IF(ISNUMBER(SEARCH(\"visual\",C{rowNumber})),J{rowNumber},0)"
                : $"IF(ISNUMBER(SEARCH(\"visual\",C{rowNumber})),J{rowNumber}+'Session Summary'!$G$2*P{previousRow},P{previousRow})";
            string visualWeightFormula = rowNumber == 2
                ? $"IF(ISNUMBER(SEARCH(\"visual\",C{rowNumber})),1,0)"
                : $"IF(ISNUMBER(SEARCH(\"visual\",C{rowNumber})),1+'Session Summary'!$G$2*Q{previousRow},Q{previousRow})";

            sheet.AddRow(rowNumber, new[]
            {
                Cell.Text(record.SessionId),
                Cell.Text(record.SessionStartedAt.ToString("ddMMyyyy", CultureInfo.InvariantCulture)),
                Cell.Text(record.TrainingType),
                Cell.Text(record.TrainingScene),
                Cell.Number(record.RunNumber, 0),
                Cell.Text(record.StartedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                Cell.Text(record.EndedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                Cell.Number(record.DurationSeconds, 2),
                Cell.Number(record.FocusedSeconds, 2),
                Cell.Formula($"IF(H{rowNumber}=0,0,I{rowNumber}/H{rowNumber}*100)", focusedPercentage),
                Cell.Formula($"IF(ISNUMBER(SEARCH(\"auditory\",C{rowNumber})),COUNTIF($C$2:C{rowNumber},\"*auditory*\"),\"\")", isAuditory ? CountModeThroughRun(record.RunNumber, "auditory") : (float?)null),
                Cell.Formula($"IF(ISNUMBER(SEARCH(\"visual\",C{rowNumber})),COUNTIF($C$2:C{rowNumber},\"*visual*\"),\"\")", isVisual ? CountModeThroughRun(record.RunNumber, "visual") : (float?)null),
                Cell.Formula(auditorySumFormula, running.AuditoryWeightedSum),
                Cell.Formula(auditoryWeightFormula, running.AuditoryWeightSum),
                Cell.Formula($"IF(N{rowNumber}=0,\"\",M{rowNumber}/N{rowNumber})", running.AuditoryWeightSum > 0f ? running.AuditoryAverage : (float?)null),
                Cell.Formula(visualSumFormula, running.VisualWeightedSum),
                Cell.Formula(visualWeightFormula, running.VisualWeightSum),
                Cell.Formula($"IF(Q{rowNumber}=0,\"\",P{rowNumber}/Q{rowNumber})", running.VisualWeightSum > 0f ? running.VisualAverage : (float?)null),
                Cell.Number(record.LoadZThreshold, 2)
            });
            rowNumber++;
        }

        return sheet.Build();
    }

    private static float CalculateFocusedPercentage(TrainingRunRecord record)
    {
        return record.DurationSeconds == 0f ? 0f : record.FocusedSeconds / record.DurationSeconds * 100f;
    }

    private static bool IsTrainingMode(string trainingType, string mode)
    {
        return !string.IsNullOrEmpty(trainingType) &&
               trainingType.IndexOf(mode, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int CountModeThroughRun(int runNumber, string mode)
    {
        int count = 0;
        int limit = Math.Min(runNumber, runRecords.Count);
        for (int i = 0; i < limit; i++)
        {
            if (IsTrainingMode(runRecords[i].TrainingType, mode))
            {
                count++;
            }
        }
        return count;
    }

    private static RunningWeightedAverages CalculateRunningWeightedAverages(int runNumber)
    {
        RunningWeightedAverages result = new RunningWeightedAverages();
        int limit = Math.Min(runNumber, runRecords.Count);
        for (int i = 0; i < limit; i++)
        {
            TrainingRunRecord record = runRecords[i];
            float percentage = CalculateFocusedPercentage(record);
            if (IsTrainingMode(record.TrainingType, "auditory"))
            {
                result.AuditoryWeightedSum = percentage + RecencyDecay * result.AuditoryWeightedSum;
                result.AuditoryWeightSum = 1f + RecencyDecay * result.AuditoryWeightSum;
            }
            if (IsTrainingMode(record.TrainingType, "visual"))
            {
                result.VisualWeightedSum = percentage + RecencyDecay * result.VisualWeightedSum;
                result.VisualWeightSum = 1f + RecencyDecay * result.VisualWeightSum;
            }
        }
        return result;
    }

    private static TrainingStatistics CalculateTrainingStatistics()
    {
        TrainingStatistics result = new TrainingStatistics();
        RunningWeightedAverages weighted = CalculateRunningWeightedAverages(runRecords.Count);
        result.AuditoryWeightedSum = weighted.AuditoryWeightedSum;
        result.AuditoryWeightSum = weighted.AuditoryWeightSum;
        result.VisualWeightedSum = weighted.VisualWeightedSum;
        result.VisualWeightSum = weighted.VisualWeightSum;

        foreach (TrainingRunRecord record in runRecords)
        {
            if (IsTrainingMode(record.TrainingType, "auditory"))
            {
                result.AuditoryCount++;
                result.AuditoryDurationTotal += record.DurationSeconds;
                result.AuditoryPercentageTotal += CalculateFocusedPercentage(record);
            }
            if (IsTrainingMode(record.TrainingType, "visual"))
            {
                result.VisualCount++;
                result.VisualDurationTotal += record.DurationSeconds;
                result.VisualPercentageTotal += CalculateFocusedPercentage(record);
            }
        }
        return result;
    }

    private struct RunningWeightedAverages
    {
        public float AuditoryWeightedSum;
        public float AuditoryWeightSum;
        public float VisualWeightedSum;
        public float VisualWeightSum;
        public float AuditoryAverage => AuditoryWeightSum > 0f ? AuditoryWeightedSum / AuditoryWeightSum : 0f;
        public float VisualAverage => VisualWeightSum > 0f ? VisualWeightedSum / VisualWeightSum : 0f;
    }

    private struct TrainingStatistics
    {
        public int AuditoryCount;
        public int VisualCount;
        public float AuditoryDurationTotal;
        public float VisualDurationTotal;
        public float AuditoryPercentageTotal;
        public float VisualPercentageTotal;
        public float AuditoryWeightedSum;
        public float AuditoryWeightSum;
        public float VisualWeightedSum;
        public float VisualWeightSum;
        public float AuditoryDurationAverage => AuditoryDurationTotal / AuditoryCount;
        public float VisualDurationAverage => VisualDurationTotal / VisualCount;
        public float AuditoryRawAverage => AuditoryPercentageTotal / AuditoryCount;
        public float VisualRawAverage => VisualPercentageTotal / VisualCount;
        public float AuditoryWeightedAverage => AuditoryWeightedSum / AuditoryWeightSum;
        public float VisualWeightedAverage => VisualWeightedSum / VisualWeightSum;
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
               "<calcPr calcMode=\"auto\" fullCalcOnLoad=\"1\" forceFullCalc=\"1\"/>" +
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
        public string TrainingScene;
        public int RunNumber;
        public DateTime StartedAt;
        public DateTime EndedAt;
        public float DurationSeconds;
        public float FocusedSeconds;
        public float FocusedPercentage;
        public float LoadZThreshold;
    }

    private struct Cell
    {
        private readonly string textValue;
        private readonly string formulaValue;
        private readonly float numberValue;
        private readonly bool hasCachedValue;

        public CellKind Kind { get; }
        public int StyleIndex { get; }

        private Cell(CellKind kind, string textValue, string formulaValue, float numberValue, bool hasCachedValue, int styleIndex)
        {
            Kind = kind;
            this.textValue = textValue;
            this.formulaValue = formulaValue;
            this.numberValue = numberValue;
            this.hasCachedValue = hasCachedValue;
            StyleIndex = styleIndex;
        }

        public static Cell Blank()
        {
            return new Cell(CellKind.Blank, string.Empty, string.Empty, 0f, false, 0);
        }

        public static Cell Text(string value, int styleIndex = 0)
        {
            return new Cell(CellKind.Text, value ?? string.Empty, string.Empty, 0f, false, styleIndex);
        }

        public static Cell Number(float value, int decimals, int styleIndex = 0)
        {
            string format = decimals <= 0 ? "0" : "0." + new string('0', decimals);
            float parsedValue = float.Parse(value.ToString(format, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            return new Cell(CellKind.Number, string.Empty, string.Empty, parsedValue, true, styleIndex);
        }

        public static Cell Formula(string formula, int styleIndex = 0)
        {
            return new Cell(CellKind.Formula, string.Empty, formula ?? string.Empty, 0f, false, styleIndex);
        }

        public static Cell Formula(string formula, float? cachedValue, int styleIndex = 0)
        {
            return new Cell(
                CellKind.Formula,
                string.Empty,
                formula ?? string.Empty,
                cachedValue.GetValueOrDefault(),
                cachedValue.HasValue,
                styleIndex);
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

            if (Kind == CellKind.Formula)
            {
                string cachedValue = hasCachedValue
                    ? $"<v>{numberValue.ToString(CultureInfo.InvariantCulture)}</v>"
                    : string.Empty;
                return $"<c r=\"{reference}\"{style}><f>{EscapeText(formulaValue)}</f>{cachedValue}</c>";
            }

            return $"<c r=\"{reference}\" t=\"inlineStr\"{style}><is><t>{EscapeText(textValue)}</t></is></c>";
        }
    }

    private enum CellKind
    {
        Blank,
        Text,
        Number,
        Formula
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
