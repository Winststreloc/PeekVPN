namespace PeekVPN.Core.Vpn;

/// <summary>
/// A network route managed by the platform routing manager.
/// </summary>
public sealed record Route(string Destination, string? Gateway, string? InterfaceName, int? Metric = null);
