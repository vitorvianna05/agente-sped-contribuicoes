using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Api.Agente.Analise;

namespace Api.Agente.Llm;

/// <summary>Tabela de preços do modelo gerador (por milhão de tokens), configurável e versionada.</summary>
internal sealed class LlmPricingOptions
{
    public string? Currency { get; set; }
    /// <summary>Origem da tabela registrada em cada tentativa.</summary>
    public string? Reference { get; set; }
    /// <summary>Versão da tabela; quando omitida, deriva dos próprios valores configurados.</summary>
    public string? Version { get; set; }
    public decimal? InputPerMillionTokens { get; set; }
    /// <summary>Preço do input em cache; ausente = mesmo preço do input.</summary>
    public decimal? CachedInputPerMillionTokens { get; set; }
    public decimal? OutputPerMillionTokens { get; set; }

    public bool IsEmpty => InputPerMillionTokens is null && CachedInputPerMillionTokens is null && OutputPerMillionTokens is null;

    /// <summary>Sem preço configurado → null (custo desconhecido); incompleto ou negativo → inválido.</summary>
    public bool IsValid => IsEmpty || (!string.IsNullOrWhiteSpace(Currency) && InputPerMillionTokens >= 0 && OutputPerMillionTokens >= 0 &&
        CachedInputPerMillionTokens is not < 0);

    public LlmPrice? ToPrice()
    {
        if (IsEmpty || !IsValid) { return null; }
        var input = InputPerMillionTokens!.Value; var output = OutputPerMillionTokens!.Value;
        var cached = CachedInputPerMillionTokens ?? input;
        var version = string.IsNullOrWhiteSpace(Version)
            ? "cfg-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', Currency,
                input.ToString(CultureInfo.InvariantCulture), cached.ToString(CultureInfo.InvariantCulture), output.ToString(CultureInfo.InvariantCulture)))))[..12]
            : Version;
        return new LlmPrice(input, cached, output, Currency!, string.IsNullOrWhiteSpace(Reference) ? "configuration:Llm:Pricing" : Reference, version);
    }
}

internal sealed record LlmCost(decimal Amount, string Currency, string Reference, string Version);

internal sealed record LlmPrice(decimal InputPerMillion, decimal CachedInputPerMillion, decimal OutputPerMillion, string Currency, string Reference, string Version)
{
    /// <summary>
    /// Custo estimado somente com input e output conhecidos. Tokens em cache não informados contam
    /// como input integral (estimativa superior); contagem de cache incoerente → custo desconhecido.
    /// </summary>
    public LlmCost? Estimate(LlmResponse? usage)
    {
        if (usage is not { InputTokens: { } input, OutputTokens: { } output } || input < 0 || output < 0) { return null; }
        var cached = usage.CachedInputTokens ?? 0;
        if (cached < 0 || cached > input) { return null; }
        var amount = ((input - cached) * InputPerMillion + cached * CachedInputPerMillion + output * OutputPerMillion) / 1_000_000m;
        return new LlmCost(amount, Currency, Reference, Version);
    }
}
