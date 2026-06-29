using FluentValidation;
using MarketViewer.Contracts.Requests.Market.Backtest;
using System;

namespace MarketViewer.Application.Validators
{
    public class BacktestRequestValidator : AbstractValidator<BacktestCreateRequest>
    {
        public BacktestRequestValidator()
        {
            //RuleFor(param => param.Start).LessThanOrEqualTo(param => param.End).GreaterThan(param => param.Start.AddYears(-15));
            //RuleFor(param => param.Start.ToString("yyyy-MM-dd")).LessThanOrEqualTo(DateTimeOffset.Now.ToString("yyyy-MM-dd"));

            //RuleFor(param => param.End).GreaterThanOrEqualTo(param => param.Start).GreaterThan(param => param.Start.AddYears(-15));
            //RuleFor(param => param.End.ToString("yyyy-MM-dd")).LessThanOrEqualTo(DateTimeOffset.Now.ToString("yyyy-MM-dd"));

            //RuleFor(param => param.Argument).NotNull();
        }
    }
}
