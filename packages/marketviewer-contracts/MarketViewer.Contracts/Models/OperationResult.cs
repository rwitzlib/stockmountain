using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace MarketViewer.Contracts.Models
{
    [ExcludeFromCodeCoverage]
    public class OperationResult<TType>
    {
        public HttpStatusCode Status { get; set; }
        public IEnumerable<string> ErrorMessages { get; set; }
        public TType Data { get; set; }
    }
}
