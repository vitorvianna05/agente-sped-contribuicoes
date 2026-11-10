using System.Security.Cryptography;
using System.Text;
using Api.Agente.Domain;
using Api.Agente.Infrastructure;
using Microsoft.Extensions.Options;

namespace Api.Agente.Validacao;

internal sealed class SpedParser(IOptions<AgenteOptions> configured, IOptions<SpedParserOptions> parserOptions) : ISpedParser
{
    public async Task<SpedDocument> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var options = parserOptions.Value;
        if (configured.Value.Limits.MaxRequestBytes <= 0 || options.MaxLineBytes <= 0 || options.MaxRecords <= 0 ||
            options.MaxFieldsPerRecord < 2 || options.MaxIssues <= 0)
        {
            throw new SpedReadException("PARSER_NOT_CONFIGURED");
        }
        var records = new List<SpedRecord>();
        var issues = new List<SpedReadIssue>();
        var line = new List<byte>();
        var lines = 0;
        long total = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += read;
            if (total > configured.Value.Limits.MaxRequestBytes)
            {
                throw new SpedReadException("SPED_FILE_TOO_LARGE");
            }
            hash.AppendData(buffer, 0, read);
            for (var index = 0; index < read; index++)
            {
                var value = buffer[index];
                if (value == 10)
                {
                    var crlf = line.Count > 0 && line[^1] == 13;
                    if (crlf) { line.RemoveAt(line.Count - 1); }
                    ReadLine(crlf ? "CRLF" : "LF");
                    line.Clear();
                }
                else
                {
                    line.Add(value);
                    if (lines == 0 && HasUnsupportedBom(line))
                    {
                        throw new SpedReadException("SPED_ENCODING_NOT_SUPPORTED", 1);
                    }
                    if ((long)line.Count > (long)options.MaxLineBytes + 1)
                    {
                        throw new SpedReadException("SPED_LINE_TOO_LONG", lines + 1);
                    }
                }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (total == 0) { throw new SpedReadException("SPED_FILE_EMPTY"); }
        if (line.Count > 0) { ReadLine("EOF"); }
        foreach (var issue in SpedFileStructure.Check(records, lines)) { AddIssue(issue); }
        var declared = records.Count > 0 && records[0].Code == "0000" && records[0].Fields.Length > 0
            ? records[0].Fields[0] : null;
        return new SpedDocument("iso-8859-1", Convert.ToHexStringLower(hash.GetHashAndReset()), total, lines,
            declared, records.ToArray(), issues.ToArray());

        void AddIssue(SpedReadIssue issue)
        {
            if (issues.Count >= options.MaxIssues) { throw new SpedReadException("SPED_ISSUE_LIMIT_EXCEEDED", issue.LineNumber); }
            issues.Add(issue);
        }

        void ReadLine(string ending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines++;
            if (line.Count > options.MaxLineBytes) { throw new SpedReadException("SPED_LINE_TOO_LONG", lines); }
            if (ending != "CRLF") { AddIssue(new(lines, "CRLF_REQUIRED")); }
            var raw = Encoding.Latin1.GetString(line.ToArray());
            if (raw.Length == 0) { AddIssue(new(lines, "EMPTY_LINE")); return; }
            if (raw.Any(character => character < 32 || character == 127))
            {
                AddIssue(new(lines, "CONTROL_CHARACTER_NOT_ALLOWED"));
                return;
            }
            if (raw[0] != '|' || raw[^1] != '|' || raw.Length < 6)
            {
                AddIssue(new(lines, "INVALID_RECORD_DELIMITERS"));
                return;
            }
            // Não remover campos vazios, aspas, zeros ou espaços; não é CSV.
            var values = raw[1..^1].Split('|', StringSplitOptions.None);
            if (values.Length > options.MaxFieldsPerRecord) { throw new SpedReadException("SPED_FIELD_LIMIT_EXCEEDED", lines); }
            if (values[0].Length != 4 || values[0].Any(character => !char.IsAsciiDigit(character) && character is not (>= 'A' and <= 'Z')))
            {
                AddIssue(new(lines, "INVALID_RECORD_CODE"));
                return;
            }
            if (records.Count >= options.MaxRecords) { throw new SpedReadException("SPED_RECORD_LIMIT_EXCEEDED", lines); }
            records.Add(new SpedRecord(lines, values[0], values[1..], raw, ending));
        }
    }

    private static bool HasUnsupportedBom(List<byte> bytes) =>
        bytes.Count == 2 && ((bytes[0] == 255 && bytes[1] == 254) || (bytes[0] == 254 && bytes[1] == 255)) ||
        bytes.Count == 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 ||
        bytes.Count == 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 254 && bytes[3] == 255;
}
