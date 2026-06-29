using System.Diagnostics.CodeAnalysis;

namespace Kesha
{
    [ExcludeFromCodeCoverage]
    public class TickerDetailsRequest
    {
        public string Market { get; set; } = "stocks";
        public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    }
}
