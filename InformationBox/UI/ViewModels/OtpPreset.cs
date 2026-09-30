using AuthIMO.Provisioning;

namespace InformationBox.UI.ViewModels;

public sealed class OtpPreset
{
    public string Name { get; }
    public OtpProvisioningProfile Profile { get; }
    public string Description { get; }

    public OtpPreset(string name, OtpProvisioningProfile profile, string description)
    {
        Name = name;
        Profile = profile;
        Description = description;
    }
}
