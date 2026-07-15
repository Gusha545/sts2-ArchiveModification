using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ArchiveModification;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private JsonNode? _root;
    private string? _filePath;
    private JsonObject? _player;
    private JsonArray? _deck;
    private JsonArray? _relics;
    private JsonArray? _players;
    private int _currentPlayerIndex;
    private bool _showChineseNames;
    private bool _showRelicChineseNames;
    private bool _allCardsUpgraded;
    private Dictionary<string, string> _cardsChineseNames = new();
    private Dictionary<string, string> _relicsChineseNames = new();
    private Dictionary<string, string> _relicsDescriptions = new();
    private static readonly Dictionary<string, string> CharacterNames = new()
    {
        { "DEFECT", "鸡煲" },
        { "IRONCLAD", "战士" },
        { "REGENT", "储君" },
        { "SILENT", "猎手" },
        { "NECROBINDER", "骨妹" }
    };

    public MainWindow()
    {
        InitializeComponent();
        LoadCardsJson();
        LoadRelicsJson();
    }

    private void LoadCardsJson()
    {
        try
        {
            // 从嵌入资源加载 cards.json
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("ArchiveModification.cards.json");
            if (stream == null)
            {
                MessageBox.Show("未找到嵌入资源 cards.json", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var cardsRoot = JsonNode.Parse(json);
            if (cardsRoot is JsonObject obj)
            {
                foreach (var kvp in obj)
                {
                    if (kvp.Key.EndsWith(".title") && kvp.Value is JsonValue val)
                    {
                        var cardId = kvp.Key[..^6]; // 去掉 ".title"
                        _cardsChineseNames[cardId] = val.GetValue<string>();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载 cards.json 失败：{ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadRelicsJson()
    {
        try
        {
            // 从嵌入资源加载 relics.json
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("ArchiveModification.relics.json");
            if (stream == null)
            {
                MessageBox.Show("未找到嵌入资源 relics.json", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var relicsRoot = JsonNode.Parse(json);
            if (relicsRoot is JsonObject obj)
            {
                foreach (var kvp in obj)
                {
                    if (kvp.Key.EndsWith(".title") && kvp.Value is JsonValue val)
                    {
                        var relicId = kvp.Key[..^6]; // 去掉 ".title"
                        _relicsChineseNames[relicId] = val.GetValue<string>();
                    }
                    else if (kvp.Key.EndsWith(".description") && kvp.Value is JsonValue descVal)
                    {
                        var relicId = kvp.Key[..^12]; // 去掉 ".description"
                        _relicsDescriptions[relicId] = descVal.GetValue<string>();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载 relics.json 失败：{ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private string GetCardDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id))
            return id;

        if (!_showChineseNames)
            return id;

        // 去掉 "CARD." 前缀
        if (id.StartsWith("CARD."))
            id = id[5..];

        // 检测是否包含角色名
        string? characterKey = null;
        foreach (var key in CharacterNames.Keys)
        {
            if (id.Contains(key))
            {
                characterKey = key;
                break;
            }
        }

        // 获取卡牌标题
        var title = _cardsChineseNames.TryGetValue(id, out var t) ? t : id;

        // 如果包含角色名，添加括号注释
        if (characterKey != null)
            title = $"{title}（{CharacterNames[characterKey]}）";

        return title;
    }

    private string GetRelicDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id))
            return id;

        if (!_showRelicChineseNames)
            return id;

        // 去掉 "RELIC." 前缀
        if (id.StartsWith("RELIC."))
            id = id[6..];

        // 获取遗物标题
        var title = _relicsChineseNames.TryGetValue(id, out var t) ? t : id;

        return title;
    }

    private void ToggleDisplayMode_Click(object sender, RoutedEventArgs e)
    {
        _showChineseNames = !_showChineseNames;
        ToggleDisplayModeBtn.Content = _showChineseNames ? "切换ID显示" : "切换卡牌显示";

        // 刷新列表以更新显示
        if (_deck != null)
            RefreshDeckList();
        if (_relics != null)
            RefreshRelicsList();
    }

    private void ToggleRelicDisplayMode_Click(object sender, RoutedEventArgs e)
    {
        _showRelicChineseNames = !_showRelicChineseNames;
        ToggleRelicDisplayModeBtn.Content = _showRelicChineseNames ? "切换ID显示" : "切换遗物显示";

        if (_relics != null)
            RefreshRelicsList();
    }

    private void UpgradeAll_Click(object sender, RoutedEventArgs e)
    {
        if (_deck == null)
            return;

        _allCardsUpgraded = !_allCardsUpgraded;

        foreach (var item in _deck)
        {
            if (item is JsonObject card)
            {
                if (_allCardsUpgraded)
                {
                    // 添加或设置升级等级为1
                    card["current_upgrade_level"] = 1;
                }
                else
                {
                    // 移除升级等级
                    card.Remove("current_upgrade_level");
                }
            }
        }

        UpgradeAllBtn.Content = _allCardsUpgraded ? "取消升级" : "一键升级";
        RefreshDeckList();
    }

    private void OpenSave_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "存档文件 (*.save)|*.save|所有文件 (*.*)|*.*",
            Title = "打开存档"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            _root = JsonNode.Parse(json);
            if (_root is null)
                throw new InvalidOperationException("存档内容为空。");

            if (_root["players"] is not JsonArray players || players.Count == 0)
                throw new InvalidOperationException("存档中未找到 players 数据。");

            if (players[0] is not JsonObject player)
                throw new InvalidOperationException("players[0] 格式无效。");

            _filePath = dialog.FileName;
            _players = players;
            _currentPlayerIndex = 0;
            _player = player;
            _deck = EnsureArray(_player, "deck");
            _relics = EnsureArray(_player, "relics");

            LoadPlayerToUi();
            RefreshDeckList();
            RefreshRelicsList();

            PlayerPropertiesPanel.IsEnabled = true;
            DeckPanel.IsEnabled = true;
            RelicsPanel.IsEnabled = true;
            FilePathText.Text = _filePath;

            // 检测多人存档
            if (players.Count > 1)
            {
                SwitchPlayerBtn.Visibility = Visibility.Visible;
                PlayerInfoText.Visibility = Visibility.Visible;
                UpdatePlayerInfo();
            }
            else
            {
                SwitchPlayerBtn.Visibility = Visibility.Collapsed;
                PlayerInfoText.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开存档失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveSave_Click(object sender, RoutedEventArgs e)
    {
        if (_root is null || _player is null || string.IsNullOrEmpty(_filePath))
        {
            MessageBox.Show("请先打开存档文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryApplyPlayerFromUi(out var error))
        {
            MessageBox.Show(error, "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var json = _root.ToJsonString(JsonOptions);
            File.WriteAllText(_filePath, json);
            MessageBox.Show("存档已保存。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存存档失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SwitchPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (_players is null || _players.Count <= 1)
            return;

        // 先保存当前玩家的UI数据
        if (!TryApplyPlayerFromUi(out var error))
        {
            MessageBox.Show(error, "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 构建玩家选项
        var playerOptions = new List<string>();
        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i] is JsonObject p)
            {
                var charId = p["character_id"]?.GetValue<string>() ?? "未知";
                var charName = GetCharacterDisplayName(charId);
                playerOptions.Add($"玩家{i + 1} - {charName}");
            }
        }

        // 显示选择对话框
        var dialog = new Window
        {
            Title = "选择玩家",
            Width = 300,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox
        {
            Margin = new Thickness(10),
            ItemsSource = playerOptions,
            SelectedIndex = _currentPlayerIndex
        };

        var okButton = new Button
        {
            Content = "确定",
            Width = 80,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 10, 10),
            IsDefault = true
        };
        okButton.Click += (s, args) =>
        {
            if (listBox.SelectedIndex >= 0 && listBox.SelectedIndex != _currentPlayerIndex)
            {
                _currentPlayerIndex = listBox.SelectedIndex;
                if (_players![_currentPlayerIndex] is JsonObject newPlayer)
                {
                    _player = newPlayer;
                    _deck = EnsureArray(_player, "deck");
                    _relics = EnsureArray(_player, "relics");
                    _allCardsUpgraded = false;
                    UpgradeAllBtn.Content = "一键升级";

                    LoadPlayerToUi();
                    RefreshDeckList();
                    RefreshRelicsList();
                    UpdatePlayerInfo();
                }
            }
            dialog.DialogResult = true;
            dialog.Close();
        };

        var panel = new System.Windows.Controls.DockPanel();
        System.Windows.Controls.DockPanel.SetDock(okButton, System.Windows.Controls.Dock.Bottom);
        panel.Children.Add(okButton);
        panel.Children.Add(listBox);
        dialog.Content = panel;

        dialog.ShowDialog();
    }

    private void UpdatePlayerInfo()
    {
        if (_players is null || _player is null)
            return;

        var charId = _player["character_id"]?.GetValue<string>() ?? "未知";
        var charName = GetCharacterDisplayName(charId);
        PlayerInfoText.Text = $"当前编辑：玩家{_currentPlayerIndex + 1} - {charName}（共{_players.Count}人）";
    }

    private string GetCharacterDisplayName(string characterId)
    {
        // 去掉 "CHARACTER." 前缀
        if (characterId.StartsWith("CHARACTER."))
            characterId = characterId[10..];

        if (CharacterNames.TryGetValue(characterId, out var name))
            return name;

        return characterId;
    }

    private void LoadPlayerToUi()
    {
        if (_player is null)
            return;

        CurrentHpBox.Text = GetIntText(_player, "current_hp");
        MaxHpBox.Text = GetIntText(_player, "max_hp");
        GoldBox.Text = GetIntText(_player, "gold");
        MaxEnergyBox.Text = GetIntText(_player, "max_energy");
        MaxPotionSlotBox.Text = GetIntText(_player, "max_potion_slot_count");

        var odds = _player["odds"] as JsonObject;
        CardRarityOddsBox.Text = GetFloatText(odds, "card_rarity_odds_value");
        PotionRewardOddsBox.Text = GetFloatText(odds, "potion_reward_odds_value");
    }

    private bool TryApplyPlayerFromUi(out string error)
    {
        error = string.Empty;
        if (_player is null)
        {
            error = "未加载玩家数据。";
            return false;
        }

        if (!TryParsePositiveInt(CurrentHpBox.Text, "当前生命", out var currentHp, out error))
            return false;
        if (!TryParsePositiveInt(MaxHpBox.Text, "最大生命", out var maxHp, out error))
            return false;
        if (!TryParsePositiveInt(GoldBox.Text, "金币", out var gold, out error))
            return false;
        if (!TryParsePositiveInt(MaxEnergyBox.Text, "每回合费用", out var maxEnergy, out error))
            return false;
        if (!TryParseRangeInt(MaxPotionSlotBox.Text, "最大药水栏位", 1, 9, out var potionSlots, out error))
            return false;
        if (!TryParseRangeDouble(CardRarityOddsBox.Text, "稀有卡牌出现概率加成", -1, 1, out var cardOdds, out error))
            return false;
        if (!TryParseRangeDouble(PotionRewardOddsBox.Text, "药水奖励概率加成", -1, 1, out var potionOdds, out error))
            return false;

        _player["current_hp"] = currentHp;
        _player["max_hp"] = maxHp;
        _player["gold"] = gold;
        _player["max_energy"] = maxEnergy;
        _player["max_potion_slot_count"] = potionSlots;

        var odds = _player["odds"] as JsonObject ?? new JsonObject();
        odds["card_rarity_odds_value"] = cardOdds;
        odds["potion_reward_odds_value"] = potionOdds;
        _player["odds"] = odds;

        return true;
    }

    private void RefreshDeckList()
    {
        DeckList.ItemsSource = BuildListItems(_deck, isRelic: false);
    }

    private void RefreshRelicsList()
    {
        RelicsList.ItemsSource = BuildListItems(_relics, isRelic: true);
    }

    private List<JsonListItem> BuildListItems(JsonArray? array, bool isRelic = false)
    {
        var items = new List<JsonListItem>();
        if (array is null)
            return items;

        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is JsonObject obj)
                items.Add(new JsonListItem(i, obj, this, isRelic));
        }

        return items;
    }

    private void AddDeckItem_Click(object sender, RoutedEventArgs e)
    {
        if (_deck is null)
            return;

        if (!TryAddJsonItem("添加卡牌", out var node))
            return;

        _deck.Add(node);
        RefreshDeckList();
    }

    private void DeleteDeckItem_Click(object sender, RoutedEventArgs e)
    {
        if (_deck is null)
            return;

        if (DeckList.SelectedItem is not JsonListItem item)
        {
            MessageBox.Show("请先选择要删除的卡牌。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _deck.RemoveAt(item.Index);
        RefreshDeckList();
    }

    private void DeckList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_deck is null)
            return;

        if (DeckList.SelectedItem is not JsonListItem item)
            return;

        EditJsonItem("编辑卡牌", _deck, item);
    }

    private void AddRelicItem_Click(object sender, RoutedEventArgs e)
    {
        if (_relics is null)
            return;

        if (!TryAddJsonItem("添加遗物", out var node))
            return;

        _relics.Add(node);
        RefreshRelicsList();
    }

    private void AddRelicByImage_Click(object sender, RoutedEventArgs e)
    {
        if (_relics is null)
        {
            MessageBox.Show("请先打开存档文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selector = new RelicSelector(_relicsChineseNames, _relicsDescriptions)
        {
            Owner = this
        };

        if (selector.ShowDialog() != true || string.IsNullOrEmpty(selector.SelectedRelicId))
            return;

        var relicId = selector.SelectedRelicId;
        var newRelic = new JsonObject
        {
            ["floor_added_to_deck"] = 1,
            ["id"] = $"RELIC.{relicId}"
        };

        _relics.Add(newRelic);
        RefreshRelicsList();

        var chineseName = _relicsChineseNames.TryGetValue(relicId, out var name) ? name : relicId;
        MessageBox.Show($"已添加遗物：{chineseName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteRelicItem_Click(object sender, RoutedEventArgs e)
    {
        if (_relics is null)
            return;

        if (RelicsList.SelectedItem is not JsonListItem item)
        {
            MessageBox.Show("请先选择要删除的遗物。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _relics.RemoveAt(item.Index);
        RefreshRelicsList();
    }

    private void RelicsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_relics is null)
            return;

        if (RelicsList.SelectedItem is not JsonListItem item)
            return;

        EditJsonItem("编辑遗物", _relics, item);
    }

    private bool TryAddJsonItem(string title, out JsonObject node)
    {
        node = null!;
        var dialog = new JsonInputDialog(title, "{\r\n  \"floor_added_to_deck\": 1,\r\n  \"id\": \"\"\r\n}")
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ResultJson))
            return false;

        try
        {
            var parsed = JsonNode.Parse(dialog.ResultJson);
            if (parsed is not JsonObject obj)
                throw new InvalidOperationException("输入必须是 JSON 对象（以 { 开头）。");

            node = obj;
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"JSON 解析失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void EditJsonItem(string title, JsonArray array, JsonListItem item)
    {
        var isDeck = array == _deck;
        var currentJson = item.Node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        var dialog = new JsonInputDialog(title, currentJson, showUpgradeButton: isDeck)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ResultJson))
            return;

        try
        {
            var parsed = JsonNode.Parse(dialog.ResultJson);
            if (parsed is not JsonObject obj)
                throw new InvalidOperationException("输入必须是 JSON 对象（以 { 开头）。");

            array[item.Index] = obj;
            
            if (array == _deck)
                RefreshDeckList();
            else if (array == _relics)
                RefreshRelicsList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"JSON 解析失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static JsonArray EnsureArray(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonArray array)
            return array;

        array = new JsonArray();
        parent[propertyName] = array;
        return array;
    }

    private static string GetIntText(JsonObject obj, string key)
    {
        return obj[key]?.GetValue<int>().ToString() ?? string.Empty;
    }

    private static string GetFloatText(JsonObject? obj, string key)
    {
        if (obj?[key] is null)
            return string.Empty;

        return obj[key]!.GetValue<double>().ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryParsePositiveInt(string text, string fieldName, out int value, out string error)
    {
        value = 0;
        error = string.Empty;

        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value <= 0)
        {
            error = $"{fieldName} 必须是大于 0 的整数。";
            return false;
        }

        return true;
    }

    private static bool TryParseRangeInt(string text, string fieldName, int min, int max, out int value, out string error)
    {
        value = 0;
        error = string.Empty;

        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < min || value > max)
        {
            error = $"{fieldName} 必须是 {min}~{max} 之间的整数。";
            return false;
        }

        return true;
    }

    private static bool TryParseRangeDouble(string text, string fieldName, double min, double max, out double value, out string error)
    {
        value = 0;
        error = string.Empty;

        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < min || value > max)
        {
            error = $"{fieldName} 必须是 {min}~{max} 之间的数值。";
            return false;
        }

        return true;
    }

    private sealed class JsonListItem
    {
        private readonly MainWindow _owner;
        private readonly bool _isRelic;

        public JsonListItem(int index, JsonObject node, MainWindow owner, bool isRelic = false)
        {
            Index = index;
            Node = node;
            _owner = owner;
            _isRelic = isRelic;
        }

        public int Index { get; }
        public JsonObject Node { get; }

        public string DisplayText
        {
            get
            {
                var id = Node["id"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(id))
                {
                    var upgrade = Node["current_upgrade_level"]?.GetValue<int>();
                    var displayId = _isRelic ? _owner.GetRelicDisplayName(id) : _owner.GetCardDisplayName(id);
                    return upgrade is not null ? $"{displayId} (+{upgrade})" : displayId;
                }

                return Node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            }
        }
    }
}
