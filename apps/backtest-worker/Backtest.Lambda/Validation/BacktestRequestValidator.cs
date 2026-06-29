using FluentValidation;
using MarketViewer.Contracts.Requests.Market.Backtest;

namespace Backtest.Lambda.Validation
{
    public class BacktestRequestValidator : AbstractValidator<WorkerRequest>
    {
        public BacktestRequestValidator()
        {
            //RuleFor(param => param.Timestamp.Date).GreaterThan(param => param.Timestamp.Date.AddYears(-5));
            //RuleFor(param => param.Timestamp.Date).LessThanOrEqualTo(DateTime.Now.Date.AddDays(-1));
            //RuleFor(param => param.Timestamp.DayOfWeek).NotEqual(DayOfWeek.Saturday).NotEqual(DayOfWeek.Sunday);
            //RuleFor(param => param.Filters).NotNull().NotEmpty();
        }
    }
}
