using System.Diagnostics;
using RedisQ.Core.Redis;
using RedisQ.Core.Runtime;

namespace RedisQ.Core.Test;

public class IntegrationTestBase : TestBase, IDisposable
{
    private readonly Process _redisProcess;
    private const int Port = 55666;

    protected IntegrationTestBase()
    {
        var psi = new ProcessStartInfo
        {
            FileName = GetDbExecutablePath(),
            Arguments = $"--port {Port} --save \"\" --appendonly no",
            RedirectStandardOutput = true,
        };
        _redisProcess = Process.Start(psi)!;
        WaitUntilReady(_redisProcess.StandardOutput).Wait();
    }

    private static string GetDbExecutablePath()
    {
        if (!OperatingSystem.IsWindows()) return "redis-server";

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var packagePath = string.Join(Path.DirectorySeparatorChar, homeDir, ".nuget", "packages", "memuraideveloper");
        var latestPackageVersion = Directory.EnumerateDirectories(packagePath)
            .Select(path => Version.Parse(Path.GetFileName(path)))
            .Max();
        return string.Join(Path.DirectorySeparatorChar, packagePath, latestPackageVersion, "tools", "memurai.exe");
    }

    private static async Task WaitUntilReady(TextReader reader)
    {
        var timeout = TimeSpan.FromSeconds(10);
        while (true)
        {
            var line = await reader.ReadLineAsync().WaitAsync(timeout);
            if (line?.Contains("Ready to accept connections") == true) break;
        }
    }

    private protected static IRedisConnection Connect() => new RedisConnection($"localhost:{Port}");

    private protected static Task<Value> Eval(Expr expr, IRedisConnection redis)
    {
        var ctx = Context.Root(redis, Helpers.DefaultFunctions);
        return expr.Evaluate(ctx);
    }

    private protected static Task<Value> Interpret(string source, IRedisConnection redis)
    {
        var expr = Compile(source);
        return Eval(expr, redis);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _redisProcess.Kill();
        _redisProcess.WaitForExit(1000);
        _redisProcess.Dispose();
    }
}
