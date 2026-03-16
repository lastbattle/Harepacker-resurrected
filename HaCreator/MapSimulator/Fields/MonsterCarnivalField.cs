using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.Fields
{
    public enum MonsterCarnivalTab
    {
        Mob = 0,
        Skill = 1,
        Guardian = 2
    }

    public enum MonsterCarnivalTeam
    {
        Team0 = 0,
        Team1 = 1
    }

    public sealed class MonsterCarnivalEntry
    {
        public MonsterCarnivalEntry(MonsterCarnivalTab tab, int index, int id, int cost, string name, string description)
        {
            Tab = tab;
            Index = index;
            Id = id;
            Cost = cost;
            Name = string.IsNullOrWhiteSpace(name) ? $"{tab} {id}" : name.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        }

        public MonsterCarnivalTab Tab { get; }
        public int Index { get; }
        public int Id { get; }
        public int Cost { get; }
        public string Name { get; }
        public string Description { get; }
    }

    public sealed class MonsterCarnivalTeamState
    {
        public int CurrentCp { get; set; }
        public int TotalCp { get; set; }
    }

    public sealed class MonsterCarnivalFieldDefinition
    {
        public int MapId { get; init; }
        public int DefaultTimeSeconds { get; init; }
        public int ExpandTimeSeconds { get; init; }
        public int MessageTimeSeconds { get; init; }
        public int FinishTimeSeconds { get; init; }
        public int DeathCp { get; init; }
        public int RewardMapWin { get; init; }
        public int RewardMapLose { get; init; }
        public int MobGenMax { get; init; }
        public int GuardianGenMax { get; init; }
        public string EffectWin { get; init; }
        public string EffectLose { get; init; }
        public string SoundWin { get; init; }
        public string SoundLose { get; init; }
        public IReadOnlyList<MonsterCarnivalEntry> MobEntries { get; init; } = Array.Empty<MonsterCarnivalEntry>();
        public IReadOnlyList<MonsterCarnivalEntry> SkillEntries { get; init; } = Array.Empty<MonsterCarnivalEntry>();
        public IReadOnlyList<MonsterCarnivalEntry> GuardianEntries { get; init; } = Array.Empty<MonsterCarnivalEntry>();

        public IReadOnlyList<MonsterCarnivalEntry> GetEntries(MonsterCarnivalTab tab)
        {
            return tab switch
            {
                MonsterCarnivalTab.Mob => MobEntries,
                MonsterCarnivalTab.Skill => SkillEntries,
                MonsterCarnivalTab.Guardian => GuardianEntries,
                _ => Array.Empty<MonsterCarnivalEntry>()
            };
        }
    }

    public static class MonsterCarnivalFieldDataLoader
    {
        private const string MonsterCarnivalPropertyName = "monsterCarnival";
        private const string MobStringImageName = "Mob.img";
        private const string McSkillImageName = "MCSkill.img";
        private const string McGuardianImageName = "MCGuardian.img";

        public static bool IsMonsterCarnivalMap(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return false;
            }

            if (mapInfo.fieldType == FieldType.FIELDTYPE_MONSTERCARNIVAL_S2
                || mapInfo.fieldType == FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE
                || mapInfo.fieldType == FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM
                || mapInfo.fieldType == FieldType.FIELDTYPE_MONSTERCARNIVAL_NOT_USE)
            {
                return true;
            }

            return FindMonsterCarnivalProperty(mapInfo) != null;
        }

        public static MonsterCarnivalFieldDefinition Load(MapInfo mapInfo)
        {
            if (!IsMonsterCarnivalMap(mapInfo))
            {
                return null;
            }

            WzImageProperty property = FindMonsterCarnivalProperty(mapInfo);
            if (property == null)
            {
                return new MonsterCarnivalFieldDefinition
                {
                    MapId = mapInfo?.id ?? 0
                };
            }

            return new MonsterCarnivalFieldDefinition
            {
                MapId = mapInfo?.id ?? 0,
                DefaultTimeSeconds = ReadInt(property["timeDefault"]),
                ExpandTimeSeconds = ReadInt(property["timeExpand"]),
                MessageTimeSeconds = ReadInt(property["timeMessage"]),
                FinishTimeSeconds = ReadInt(property["timeFinish"]),
                DeathCp = ReadInt(property["deathCP"]),
                RewardMapWin = ReadInt(property["rewardMapWin"]),
                RewardMapLose = ReadInt(property["rewardMapLose"]),
                MobGenMax = ReadInt(property["mobGenMax"]),
                GuardianGenMax = ReadInt(property["guardianGenMax"]),
                EffectWin = ReadString(property["effectWin"]),
                EffectLose = ReadString(property["effectLose"]),
                SoundWin = ReadString(property["soundWin"]),
                SoundLose = ReadString(property["soundLose"]),
                MobEntries = LoadMobEntries(property["mob"]),
                SkillEntries = LoadNamedEntries(property["skill"], MonsterCarnivalTab.Skill, McSkillImageName),
                GuardianEntries = LoadNamedEntries(property["guardian"], MonsterCarnivalTab.Guardian, McGuardianImageName)
            };
        }

        private static WzImageProperty FindMonsterCarnivalProperty(MapInfo mapInfo)
        {
            if (mapInfo?.additionalNonInfoProps == null)
            {
                return null;
            }

            for (int i = 0; i < mapInfo.additionalNonInfoProps.Count; i++)
            {
                WzImageProperty property = mapInfo.additionalNonInfoProps[i];
                if (string.Equals(property?.Name, MonsterCarnivalPropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }
            }

            return null;
        }

        private static IReadOnlyList<MonsterCarnivalEntry> LoadMobEntries(WzImageProperty property)
        {
            var entries = new List<MonsterCarnivalEntry>();
            if (property?.WzProperties == null)
            {
                return entries;
            }

            foreach (WzImageProperty child in EnumerateIndexedChildren(property))
            {
                int mobId = ReadInt(child["id"]);
                int cost = ReadInt(child["spendCP"]);
                string name = ResolveMobName(mobId) ?? $"Mob {mobId}";
                string description = $"Summon {name}.";
                entries.Add(new MonsterCarnivalEntry(
                    MonsterCarnivalTab.Mob,
                    ParseChildIndex(child.Name, entries.Count),
                    mobId,
                    cost,
                    name,
                    description));
            }

            return entries;
        }

        private static IReadOnlyList<MonsterCarnivalEntry> LoadNamedEntries(WzImageProperty property, MonsterCarnivalTab tab, string imageName)
        {
            var entries = new List<MonsterCarnivalEntry>();
            if (property?.WzProperties == null)
            {
                return entries;
            }

            WzImage definitionImage = FindImageSafe("Skill", imageName);
            foreach (WzImageProperty child in EnumerateIndexedChildren(property))
            {
                int entryId = ReadInt(child);
                WzImageProperty definition = definitionImage?[entryId.ToString(CultureInfo.InvariantCulture)];
                string name = ReadString(definition?["name"]) ?? $"{tab} {entryId}";
                string description = ReadString(definition?["desc"]);
                int cost = ReadInt(definition?["spendCP"]);
                entries.Add(new MonsterCarnivalEntry(
                    tab,
                    ParseChildIndex(child.Name, entries.Count),
                    entryId,
                    cost,
                    name,
                    description));
            }

            return entries;
        }

        private static IEnumerable<WzImageProperty> EnumerateIndexedChildren(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                yield break;
            }

            foreach (WzImageProperty child in property.WzProperties.Cast<WzImageProperty>().OrderBy(candidate => ParseChildIndex(candidate.Name, int.MaxValue)))
            {
                yield return child;
            }
        }

        private static int ParseChildIndex(string name, int fallback)
        {
            return int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;
        }

        private static string ResolveMobName(int mobId)
        {
            if (mobId <= 0)
            {
                return null;
            }

            try
            {
                WzImage stringImage = FindImageSafe("String", MobStringImageName);
                return ReadString(stringImage?[mobId.ToString(CultureInfo.InvariantCulture)]?["name"]);
            }
            catch
            {
                return null;
            }
        }

        private static WzImage FindImageSafe(string category, string imageName)
        {
            try
            {
                return global::HaCreator.Program.FindImage(category, imageName);
            }
            catch
            {
                return null;
            }
        }

        private static int ReadInt(WzImageProperty property, int defaultValue = 0)
        {
            if (property == null)
            {
                return defaultValue;
            }

            try
            {
                return InfoTool.GetInt(property);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static string ReadString(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            try
            {
                return InfoTool.GetString(property);
            }
            catch
            {
                return property.GetString();
            }
        }
    }

    /// <summary>
    /// Partial Monster Carnival runtime.
    /// Mirrors the client's dedicated field owner enough to surface the
    /// Carnival-only HUD, CP counters, item lists, and request/result flow.
    /// </summary>
    public sealed class MonsterCarnivalField
    {
        private const int StatusDurationMs = 4500;

        private readonly Dictionary<int, int> _mobSpellCounts = new();
        private readonly MonsterCarnivalTeamState _team0 = new();
        private readonly MonsterCarnivalTeamState _team1 = new();
        private MonsterCarnivalFieldDefinition _definition;
        private MonsterCarnivalTeam _localTeam;
        private MonsterCarnivalTab _activeTab;
        private string _statusMessage;
        private int _statusMessageUntil;
        private int _selectedEntryIndex;
        private int _personalCp;
        private int _personalTotalCp;
        private int _lastRequestTab;
        private int _lastRequestIndex;
        private bool _isVisible;
        private bool _enteredField;

        public bool IsVisible => _isVisible;
        public bool IsEntered => _enteredField;
        public MonsterCarnivalFieldDefinition Definition => _definition;
        public MonsterCarnivalTeam LocalTeam => _localTeam;
        public MonsterCarnivalTab ActiveTab => _activeTab;
        public int PersonalCp => _personalCp;
        public int PersonalTotalCp => _personalTotalCp;
        public MonsterCarnivalTeamState Team0 => _team0;
        public MonsterCarnivalTeamState Team1 => _team1;
        public IReadOnlyDictionary<int, int> MobSpellCounts => _mobSpellCounts;

        public void Configure(MapInfo mapInfo)
        {
            Reset();
            _definition = MonsterCarnivalFieldDataLoader.Load(mapInfo);
            _isVisible = _definition != null;
            _activeTab = MonsterCarnivalTab.Mob;
        }

        public void OnEnter(
            MonsterCarnivalTeam localTeam,
            int personalCp,
            int personalTotalCp,
            int myTeamCp,
            int myTeamTotalCp,
            int enemyTeamCp,
            int enemyTeamTotalCp)
        {
            if (!_isVisible)
            {
                return;
            }

            _enteredField = true;
            _localTeam = localTeam;
            _personalCp = Math.Max(0, personalCp);
            _personalTotalCp = Math.Max(_personalCp, personalTotalCp);

            MonsterCarnivalTeamState myTeam = GetTeamState(localTeam);
            MonsterCarnivalTeamState enemyTeam = GetTeamState(GetOpposingTeam(localTeam));
            myTeam.CurrentCp = Math.Max(0, myTeamCp);
            myTeam.TotalCp = Math.Max(myTeam.CurrentCp, myTeamTotalCp);
            enemyTeam.CurrentCp = Math.Max(0, enemyTeamCp);
            enemyTeam.TotalCp = Math.Max(enemyTeam.CurrentCp, enemyTeamTotalCp);

            ShowStatus($"Entered Monster Carnival as {FormatTeam(localTeam)}.", Environment.TickCount);
        }

        public void UpdateTeamCp(
            int personalCp,
            int personalTotalCp,
            int team0CurrentCp,
            int team0TotalCp,
            int team1CurrentCp,
            int team1TotalCp)
        {
            if (!_isVisible)
            {
                return;
            }

            _personalCp = Math.Max(0, personalCp);
            _personalTotalCp = Math.Max(_personalCp, personalTotalCp);
            _team0.CurrentCp = Math.Max(0, team0CurrentCp);
            _team0.TotalCp = Math.Max(_team0.CurrentCp, team0TotalCp);
            _team1.CurrentCp = Math.Max(0, team1CurrentCp);
            _team1.TotalCp = Math.Max(_team1.CurrentCp, team1TotalCp);
        }

        public bool TrySetActiveTab(string tabText, out string message)
        {
            if (!TryParseTab(tabText, out MonsterCarnivalTab tab))
            {
                message = $"Unknown Monster Carnival tab: {tabText}";
                return false;
            }

            _activeTab = tab;
            _selectedEntryIndex = 0;
            message = DescribeStatus();
            return true;
        }

        public bool TrySetMobSpellCount(int index, int count, out string message)
        {
            MonsterCarnivalEntry entry = GetEntry(MonsterCarnivalTab.Mob, index);
            if (entry == null)
            {
                message = $"Monster Carnival mob index {index} is out of range.";
                return false;
            }

            _mobSpellCounts[entry.Id] = Math.Max(0, count);
            message = $"{entry.Name} spell count set to {_mobSpellCounts[entry.Id]}.";
            return true;
        }

        public bool TryRequestActiveEntry(int index, string requestMessage, int tickCount, out string message)
        {
            MonsterCarnivalEntry entry = GetEntry(_activeTab, index);
            if (entry == null)
            {
                message = $"Monster Carnival {_activeTab.ToString().ToLowerInvariant()} index {index} is out of range.";
                return false;
            }

            _selectedEntryIndex = entry.Index;
            _lastRequestTab = (int)_activeTab;
            _lastRequestIndex = entry.Index;

            if (_activeTab == MonsterCarnivalTab.Mob)
            {
                _mobSpellCounts[entry.Id] = _mobSpellCounts.TryGetValue(entry.Id, out int currentCount)
                    ? currentCount + 1
                    : 1;
            }

            string suffix = string.IsNullOrWhiteSpace(requestMessage)
                ? string.Empty
                : $" {requestMessage.Trim()}";
            ShowStatus($"Requested {entry.Name}.{suffix}".TrimEnd(), tickCount);
            message = DescribeStatus();
            return true;
        }

        public void OnRequestFailure(int reasonCode, int tickCount)
        {
            ShowStatus($"Monster Carnival request rejected (reason {reasonCode}).", tickCount);
        }

        public void OnShowGameResult(int resultCode, int tickCount)
        {
            string rewardText = _definition == null
                ? string.Empty
                : $" Win map {_definition.RewardMapWin}, lose map {_definition.RewardMapLose}.";
            ShowStatus($"Monster Carnival result packet {resultCode} received.{rewardText}".Trim(), tickCount);
        }

        public void Update(int tickCount)
        {
            if (_statusMessage != null && tickCount >= _statusMessageUntil)
            {
                _statusMessage = null;
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isVisible || spriteBatch == null || pixelTexture == null || font == null)
            {
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            int panelWidth = 368;
            int panelX = viewport.Width - panelWidth - 18;
            int panelY = 18;
            int panelHeight = 380;

            spriteBatch.Draw(pixelTexture, new Rectangle(panelX, panelY, panelWidth, panelHeight), new Color(16, 22, 28, 225));
            spriteBatch.Draw(pixelTexture, new Rectangle(panelX, panelY, panelWidth, 34), new Color(54, 76, 98, 255));
            DrawShadowedText(spriteBatch, font, "Monster Carnival", new Vector2(panelX + 12, panelY + 8), Color.White);

            int timerY = panelY + 44;
            string headerText = _definition == null
                ? "No WZ carnival definition loaded."
                : $"Time {_definition.DefaultTimeSeconds}s +{_definition.ExpandTimeSeconds}s | Death CP {_definition.DeathCp}";
            DrawShadowedText(spriteBatch, font, headerText, new Vector2(panelX + 12, timerY), Color.Gainsboro, 0.85f);

            DrawCpRow(spriteBatch, pixelTexture, font, panelX + 12, panelY + 76, panelWidth - 24);
            DrawTabs(spriteBatch, pixelTexture, font, panelX + 12, panelY + 130, panelWidth - 24);
            DrawEntryList(spriteBatch, pixelTexture, font, panelX + 12, panelY + 164, panelWidth - 24, 154);
            DrawFooter(spriteBatch, pixelTexture, font, panelX + 12, panelY + 324, panelWidth - 24, 44);
        }

        public string DescribeStatus()
        {
            if (!_isVisible)
            {
                return "Monster Carnival runtime is inactive on this map.";
            }

            return $"Monster Carnival: {( _enteredField ? "entered" : "configured")} | tab={_activeTab} | personalCP={_personalCp}/{_personalTotalCp} | team0={_team0.CurrentCp}/{_team0.TotalCp} | team1={_team1.CurrentCp}/{_team1.TotalCp}";
        }

        public void Reset()
        {
            _definition = null;
            _mobSpellCounts.Clear();
            _team0.CurrentCp = 0;
            _team0.TotalCp = 0;
            _team1.CurrentCp = 0;
            _team1.TotalCp = 0;
            _localTeam = MonsterCarnivalTeam.Team0;
            _activeTab = MonsterCarnivalTab.Mob;
            _statusMessage = null;
            _statusMessageUntil = 0;
            _selectedEntryIndex = 0;
            _personalCp = 0;
            _personalTotalCp = 0;
            _lastRequestTab = -1;
            _lastRequestIndex = -1;
            _isVisible = false;
            _enteredField = false;
        }

        private void DrawCpRow(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width)
        {
            int rowHeight = 44;
            spriteBatch.Draw(pixelTexture, new Rectangle(x, y, width, rowHeight), new Color(26, 34, 44, 220));
            string personalText = $"Personal CP  {_personalCp}/{_personalTotalCp}";
            DrawShadowedText(spriteBatch, font, personalText, new Vector2(x + 10, y + 6), Color.White);

            string team0Text = $"Team 0  {_team0.CurrentCp}/{_team0.TotalCp}";
            string team1Text = $"Team 1  {_team1.CurrentCp}/{_team1.TotalCp}";
            DrawShadowedText(spriteBatch, font, team0Text, new Vector2(x + 10, y + 24), new Color(128, 191, 255));
            DrawShadowedText(spriteBatch, font, team1Text, new Vector2(x + width / 2 + 4, y + 24), new Color(255, 153, 153));
        }

        private void DrawTabs(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width)
        {
            int tabWidth = width / 3;
            DrawTab(spriteBatch, pixelTexture, font, x, y, tabWidth, "Mob", MonsterCarnivalTab.Mob);
            DrawTab(spriteBatch, pixelTexture, font, x + tabWidth, y, tabWidth, "Skill", MonsterCarnivalTab.Skill);
            DrawTab(spriteBatch, pixelTexture, font, x + tabWidth * 2, y, width - tabWidth * 2, "Guardian", MonsterCarnivalTab.Guardian);
        }

        private void DrawTab(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width, string label, MonsterCarnivalTab tab)
        {
            Color background = _activeTab == tab
                ? new Color(71, 103, 140, 255)
                : new Color(31, 42, 56, 255);
            spriteBatch.Draw(pixelTexture, new Rectangle(x, y, width - 1, 26), background);
            Vector2 size = font.MeasureString(label);
            Vector2 position = new Vector2(x + (width - size.X) * 0.5f, y + 4);
            DrawShadowedText(spriteBatch, font, label, position, Color.White);
        }

        private void DrawEntryList(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width, int height)
        {
            spriteBatch.Draw(pixelTexture, new Rectangle(x, y, width, height), new Color(20, 27, 34, 200));
            IReadOnlyList<MonsterCarnivalEntry> entries = _definition?.GetEntries(_activeTab) ?? Array.Empty<MonsterCarnivalEntry>();
            if (entries.Count == 0)
            {
                DrawShadowedText(spriteBatch, font, "No entries loaded for this tab.", new Vector2(x + 10, y + 10), Color.Silver);
                return;
            }

            int rowHeight = 15;
            int visibleRows = Math.Min(entries.Count, height / rowHeight);
            for (int i = 0; i < visibleRows; i++)
            {
                MonsterCarnivalEntry entry = entries[i];
                Rectangle rowRect = new Rectangle(x + 4, y + 4 + i * rowHeight, width - 8, rowHeight - 1);
                bool isSelected = entry.Index == _selectedEntryIndex && _activeTab == (MonsterCarnivalTab)_lastRequestTab;
                Color rowColor = isSelected
                    ? new Color(67, 92, 120, 220)
                    : new Color(31, 39, 49, 180);
                spriteBatch.Draw(pixelTexture, rowRect, rowColor);

                string countText = _activeTab == MonsterCarnivalTab.Mob && _mobSpellCounts.TryGetValue(entry.Id, out int count) && count > 0
                    ? $"x{count}"
                    : string.Empty;
                string line = $"{entry.Index + 1}. {entry.Name}";
                DrawShadowedText(spriteBatch, font, line, new Vector2(rowRect.X + 6, rowRect.Y), Color.White, 0.82f);

                string costText = $"CP {entry.Cost}";
                Vector2 costSize = font.MeasureString(costText) * 0.82f;
                float countOffset = 0f;
                if (!string.IsNullOrWhiteSpace(countText))
                {
                    Vector2 countSize = font.MeasureString(countText) * 0.82f;
                    DrawShadowedText(spriteBatch, font, countText, new Vector2(rowRect.Right - countSize.X - 6, rowRect.Y), Color.Gold, 0.82f);
                    countOffset = countSize.X + 12f;
                }

                DrawShadowedText(spriteBatch, font, costText, new Vector2(rowRect.Right - costSize.X - 6 - countOffset, rowRect.Y), Color.LightGray, 0.82f);
            }
        }

        private void DrawFooter(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width, int height)
        {
            spriteBatch.Draw(pixelTexture, new Rectangle(x, y, width, height), new Color(26, 34, 44, 220));
            string status = _statusMessage;
            if (string.IsNullOrWhiteSpace(status))
            {
                MonsterCarnivalEntry selected = GetEntry(_activeTab, _selectedEntryIndex);
                status = selected?.Description;
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                status = _enteredField
                    ? "Use /mcarnival request <index> to simulate client RequestResult updates."
                    : "Use /mcarnival enter ... to populate the Carnival HUD like CField_MonsterCarnival::OnEnter.";
            }

            DrawShadowedText(spriteBatch, font, status, new Vector2(x + 8, y + 8), Color.Gainsboro, 0.8f);
        }

        private MonsterCarnivalEntry GetEntry(MonsterCarnivalTab tab, int index)
        {
            IReadOnlyList<MonsterCarnivalEntry> entries = _definition?.GetEntries(tab);
            if (entries == null || index < 0 || index >= entries.Count)
            {
                return null;
            }

            return entries[index];
        }

        private MonsterCarnivalTeamState GetTeamState(MonsterCarnivalTeam team)
        {
            return team == MonsterCarnivalTeam.Team0 ? _team0 : _team1;
        }

        private static MonsterCarnivalTeam GetOpposingTeam(MonsterCarnivalTeam team)
        {
            return team == MonsterCarnivalTeam.Team0 ? MonsterCarnivalTeam.Team1 : MonsterCarnivalTeam.Team0;
        }

        private static bool TryParseTab(string text, out MonsterCarnivalTab tab)
        {
            tab = MonsterCarnivalTab.Mob;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            switch (text.Trim().ToLowerInvariant())
            {
                case "mob":
                case "mobs":
                    tab = MonsterCarnivalTab.Mob;
                    return true;
                case "skill":
                case "skills":
                    tab = MonsterCarnivalTab.Skill;
                    return true;
                case "guardian":
                case "guardians":
                    tab = MonsterCarnivalTab.Guardian;
                    return true;
                default:
                    return false;
            }
        }

        private void ShowStatus(string message, int tickCount)
        {
            _statusMessage = message;
            _statusMessageUntil = tickCount + StatusDurationMs;
        }

        private static string FormatTeam(MonsterCarnivalTeam team)
        {
            return team == MonsterCarnivalTeam.Team0 ? "Team 0" : "Team 1";
        }

        private static void DrawShadowedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color, float scale = 1f)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            spriteBatch.DrawString(font, text, position + Vector2.One, Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
