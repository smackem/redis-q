﻿namespace RedisQ.Core.Runtime;

public partial class FunctionRegistry
{
    private readonly IDictionary<string, FunctionDefinition> _dict;

    public FunctionRegistry(bool ignoreCase)
    {
        var comparer = ignoreCase
            ? StringComparer.InvariantCultureIgnoreCase
            : StringComparer.InvariantCulture;
        _dict = new Dictionary<string, FunctionDefinition>(comparer);
        RegisterCommonFunctions();
        RegisterRedisFunctions();
    }

    public FunctionDefinition Resolve(string name, int parameterCount)
    {
        if (!_dict.TryGetValue(name, out var function)) throw new Exception($"function {name} not found");
        if (function.ParameterCount != parameterCount) throw new Exception($"parameter count mismatch for function {name}: expected {function.ParameterCount}, got {parameterCount}");

        return function;
    }

    public void Register(FunctionDefinition function) =>
        _dict.Add(function.Name, function);
}