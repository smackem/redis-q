using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
            FileName = "redis-server",
            Arguments = $"--port {Port} --save \"\" --appendonly no",
            RedirectStandardOutput = true,
        };
        _redisProcess = Process.Start(psi)!;
        WaitUntilReady(_redisProcess.StandardOutput);
    }

    private static void WaitUntilReady(TextReader reader)
    {
        while (reader.ReadLine()?.Contains("Ready to accept connections") == false)
        {
        }
    }

    private protected static IRedisConnection Connect() => new RedisConnection($"localhost:{Port}");

    private protected static Task<Value> Eval(Expr expr, IRedisConnection redis)
    {
        var ctx = Context.Root(redis, Helpers.DefaultFunctions);
        return expr.Evaluate(ctx);
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _redisProcess.Kill();
        _redisProcess.WaitForExit(1000);
        _redisProcess.Dispose();
    }
}
