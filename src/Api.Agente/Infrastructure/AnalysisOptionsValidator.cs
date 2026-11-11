using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Llm;
using Api.Agente.Validacao;
using Microsoft.Extensions.Options;

namespace Api.Agente.Infrastructure;

internal sealed class AnalysisOptionsValidator : IValidateOptions<LlmOptions>, IValidateOptions<AgentDataOptions>, IValidateOptions<LayoutPolicyOptions>
{
    public ValidateOptionsResult Validate(string? name, LlmOptions value) =>
        !string.IsNullOrWhiteSpace(value.Model) && value.TimeoutSeconds > 0 && value.MaxAttempts > 0 &&
        value.RetryDelayMilliseconds >= 0 && value.MaxOutputTokens > 0 && value.MaxContextCharacters > 0 && value.MaxOutputCharacters > 0
            ? value.Pricing is not null && value.Pricing.IsValid ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail("Llm:Pricing exige Currency, InputPerMillionTokens e OutputPerMillionTokens nao negativos quando houver preco.")
            : ValidateOptionsResult.Fail("Llm exige modelo e limites finitos de timeout/tentativas/contexto/saida.");

    public ValidateOptionsResult Validate(string? name, AgentDataOptions value) => value.TimeoutSeconds > 0 && value.RetentionBatchSize > 0 &&
        value.AuditDays is not <= 0 && value.ConsumptionDays is not <= 0 &&
        !(value.AuditDays.HasValue && value.ConsumptionDays.HasValue && value.AuditDays < value.ConsumptionDays)
        ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("AgentData exige timeout/lote positivos e auditoria >= consumo; retencao ausente permanece pendente, nunca zero.");

    public ValidateOptionsResult Validate(string? name, LayoutPolicyOptions value)
    {
        if (value.Profiles is null || value.Profiles.Any(profile => profile is null))
        { return ValidateOptionsResult.Fail("LayoutPolicy.Profiles nao pode conter valores nulos."); }
        var profiles = value.Profiles.Select(profile => profile.ToDomain()).ToArray();
        for (var index = 0; index < profiles.Length; index++)
        {
            if (!profiles[index].IsDefined || profiles.Skip(index + 1).Any(profiles[index].Overlaps))
            { return ValidateOptionsResult.Fail("LayoutPolicy.Profiles exige codigo/periodo 2020-2026, mapeamento RAG/fonte/aprovacao e intervalos sem ambiguidade."); }
        }
        return ValidateOptionsResult.Success;
    }
}
