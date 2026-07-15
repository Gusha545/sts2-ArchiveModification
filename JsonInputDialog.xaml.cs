using System.Text.Json.Nodes;
using System.Windows;

namespace ArchiveModification;

public partial class JsonInputDialog : Window
{
    public string? ResultJson { get; private set; }

    public JsonInputDialog(string prompt, string? defaultJson = null, bool showUpgradeButton = false)
    {
        InitializeComponent();
        Title = prompt;
        if (!string.IsNullOrEmpty(defaultJson))
            JsonTextBox.Text = defaultJson;
        if (showUpgradeButton)
            UpgradeButton.Visibility = Visibility.Visible;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ResultJson = JsonTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void Upgrade_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var parsed = JsonNode.Parse(JsonTextBox.Text);
            if (parsed is not JsonObject obj)
            {
                MessageBox.Show("JSON 必须是对象格式。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            obj["current_upgrade_level"] = 1;
            JsonTextBox.Text = obj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"JSON 解析失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
