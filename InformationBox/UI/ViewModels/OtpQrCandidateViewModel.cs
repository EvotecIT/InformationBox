using System.Windows.Media;

namespace InformationBox.UI.ViewModels;

public sealed class OtpQrCandidateViewModel
{
    public string Payload { get; }
    public string Title { get; }
    public string Description { get; }
    public int AccountCount { get; }
    public bool IsMigration { get; }
    public string SourceLabel { get; }
    public ImageSource? PreviewImage { get; }

    public OtpQrCandidateViewModel(
        string payload,
        string title,
        string description,
        int accountCount,
        bool isMigration,
        string sourceLabel,
        ImageSource? previewImage)
    {
        Payload = payload;
        Title = title;
        Description = description;
        AccountCount = accountCount;
        IsMigration = isMigration;
        SourceLabel = sourceLabel;
        PreviewImage = previewImage;
    }
}
