using System.Security.Cryptography;
using System.Text;
using Api.Agente.Infrastructure;
using Api.Agente.Validacao;
using Microsoft.Extensions.Options;

namespace Api.Agente.Tests.Unit;

/// <summary>Fixtures textuais sintéticas verificam leitura, nunca conformidade tributária.</summary>
public sealed class SpedParserTests
{
    internal static SpedParser Create(long bytes = 10000, int lineBytes = 1024, int records = 100, int fields = 32, int issues = 20) =>
        new(Options.Create(new AgenteOptions { Limits = new AgentLimits { MaxRequestBytes = bytes } }),
            Options.Create(new SpedParserOptions { MaxLineBytes = lineBytes, MaxRecords = records, MaxFieldsPerRecord = fields, MaxIssues = issues }));

    /// <summary>Latin-1, campos vazios/finais, casas decimais, espaços e ordem permanecem intactos.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8192)]
    public async Task ReadPreservesOriginalFieldsAcrossBufferBoundaries(int chunkSize)
    {
        const string source = "|0000|006|\r\n|0150|0001|João||00123|123,4500|  texto  |\"aspas\"||\r\n|9999|3|\r\n";
        var bytes = Encoding.Latin1.GetBytes(source);
        await using var stream = new ChunkedStream(bytes, chunkSize);
        var document = await Create().ReadAsync(stream, TestContext.Current.CancellationToken);
        Assert.Empty(document.Issues);
        Assert.Equal("006", document.DeclaredLayoutCode);
        Assert.Equal("iso-8859-1", document.Encoding);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), document.Sha256);
        Assert.Equal(bytes.Length, document.BytesRead);
        Assert.Equal(3, document.PhysicalLineCount);
        var record = document.Records[1];
        Assert.Equal(2, record.LineNumber);
        Assert.Equal("0150", record.GetField(1));
        Assert.Equal("0001", record.GetField(2));
        Assert.Equal(["0001", "João", "", "00123", "123,4500", "  texto  ", "\"aspas\"", ""], record.Fields);
        Assert.Equal("|0150|0001|João||00123|123,4500|  texto  |\"aspas\"||", record.RawLine);
        Assert.All(document.Records, item => Assert.Equal("CRLF", item.LineEnding));
        Assert.True(stream.CanRead);
        Assert.Throws<ArgumentOutOfRangeException>(() => record.GetField(0));
    }

    /// <summary>Linhas incorretas são localizadas sem retornar seus valores nas ocorrências.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("", "EMPTY_LINE")]
    [InlineData(" |C100|valor|", "INVALID_RECORD_DELIMITERS")]
    [InlineData("|C100|valor", "INVALID_RECORD_DELIMITERS")]
    [InlineData("|c100|valor|", "INVALID_RECORD_CODE")]
    [InlineData("|C10|valor|", "INVALID_RECORD_CODE")]
    [InlineData("|C100|segredo\t|", "CONTROL_CHARACTER_NOT_ALLOWED")]
    [InlineData("|C100|segredo\0|", "CONTROL_CHARACTER_NOT_ALLOWED")]
    public async Task MalformedLineHasExplicitLocation(string line, string code)
    {
        await using var stream = new MemoryStream(Encoding.Latin1.GetBytes("|0000|006|\r\n" + line + "\r\n|9999|3|\r\n"));
        var document = await Create().ReadAsync(stream, TestContext.Current.CancellationToken);
        Assert.Contains(document.Issues, issue => issue.LineNumber == 2 && issue.Code == code);
        Assert.DoesNotContain(document.Issues, issue => issue.Code.Contains("segredo", StringComparison.Ordinal));
    }

    /// <summary>Não normalizar LF/EOF evita esconder divergência do arquivo original.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("|0000|006|\n|9999|2|\n", "LF")]
    [InlineData("|0000|006|\r\n|9999|2|", "EOF")]
    public async Task NonCrLfInputIsReadButMarkedInvalid(string text, string ending)
    {
        await using var stream = new MemoryStream(Encoding.Latin1.GetBytes(text));
        var document = await Create().ReadAsync(stream, TestContext.Current.CancellationToken);
        Assert.Contains(document.Issues, issue => issue.Code == "CRLF_REQUIRED");
        Assert.Equal(ending, document.Records[^1].LineEnding);
    }

    /// <summary>BOM incompatível não é removido nem convertido silenciosamente.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("utf8")]
    [InlineData("utf16")]
    [InlineData("utf32")]
    public async Task UnsupportedEncodingIsExplicit(string encoding)
    {
        var bytes = encoding switch { "utf8" => new byte[] { 239, 187, 191 }, "utf16" => [255, 254], _ => [0, 0, 254, 255] };
        await using var stream = new ChunkedStream(bytes, 1);
        var exception = await Assert.ThrowsAsync<SpedReadException>(() => Create().ReadAsync(stream, TestContext.Current.CancellationToken));
        Assert.Equal("SPED_ENCODING_NOT_SUPPORTED", exception.Code);
    }

    /// <summary>Limites falham explicitamente, sem devolver uma leitura truncada como sucesso.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("bytes", "SPED_FILE_TOO_LARGE")]
    [InlineData("line", "SPED_LINE_TOO_LONG")]
    [InlineData("records", "SPED_RECORD_LIMIT_EXCEEDED")]
    [InlineData("fields", "SPED_FIELD_LIMIT_EXCEEDED")]
    [InlineData("issues", "SPED_ISSUE_LIMIT_EXCEEDED")]
    public async Task LimitsCannotProduceSuccessfulPartialDocument(string limit, string code)
    {
        var parser = Create(bytes: limit == "bytes" ? 5 : 10000, lineBytes: limit == "line" ? 5 : 1024,
            records: limit == "records" ? 1 : 100, fields: limit == "fields" ? 2 : 32, issues: limit == "issues" ? 1 : 20);
        var text = limit == "issues" ? "\r\n\r\n" : "|0000|006|\r\n|C100|a|b|\r\n|9999|3|\r\n";
        await using var stream = new MemoryStream(Encoding.Latin1.GetBytes(text));
        Assert.Equal(code, (await Assert.ThrowsAsync<SpedReadException>(() => parser.ReadAsync(stream, TestContext.Current.CancellationToken))).Code);
    }

    /// <summary>Arquivo vazio e cancelamento permanecem falhas explícitas.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task EmptyAndCancelledReadsAreRejected()
    {
        await using var empty = new MemoryStream();
        Assert.Equal("SPED_FILE_EMPTY", (await Assert.ThrowsAsync<SpedReadException>(() => Create().ReadAsync(empty, TestContext.Current.CancellationToken))).Code);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().ReadAsync(empty, cancellation.Token));
    }

    /// <summary>Contagem de linhas inclui o próprio 9999; código declarado não aprova compatibilidade.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("|0000|ABC|\r\n|9999|2|\r\n", "DECLARED_LAYOUT_CODE_INVALID")]
    [InlineData("|C100|x|\r\n|9999|2|\r\n", "OPENING_RECORD_MISSING")]
    [InlineData("|C100|x|\r\n|0000|006|\r\n|9999|3|\r\n", "OPENING_RECORD_NOT_FIRST")]
    [InlineData("|0000|006|\r\n", "CLOSING_RECORD_MISSING")]
    [InlineData("|0000|006|\r\n|9999|1|\r\n", "DECLARED_LINE_COUNT_MISMATCH")]
    [InlineData("|0000|006|\r\n|9999|x|\r\n", "DECLARED_LINE_COUNT_INVALID")]
    [InlineData("|0000|006|\r\n|0000|006|\r\n|9999|3|\r\n", "OPENING_RECORD_DUPLICATED")]
    [InlineData("|0000|006|\r\n|9999|4|\r\n|C100|x|\r\n|9999|4|\r\n", "CLOSING_RECORD_NOT_LAST")]
    public async Task FileEnvelopeInconsistenciesAreReported(string text, string code)
    {
        await using var stream = new MemoryStream(Encoding.Latin1.GetBytes(text));
        var document = await Create().ReadAsync(stream, TestContext.Current.CancellationToken);
        Assert.Contains(document.Issues, issue => issue.Code == code);
    }

    private sealed class ChunkedStream(byte[] bytes, int chunkSize) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);
    }
}
