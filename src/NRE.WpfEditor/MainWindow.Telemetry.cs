namespace NRE.WpfEditor;

// Telemetry text-panel setters. Each method short-circuits if the text is unchanged
// to avoid pointless WPF rebinding/redraw work on the UI thread.
// Extracted from MainWindow.xaml.cs.
public partial class MainWindow
{
    private void SetTransportStatsText(string text)
    {
        if (string.Equals(_lastTransportStatsText, text, StringComparison.Ordinal))
        {
            return;
        }

        _lastTransportStatsText = text;
        TransportStatsTextBox.Text = text;
        TransportStatsTextBox.CaretIndex = 0;
    }

    private void SetBrainDashboardText(string text)
    {
        if (string.Equals(_lastBrainDashboardText, text, StringComparison.Ordinal))
        {
            return;
        }

        _lastBrainDashboardText = text;
        BrainDashboardTextBox.Text = text;
        BrainDashboardTextBox.CaretIndex = 0;
    }

    private void SetInhabitanceText(string text)
    {
        if (string.Equals(_lastInhabitanceText, text, StringComparison.Ordinal))
        {
            return;
        }

        _lastInhabitanceText = text;
        InhabitanceTextBox.Text = text;
        InhabitanceTextBox.CaretIndex = 0;
    }

    private void SetCircuitAuditText(string text)
    {
        if (string.Equals(_lastCircuitAuditText, text, StringComparison.Ordinal))
        {
            return;
        }

        _lastCircuitAuditText = text;
        CircuitAuditTextBox.Text = text;
        CircuitAuditTextBox.CaretIndex = 0;
    }

    private void SetReasoningText(string text)
    {
        if (string.Equals(_lastReasoningText, text, StringComparison.Ordinal))
        {
            return;
        }

        _lastReasoningText = text;
        ReasoningTextBox.Text = text;
        ReasoningTextBox.CaretIndex = 0;
    }

    private void SetLanguageCommandTelemetryText(string text)
    {
        if (LanguageCommandTelemetryTextBox is null ||
            string.Equals(_lastLanguageCommandTelemetryText, text, StringComparison.Ordinal))
        {
            return;
        }

        _lastLanguageCommandTelemetryText = text;
        LanguageCommandTelemetryTextBox.Text = text;
        LanguageCommandTelemetryTextBox.CaretIndex = 0;
    }

}
