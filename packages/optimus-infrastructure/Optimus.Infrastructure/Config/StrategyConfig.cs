using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimus.Infrastructure.Config;

[ExcludeFromCodeCoverage]
public class StrategyConfig
{
    public string TableName { get; set; }
    public string PublicIndexName { get; set; }
    public string UserIndexName { get; set; }
    public string StrategyHashIndexName { get; set; }
}
