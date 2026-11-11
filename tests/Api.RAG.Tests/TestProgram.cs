using Api.RAG.Tests.Fixtures;

namespace Api.RAG.Tests;

internal static class TestProgram
{
    public static Task<int> Main(string[] args)
    {
        if (args is ["--mcp-consumer"])
        {
            return McpConsumerProcess.RunChildAsync();
        }

        if (args is ["--rag-server"])
        {
            return RagServerProcess.RunChildAsync();
        }

        if (args.Any(arg => arg is "-automated" or "@@"))
        {
            return Xunit.Runner.InProc.SystemConsole.ConsoleRunner.Run(args);
        }

        return Xunit.MicrosoftTestingPlatform.TestPlatformTestFramework.RunAsync(
            args, SelfRegisteredExtensions.AddSelfRegisteredExtensions);
    }
}
