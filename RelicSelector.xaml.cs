using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ArchiveModification;

public partial class RelicSelector : Window
{
    public string? SelectedRelicId { get; private set; }

    private readonly List<RelicItem> _allRelics = new();

    public RelicSelector(Dictionary<string, string> relicsChineseNames, Dictionary<string, string> relicsDescriptions)
    {
        InitializeComponent();

        LoadRelics(relicsChineseNames, relicsDescriptions);
    }

    private void LoadRelics(Dictionary<string, string> relicsChineseNames, Dictionary<string, string> relicsDescriptions)
    {
        // 获取程序所在目录
        var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var imagesPath = Path.Combine(exeDirectory, "relics");

        foreach (var kvp in relicsChineseNames)
        {
            var relicId = kvp.Key;
            var chineseName = kvp.Value;
            relicsDescriptions.TryGetValue(relicId, out var description);

            var imagePath = GetImagePath(imagesPath, relicId);

            _allRelics.Add(new RelicItem
            {
                Id = relicId,
                ChineseName = chineseName,
                Description = description ?? string.Empty,
                ImageSource = imagePath != null ? new BitmapImage(new Uri(imagePath)) : null
            });
        }

        _allRelics.Sort((a, b) => a.ChineseName.CompareTo(b.ChineseName));
        RelicGrid.ItemsSource = _allRelics;
    }

    private string? GetImagePath(string imagesPath, string relicId)
    {
        // 构建文件路径（小写）
        var fileName = relicId.ToLower() + ".png";
        var mainPath = Path.Combine(imagesPath, fileName);

        // 先在主目录查找
        if (File.Exists(mainPath))
            return mainPath;

        // 再在 beta 子目录查找
        var betaPath = Path.Combine(imagesPath, "beta", fileName);
        if (File.Exists(betaPath))
            return betaPath;

        return null;
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        var searchText = SearchBox.Text.Trim().ToLower();

        if (string.IsNullOrEmpty(searchText))
        {
            RelicGrid.ItemsSource = _allRelics;
        }
        else
        {
            var filtered = _allRelics.Where(r =>
                r.ChineseName.ToLower().Contains(searchText) ||
                r.Id.ToLower().Contains(searchText)).ToList();
            RelicGrid.ItemsSource = filtered;
        }
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (SearchBox.Text == "搜索遗物...")
            SearchBox.Text = string.Empty;
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SearchBox.Text))
            SearchBox.Text = "搜索遗物...";
    }

    private void RelicItem_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is RelicItem item)
        {
            SelectedRelicId = item.Id;
            DialogResult = true;
            Close();
        }
    }

    public sealed class RelicItem
    {
        public string Id { get; init; } = string.Empty;
        public string ChineseName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public BitmapImage? ImageSource { get; init; }
    }
}
