using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Management;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Management.User;

[ExcludeFromCodeCoverage]
public class UserReadRequest
{
    public string UserId { get; set; }
}

