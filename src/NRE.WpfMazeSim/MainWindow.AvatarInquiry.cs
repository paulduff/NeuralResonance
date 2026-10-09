using System.Windows;
using System.Windows.Controls;

namespace NRE.WpfMazeSim;

public partial class MainWindow
{
    private void AvatarQuestionPreset_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string question })
        {
            AvatarQuestionTextBox.Text = question;
        }
    }

    private void AvatarQuestionTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        // TextChanged also runs while InitializeComponent creates the controls.
        if (AvatarReplyText is null || RequestAvatarReplyButton is null || AvatarInquiryStatusText is null)
        {
            return;
        }

        ClearAvatarInquiry();
    }

    private void ClearAvatarInquiry()
    {
        _avatarInquiryReceipt = null;
        AvatarReplyText.Text = string.Empty;
        RequestAvatarReplyButton.IsEnabled = false;
        AvatarInquiryStatusText.Text = "Present the question, then request a reply.";
    }

    private async void PresentAvatarQuestion_OnClick(object sender, RoutedEventArgs e)
    {
        if (_avatarInquiryInFlight || _textDisplayInFlight)
        {
            return;
        }

        ClearAvatarInquiry();
        SetAvatarInquiryBusy(true);
        AvatarInquiryStatusText.Text = "Presenting the question...";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            var presentation = await NRE.SimAvatar.AvatarInquiryApi.PresentQuestionAsync(
                _avatarInquiryHttpClient,
                GetAvatarInquiryEndpoint(),
                AvatarQuestionTextBox.Text,
                _avatarInquirySessionId,
                Interlocked.Increment(ref _textDisplayGeneration),
                Environment.TickCount64,
                timeout.Token);
            if (presentation.Receipt is not null && !IsCurrentAvatarQuestion(presentation.Receipt))
            {
                AvatarInquiryStatusText.Text = "The DNNE endpoint changed. Present the question again.";
                return;
            }

            _avatarInquiryReceipt = presentation.Receipt;
            AvatarInquiryStatusText.Text = presentation.Status;
            Log("Avatar inquiry: " + TrimForLog(presentation.Status, 200));
        }
        catch (Exception ex)
        {
            AvatarInquiryStatusText.Text = "Question unavailable: " + TrimForLog(ex.Message, 200);
            Log($"Avatar inquiry warning: {ex.GetType().Name}: {TrimForLog(ex.Message, 200)}");
        }
        finally
        {
            SetAvatarInquiryBusy(false);
        }
    }

    private async void RequestAvatarReply_OnClick(object sender, RoutedEventArgs e)
    {
        if (_avatarInquiryInFlight || _textDisplayInFlight)
        {
            return;
        }

        var receipt = _avatarInquiryReceipt;
        AvatarReplyText.Text = string.Empty;
        SetAvatarInquiryBusy(true);
        AvatarInquiryStatusText.Text = "Requesting a DNNE-reviewed reply...";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var reply = await NRE.SimAvatar.AvatarInquiryApi.RequestReplyAsync(
                _avatarInquiryHttpClient, GetAvatarInquiryEndpoint(), AvatarQuestionTextBox.Text, receipt, timeout.Token);
            if (receipt is null || !IsCurrentAvatarQuestion(receipt))
            {
                ClearAvatarInquiry();
                AvatarInquiryStatusText.Text = "The question or DNNE endpoint changed. Present the question again.";
                return;
            }

            AvatarReplyText.Text = reply.AcceptedText;
            AvatarInquiryStatusText.Text = reply.Status;
            Log("Avatar inquiry: " + TrimForLog(reply.Status, 200));
        }
        catch (Exception ex)
        {
            AvatarInquiryStatusText.Text = "Reply unavailable: " + TrimForLog(ex.Message, 200);
            Log($"Avatar reply warning: {ex.GetType().Name}: {TrimForLog(ex.Message, 200)}");
        }
        finally
        {
            SetAvatarInquiryBusy(false);
        }
    }

    private bool IsCurrentAvatarQuestion(NRE.SimAvatar.AvatarInquiryReceipt receipt)
    {
        try
        {
            return string.Equals(GetAvatarInquiryEndpoint().AbsoluteUri, receipt.Endpoint.AbsoluteUri, StringComparison.Ordinal) &&
                string.Equals(NRE.SimAvatar.AvatarInquiryApi.NormalizeQuestion(AvatarQuestionTextBox.Text), receipt.Question, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private void SetAvatarInquiryBusy(bool busy)
    {
        _avatarInquiryInFlight = busy;
        AvatarInquiryPanel.IsEnabled = !busy;
        PresentTextButton.IsEnabled = !busy && !_textDisplayInFlight;
        RequestAvatarReplyButton.IsEnabled = !busy && _avatarInquiryReceipt is not null;
    }

    private Uri GetAvatarInquiryEndpoint() => ResolveEndpointUri() ?? throw new InvalidOperationException("Invalid DNNE endpoint.");
}
