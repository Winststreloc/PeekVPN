using PeekVPN.App.Localization;

namespace PeekVPN.App.ViewModels;

/// <summary>
/// Supplies the presentation-only summary data for the statistics destination.
/// </summary>
public sealed class StatisticsPageViewModel : ViewModelBase
{
    public string Title => Strings.StatisticsPageTitle;
    public string Subtitle => Strings.StatisticsPageSubtitle;
    public string ProtectedTimeLabel => Strings.StatisticsProtectedTime;
    public string ProtectedTimeValue => Strings.StatisticsProtectedTimeValue;
    public string DataProtectedLabel => Strings.StatisticsDataProtected;
    public string DataProtectedValue => Strings.StatisticsDataProtectedValue;
    public string AverageSpeedLabel => Strings.StatisticsAverageSpeed;
    public string AverageSpeedValue => Strings.StatisticsAverageSpeedValue;
    public string ConnectionsLabel => Strings.StatisticsConnections;
    public string ConnectionsValue => Strings.StatisticsConnectionsValue;
    public string ActivityTitle => Strings.StatisticsActivityTitle;
    public string ActivitySubtitle => Strings.StatisticsActivitySubtitle;
    public string DownloadLabel => Strings.StatisticsDownload;
    public string UploadLabel => Strings.StatisticsUpload;
    public string LocationsTitle => Strings.StatisticsLocationsTitle;
    public string LocationsSubtitle => Strings.StatisticsLocationsSubtitle;
    public string GermanyLabel => Strings.StatisticsGermany;
    public string NetherlandsLabel => Strings.StatisticsNetherlands;
    public string UnitedStatesLabel => Strings.StatisticsUnitedStates;
    public string TodayLabel => Strings.StatisticsToday;
    public string LastSevenDaysLabel => Strings.StatisticsLastSevenDays;
}
