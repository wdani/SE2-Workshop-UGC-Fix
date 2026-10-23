using System;
using System.Windows;
using System.Windows.Media;
using SE2WorkshopUGCFix;

namespace SE2WorkshopUGCFix.Wpf;

public partial class PreflightWindow : Window
{
    private readonly string? _template;
    private readonly ulong? _itemId;

    public PreflightWindow(string? template, ulong? itemId)
    {
        _template = template;
        _itemId = itemId;
        InitializeComponent();
    }

    private void Estimate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = DescriptionPreflight.Calculate(DescriptionInput.Text, _template);
            EstimateResult.Text =
                $"Description characters: {result.DescriptionChars:N0}\n" +
                $"Description UTF-8 bytes: {result.DescriptionUtf8Bytes:N0}\n" +
                $"JSON encoding: {result.JsonMinBytes:N0} – {result.JsonConservativeBytes:N0} bytes\n" +
                $"Base developer metadata: {result.BaseBytesMin:N0} bytes\n" +
                $"Estimated total IF embedded: {result.ProjectedMin:N0} – {result.ProjectedConservative:N0} bytes\n" +
                $"Steam developer metadata limit: {MetadataPatch.LimitBytes:N0} bytes\n\n" +
                result.RiskLabel + "\n\n" + result.TemplateNote;
            bool danger = result.ProjectedConservative >= MetadataPatch.LimitBytes;
            EstimateStatus.Text = (danger ? "Possible oversized metadata. " : "Estimate only. ") +
                (_itemId is null ? "No live template selected. " : $"Template from last scan: {_itemId}. ") +
                "No Steam write was performed.";
            EstimateStatus.Foreground = (Brush)FindResource(danger ? "DangerBrush" : "SuccessBrush");
            CopyButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            EstimateResult.Text = "Could not produce a reliable estimate: " + ex.Message;
            CopyButton.IsEnabled = false;
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (CopyButton.IsEnabled) Clipboard.SetText(EstimateResult.Text);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        DescriptionInput.Clear();
        EstimateResult.Text = "Your offline estimate will appear here.";
        EstimateStatus.Text = "This tool does not write to Steam.";
        CopyButton.IsEnabled = false;
    }
}
