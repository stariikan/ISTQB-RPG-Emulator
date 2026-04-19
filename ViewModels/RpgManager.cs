using CommunityToolkit.Mvvm.ComponentModel;
using ISTQBEmulator.Data;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ISTQBEmulator.ViewModels
{
    public partial class RpgManager : ObservableObject
    {
        [ObservableProperty] private string _bossName = "Searching for monsters...";
        [ObservableProperty] private string _bossIcon = "👹"; // 🔥 NEW: Observable Icon
        [ObservableProperty] private int _bossMaxHealth = 100;
        [ObservableProperty] private int _bossCurrentHealth = 100;
        [ObservableProperty] private int _comboStreak = 0;
        [ObservableProperty] private string _combatMessage = "Ready for battle!";
        [ObservableProperty] private string _combatMessageColor = "Gray";

        private int _bossesDefeated = 0;
        private Random _rnd = new Random();
        private DispatcherTimer _idleTimer;

        // 🔥 NEW: Store objects instead of just strings
        private List<MonsterDto> _monsters = new();

        private List<string> _victoryMessages = new();
        private BattleData _battlePhrases = new(); // Initialize to avoid nulls

        public RpgManager()
        {
            LoadAllJsonData();
            SpawnNextBoss();
            SetupIdleTimer();
        }

        private void LoadAllJsonData()
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // 1. Load Monsters from Memory (NEW OBJECT PARSER)
                string monsterJson = ResourceHelper.ReadEmbeddedJson("Monster_Names.json");
                if (!string.IsNullOrEmpty(monsterJson))
                {
                    var mRoot = JsonSerializer.Deserialize<MonsterRoot>(monsterJson, options);
                    if (mRoot?.Monsters != null)
                    {
                        _monsters = mRoot.Monsters;
                    }
                }

                // 2. Load Victory Messages from Memory (Uses old string parser)
                string victoryJson = ResourceHelper.ReadEmbeddedJson("Victory_messages.json");
                _victoryMessages = LoadListFromJson(victoryJson, options, "VictoryMessages");

                // 3. Load Battle Phrases from Memory
                string battleJson = ResourceHelper.ReadEmbeddedJson("Battle_Messages.json");
                if (!string.IsNullOrEmpty(battleJson))
                {
                    _battlePhrases = JsonSerializer.Deserialize<BattleData>(battleJson, options) ?? new();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"🔥 RPG Data Error: {ex.Message}");
            }
        }

        private List<string> LoadListFromJson(string jsonContent, JsonSerializerOptions opt, string key)
        {
            if (string.IsNullOrEmpty(jsonContent)) return new List<string>();

            try
            {
                using var doc = JsonDocument.Parse(jsonContent);

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return doc.RootElement.EnumerateArray().Select(x => x.GetString()).ToList();
                }

                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var property = doc.RootElement.EnumerateObject()
                        .FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase));

                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        return property.Value.EnumerateArray().Select(x => x.GetString()).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"🔥 JSON Parse Error for '{key}': {ex.Message}");
            }

            return new List<string>();
        }

        // --- THE SAFETY SHIELD ---
        private string SafeGet(List<string> list, string fallback)
        {
            if (list == null || list.Count == 0) return fallback;
            return list[_rnd.Next(list.Count)];
        }

        private void SetupIdleTimer()
        {
            _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _idleTimer.Tick += async (s, e) =>
            {
                if (_battlePhrases != null && CombatMessageColor == "Gray")
                {
                    string cleanName = GetCleanBossName();

                    string act = SafeGet(_battlePhrases.MissActions, "You prepare your next move...");
                    string evade = SafeGet(_battlePhrases.MonsterEvades, "The {0} watches you closely.");

                    CombatMessage = string.Format($"{act} {evade}", cleanName);

                    await Task.Delay(5000);
                    if (CombatMessageColor == "Gray") CombatMessage = "Waiting for your move...";
                }
            };
            _idleTimer.Start();
        }

        public void HandleCombat(bool isCorrect, ProgressTracker progress, int hitXp = 10)
        {
            string cleanName = GetCleanBossName();

            if (isCorrect)
            {
                ComboStreak++;
                int damage = 20 + ((ComboStreak - 1) * 5);

                BossCurrentHealth -= damage;
                progress.GainXP(hitXp);

                if (BossCurrentHealth <= 0)
                {
                    _bossesDefeated++;
                    BossCurrentHealth = 0;

                    string vMsg = SafeGet(_victoryMessages, "Defeated {0}!");
                    CombatMessage = string.Format(vMsg, cleanName);
                    CombatMessageColor = "#4CAF50";

                    progress.GainXP(50);
                    progress.QaCoins += 2;
                    SpawnNextBoss();
                }
                else
                {
                    string act = SafeGet(_battlePhrases.HitActions, "You strike the {0}!");
                    string react = SafeGet(_battlePhrases.HitReactions, "It takes damage!");
                    CombatMessage = string.Format($"{act} {react} (-{damage} HP)", cleanName);
                    CombatMessageColor = "#2196F3";
                }
            }
            else
            {
                ComboStreak = 0;

                string evade = SafeGet(_battlePhrases.MonsterEvades, "The {0} dodges!");
                string counter = SafeGet(_battlePhrases.MonsterCounters, "It strikes back!");
                CombatMessage = string.Format($"{evade} {counter}", cleanName);
                CombatMessageColor = "#F44336";
            }
        }

        private async void SpawnNextBoss()
        {
            if (_bossesDefeated > 0)
            {
                await Task.Delay(2000);
            }

            BossMaxHealth = 100 + (_bossesDefeated * 25);
            BossCurrentHealth = BossMaxHealth;

            // 🔥 NEW: Safe Object Spawn
            if (_monsters != null && _monsters.Count > 0)
            {
                var monster = _monsters[_rnd.Next(_monsters.Count)];
                BossName = $"Lv.{_bossesDefeated + 1} {monster.Name}";
                BossIcon = monster.Icon ?? "👹";
            }
            else
            {
                BossName = $"Lv.{_bossesDefeated + 1} Troll";
                BossIcon = "👹"; // Fallback if JSON failed to load
            }

            if (CombatMessageColor != "Gray")
            {
                CombatMessage = "A new challenger appears!";
                CombatMessageColor = "Gray";
            }
        }

        private string GetCleanBossName()
        {
            int spaceIndex = BossName.IndexOf(' ');
            if (spaceIndex >= 0 && spaceIndex < BossName.Length - 1)
            {
                return BossName.Substring(spaceIndex + 1);
            }
            return BossName;
        }

        // --- INNER CLASSES ---
        public class MonsterDto
        {
            public string Name { get; set; }
            public string Icon { get; set; }
        }
        private class MonsterRoot { public List<MonsterDto> Monsters { get; set; } }
        private class VictoryRoot { public List<string> VictoryMessages { get; set; } }
        private class BattleData
        {
            public List<string> HitActions { get; set; } = new();
            public List<string> HitReactions { get; set; } = new();
            public List<string> MissActions { get; set; } = new();
            public List<string> MonsterEvades { get; set; } = new();
            public List<string> MonsterCounters { get; set; } = new();
        }
    }
}