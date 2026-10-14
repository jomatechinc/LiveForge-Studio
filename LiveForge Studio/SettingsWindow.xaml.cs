using System.Windows;

namespace LiveForge_Studio;

public partial class SettingsWindow : Window
{
    private static readonly string[] CanvasFormats =
    [
        "1280 × 720 · 30 fps",
        "1920 × 1080 · 30 fps",
        "1920 × 1080 · 60 fps",
        "3840 × 2160 · 30 fps"
    ];

    public SettingsWindow(string productName, string canvasFormat)
    {
        InitializeComponent();
        ProductNameInput.Text = productName;
        CanvasFormatInput.ItemsSource = CanvasFormats;
        CanvasFormatInput.SelectedItem = CanvasFormats.Contains(canvasFormat) ? canvasFormat : CanvasFormats[1];
    }

    public string ProductNameValue => ProductNameInput.Text.Trim();
    public string CanvasFormatValue => CanvasFormatInput.SelectedItem as string ?? CanvasFormats[1];

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProductNameValue))
        {
            MessageBox.Show(this, "Enter a product name before saving.", "Workspace Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            ProductNameInput.Focus();
            return;
        }

        DialogResult = true;
    }
}
