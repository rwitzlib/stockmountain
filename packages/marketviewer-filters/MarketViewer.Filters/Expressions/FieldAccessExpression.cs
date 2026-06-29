using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Expressions;

/// <summary>
/// Expression that accesses a specific field from another expression's result
/// </summary>
public class FieldAccessExpression : IExpression
{
    private readonly IExpression _targetExpression;
    private readonly string _fieldName;

    public FieldAccessExpression(IExpression targetExpression, string fieldName)
    {
        _targetExpression = targetExpression;
        _fieldName = fieldName;
    }

    public IExpression GetTargetExpression() => _targetExpression;
    public string GetFieldName() => _fieldName;

    public object Evaluate(ExpressionContext context)
    {
        var targetResult = _targetExpression.Evaluate(context);

        // If the target returns a list of indicator results, we need to extract the field from each
        if (targetResult is List<IIndicatorResult> indicatorResults)
        {
            // For series results, we need to return a list of field values
            // This maintains compatibility with the series-based operators
            return indicatorResults.Select(r => r.GetFieldValue(_fieldName)).ToList();
        }
        else if (targetResult is IIndicatorResult singleResult)
        {
            // For single results (if any function returns a single IIndicatorResult)
            return singleResult.GetFieldValue(_fieldName);
        }
        else
        {
            throw new InvalidOperationException($"Cannot access field '{_fieldName}' on result of type {targetResult.GetType().Name}");
        }
    }
}
