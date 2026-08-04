using System.Collections.ObjectModel;
using PeekVPN.App.Localization;

namespace PeekVPN.App.ViewModels;

/// <summary>
/// Presentation-only content for the optional VPN feature cards.
/// </summary>
public sealed class FeatureCardsViewModel : ViewModelBase
{
    public ObservableCollection<FeatureCardViewModel> Cards { get; } =
    [
        new(Strings.FeatureThreatTitle, Strings.FeatureThreatBadge, Strings.FeatureThreatBody, "S", false),
        new(Strings.FeatureDarkWebTitle, Strings.FeatureDarkWebBadge, Strings.FeatureDarkWebBody, "@", true),
        new(Strings.FeatureDoubleVpnTitle, Strings.FeatureDoubleVpnBadge, Strings.FeatureDoubleVpnBody, "D", false),
    ];
}

public sealed class FeatureCardViewModel(
    string title,
    string badge,
    string description,
    string iconText,
    bool isActive) : ViewModelBase
{
    public string Title { get; } = title;
    public string Badge { get; } = badge;
    public string Description { get; } = description;
    public string IconText { get; } = iconText;
    public bool IsActive { get; } = isActive;
}
