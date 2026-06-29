using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Expressions;

/// <summary>
/// Represents a function call expression
/// </summary>
public class FunctionCallExpression : IExpression
{
    private readonly IFunction _function;
    private readonly IExpression[] _arguments;

    public FunctionCallExpression(IFunction function, IExpression[] arguments)
    {
        _function = function;
        _arguments = arguments;
    }

    public string FunctionName => _function.Name;
    public IFunction GetFunction() => _function;
    public IReadOnlyList<IExpression> GetArguments() => _arguments;

    public object Evaluate(ExpressionContext context)
    {
        var argumentValues = new object[_arguments.Length];
        for (int i = 0; i < _arguments.Length; i++)
        {
            argumentValues[i] = _arguments[i].Evaluate(context);
        }

        return _function.Execute(argumentValues, context);
    }
}
