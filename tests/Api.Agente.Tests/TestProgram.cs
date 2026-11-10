using Api.Agente.Tests.Fixtures;

namespace Api.Agente.Tests;

internal static class TestProgram
{
    public static async Task<int> Main(string[] args)
    {
        if (args is ["--knowledge-server"])
        {
            return await KnowledgeServerProcess.RunChildAsync();
        }
        if (args.Any(argument => argument is "-automated" or "@@"))
        {
            return await Xunit.Runner.InProc.SystemConsole.ConsoleRunner.Run(args);
        }
        return await Xunit.MicrosoftTestingPlatform.TestPlatformTestFramework.RunAsync(args, SelfRegisteredExtensions.AddSelfRegisteredExtensions);
    }
}
