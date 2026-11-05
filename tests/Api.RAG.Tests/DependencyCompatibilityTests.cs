using Npgsql;

namespace Api.RAG.Tests;

/// <summary>Verifica a integração local dos pacotes fixados na T004, sem banco ou rede.</summary>
public sealed class DependencyCompatibilityTests
{
    /// <summary>Detecta incompatibilidade binária no registro do plugin Pgvector com Npgsql.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void PgvectorRegistersWithSelectedNpgsqlWithoutOpeningConnection()
    {
        var builder = new NpgsqlDataSourceBuilder(
            "Host=localhost;Database=synthetic_dependency_check;Username=synthetic");

        builder.UseVector();
        using var dataSource = builder.Build();

        Assert.NotNull(dataSource);
    }
}
