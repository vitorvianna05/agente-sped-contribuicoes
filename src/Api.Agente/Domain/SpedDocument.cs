using System.Globalization;

namespace Api.Agente.Domain;

internal sealed record SpedRecord(int LineNumber, string Code, string[] Fields, string RawLine, string LineEnding)
{
    // Posição 1 é REG; posição 2 é Fields[0], conforme numeração do guia.
    public string GetField(int position) => position == 1 ? Code :
        position >= 2 && position <= Fields.Length + 1 ? Fields[position - 2] :
        throw new ArgumentOutOfRangeException(nameof(position));
}

internal sealed record SpedReadIssue(int LineNumber, string Code);

internal sealed record SpedDocument(string Encoding, string Sha256, long BytesRead, int PhysicalLineCount,
    string? DeclaredLayoutCode, SpedRecord[] Records, SpedReadIssue[] Issues)
{
    public int ParsedRecordCount => Records.Length;
    public int RejectedLineCount => PhysicalLineCount - Records.Length;
}

internal static class SpedFileStructure
{
    public static SpedReadIssue[] Check(IReadOnlyList<SpedRecord> records, int physicalLineCount)
    {
        var issues = new List<SpedReadIssue>();
        if (records.Count == 0 || records[0].Code != "0000")
        {
            issues.Add(new(1, "OPENING_RECORD_MISSING"));
        }
        else if (records[0].Fields.Length == 0 || records[0].Fields[0].Length != 3 ||
            records[0].Fields[0].Any(character => !char.IsAsciiDigit(character)))
        {
            issues.Add(new(records[0].LineNumber, "DECLARED_LAYOUT_CODE_INVALID"));
        }
        var openingSeen = records.Count > 0 && records[0].Code == "0000";
        foreach (var record in records.Skip(1).Where(record => record.Code == "0000"))
        {
            issues.Add(new(record.LineNumber, openingSeen ? "OPENING_RECORD_DUPLICATED" : "OPENING_RECORD_NOT_FIRST"));
            openingSeen = true;
        }
        if (records.Count == 0 || records[^1].Code != "9999")
        {
            issues.Add(new(Math.Max(1, physicalLineCount), "CLOSING_RECORD_MISSING"));
        }
        else
        {
            var closing = records[^1];
            if (closing.Fields.Length != 1 || !int.TryParse(closing.Fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                issues.Add(new(closing.LineNumber, "DECLARED_LINE_COUNT_INVALID"));
            }
            else if (count != physicalLineCount)
            {
                issues.Add(new(closing.LineNumber, "DECLARED_LINE_COUNT_MISMATCH"));
            }
        }
        foreach (var record in records.Take(Math.Max(0, records.Count - 1)).Where(record => record.Code == "9999"))
        {
            issues.Add(new(record.LineNumber, "CLOSING_RECORD_NOT_LAST"));
        }
        return issues.ToArray();
    }
}
