﻿namespace MScript.Evaluation
{
    public interface IFunction
    {
        string Name { get; }
        IReadOnlyList<Parameter> Parameters { get; }


        object? Apply(object?[] arguments);
    }
}
