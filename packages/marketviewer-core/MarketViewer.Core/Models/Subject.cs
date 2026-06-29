using MarketViewer.Contracts.Enums;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Core.Models;

[ExcludeFromCodeCoverage]
public class Subject
{
    public string Username { get; set; }
    public UserRole Role { get; set; }
}