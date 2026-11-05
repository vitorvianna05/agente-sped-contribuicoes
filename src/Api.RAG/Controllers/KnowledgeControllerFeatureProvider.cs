using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Api.RAG.Controllers;

// Registro explícito permite adaptadores internos com dependências tipadas sem expor domínio na API .NET pública.
internal sealed class KnowledgeControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo) => typeInfo.AsType() == typeof(IngestController) ||
        typeInfo.AsType() == typeof(IngestionStatusController) || base.IsController(typeInfo);
}
