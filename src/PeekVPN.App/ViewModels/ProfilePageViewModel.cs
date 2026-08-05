using PeekVPN.App.Localization;

namespace PeekVPN.App.ViewModels;

/// <summary>
/// Supplies safe, non-account-backed presentation data for the profile destination.
/// </summary>
public sealed class ProfilePageViewModel : ViewModelBase
{
    public string Title => Strings.ProfilePageTitle;
    public string Subtitle => Strings.ProfilePageSubtitle;
    public string DisplayName => Strings.ProfileDisplayName;
    public string EmailAddress => Strings.ProfileEmailAddress;
    public string PlanLabel => Strings.ProfilePlanLabel;
    public string PlanValue => Strings.ProfilePlanValue;
    public string RenewsLabel => Strings.ProfileRenewsLabel;
    public string RenewsValue => Strings.ProfileRenewsValue;
    public string AccountTitle => Strings.ProfileAccountTitle;
    public string AccountSubtitle => Strings.ProfileAccountSubtitle;
    public string MemberSinceLabel => Strings.ProfileMemberSinceLabel;
    public string MemberSinceValue => Strings.ProfileMemberSinceValue;
    public string DevicesLabel => Strings.ProfileDevicesLabel;
    public string DevicesValue => Strings.ProfileDevicesValue;
    public string ProtectionTitle => Strings.ProfileProtectionTitle;
    public string ProtectionSubtitle => Strings.ProfileProtectionSubtitle;
    public string ProtectionValue => Strings.ProfileProtectionValue;
    public string ManagePlan => Strings.ProfileManagePlan;
    public string EditProfile => Strings.ProfileEditProfile;
}
