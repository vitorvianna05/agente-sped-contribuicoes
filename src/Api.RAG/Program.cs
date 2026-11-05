namespace Api.RAG;

/// <summary>Ponto de entrada da API de conhecimento fiscal.</summary>
public sealed class Program
{
    private Program()
    {
    }

    /// <summary>Inicializa o host da aplicação.</summary>
    /// <param name="args">Argumentos de inicialização.</param>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        // Endpoints and integrations belong to later phases; setup exposes no business operations.
        await app.RunAsync();
    }
}
