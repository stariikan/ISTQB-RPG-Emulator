using CommunityToolkit.Mvvm.ComponentModel;
using ISTQBEmulator.Core.Models;
using ISTQBEmulator.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ISTQBEmulator.ViewModels
{
    public partial class ProgressTracker : ObservableObject
    {
        // --- DATABASE STATS ---
        [ObservableProperty] private int _totalGlossaryTerms = 0;
        [ObservableProperty] private int _passedGlossaryTerms = 0;
        [ObservableProperty] private int _totalSyllabusConcepts = 0;
        [ObservableProperty] private int _passedSyllabusConcepts = 0;
        [ObservableProperty] private int _totalPracticeQuestions = 0;
        [ObservableProperty] private int _passedPracticeQuestions = 0;
        [ObservableProperty] private int _passedTrueExams = 0;

        // --- GAMIFICATION STATS ---
        [ObservableProperty] private int _experiencePoints = 0;
        [ObservableProperty] private int _level = 1;
        [ObservableProperty] private int _qaCoins = 0;
        [ObservableProperty] private string _currentRank = "Clueless Intern";
        [ObservableProperty] private double _levelProgressPercentage = 0;
        [ObservableProperty] private string _xpDisplay = "0/100 XP";

        // 🔥 NEW: Hidden field to track extra XP that isn't tied to passing a 10XP card
        private int _bonusXP = 0;

        // --- JSON SAVE/LOAD LOGIC ---
        private string GetSavePath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ISTQBEmulator");
            Directory.CreateDirectory(folder); // Ensures the folder exists
            return Path.Combine(folder, "playersave.json");
        }

        private void LoadSaveFile()
        {
            string path = GetSavePath();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var saveData = JsonSerializer.Deserialize<PlayerSaveData>(json);
                    if (saveData != null)
                    {
                        _bonusXP = saveData.BonusXP;
                        PassedTrueExams = saveData.PassedTrueExams;
                    }
                }
                catch
                {
                    // If the save file is corrupted, we just start bonus XP at 0 safely.
                }
            }
        }

        public void SaveProgress()
        {
            string path = GetSavePath();
            var saveData = new PlayerSaveData
            {
                BonusXP = _bonusXP,
                PassedTrueExams = this.PassedTrueExams
            };
            string json = JsonSerializer.Serialize(saveData);
            File.WriteAllText(path, json);
        }

        // --- CORE LOGIC ---

        public async Task InitializeFromDatabaseAsync(AppDbContext db)
        {
            await db.Database.EnsureCreatedAsync();

            // 1. Calculate Base Progress from the DB
            TotalGlossaryTerms = await db.GlossaryTerms.CountAsync(t => t.Type == "GlossaryTerm");
            PassedGlossaryTerms = await db.GlossaryTerms.CountAsync(t => t.Type == "GlossaryTerm" && t.IsPassed);
            TotalSyllabusConcepts = await db.GlossaryTerms.CountAsync(t => t.Type == "SyllabusConcept");
            PassedSyllabusConcepts = await db.GlossaryTerms.CountAsync(t => t.Type == "SyllabusConcept" && t.IsPassed);
            TotalPracticeQuestions = await db.Questions.CountAsync();
            PassedPracticeQuestions = await db.Questions.CountAsync(q => q.IsPassed);

            // 2. Load Bonus Stats from JSON
            LoadSaveFile();

            // 3. Calculate Total XP (10 per passed item + Bonus)
            int baseXP = (PassedGlossaryTerms + PassedSyllabusConcepts + PassedPracticeQuestions) * 10;
            ExperiencePoints = baseXP + _bonusXP;

            CalculateLevelAndRank();
        }

        public void GainXP(int amount)
        {
            ExperiencePoints += amount;

            // If they earned more than 10 XP (like a Combo or Boss Kill), save the extra!
            if (amount > 10)
            {
                _bonusXP += (amount - 10);
                SaveProgress();
            }

            CalculateLevelAndRank();
        }

        // Use this specific method in your TrueExamEngine when they pass!
        public void RecordTrueExamPass()
        {
            PassedTrueExams++;
            _bonusXP += 500;
            ExperiencePoints += 500;
            SaveProgress();
            CalculateLevelAndRank();
        }

        private void CalculateLevelAndRank()
        {
            int newLevel = (ExperiencePoints / 100) + 1;

            if (newLevel > Level)
            {
                int levelsGained = newLevel - Level;
                QaCoins += (levelsGained * 5);
            }

            Level = newLevel;
            int currentLevelXP = ExperiencePoints % 100;
            LevelProgressPercentage = currentLevelXP;
            XpDisplay = $"{currentLevelXP}/100 XP";

            CurrentRank = Level switch
            {
                1 => "Clueless Intern",
                2 => "Coffee Fetcher",
                3 => "Bug Hunter Initiate",
                4 => "Syntax Scrutinizer",
                5 => "Junior Tester",
                6 => "Console Cowboy",
                7 => "Traceback Tracker",
                8 => "Happy Path Walker",
                9 => "Edge Case Explorer",
                10 => "QA Analyst",
                11 => "Boundary Value Brawler",
                12 => "Equivalence Partitioner",
                13 => "Smoke Test Sentinel",
                14 => "Regression Ranger",
                15 => "Defect Detective",
                16 => "Black-Box Brawler",
                17 => "White-Box Wanderer",
                18 => "Usability Umpire",
                19 => "Metric Mercenary",
                20 => "Automation Engineer",
                21 => "Selenium Sorcerer",
                22 => "Cypress Centurion",
                23 => "API Assassin",
                24 => "Postman Paladin",
                25 => "CI/CD Commander",
                26 => "Pipeline Pioneer",
                27 => "Flaky Test Fixer",
                28 => "Mock Object Master",
                29 => "Test Data Titan",
                30 => "Senior SDET",
                31 => "Framework Architect",
                32 => "Load Test Lumberjack",
                33 => "Stress Test Samurai",
                34 => "Performance Prophet",
                35 => "Security Sentinel",
                36 => "Penetration Paladin",
                37 => "Chaos Engineer",
                38 => "Mutation Mage",
                39 => "Heuristic Hero",
                40 => "QA Lead",
                41 => "Bug Triage Boss",
                42 => "Release Ruler",
                43 => "Sprint Protector",
                44 => "Quality Gatekeeper",
                45 => "Defect Zero Hero",
                46 => "Shift-Left Legend",
                47 => "Agile Alchemist",
                48 => "V-Model Vanguard",
                49 => "Master of the Syllabus",
                50 => "Test Manager",
                _ => "QA Emperor" // The "_" handles level 51 and infinitely beyond
            };
        }

        public async Task ResetAllProgressAsync(AppDbContext db)
        {
            // Wipe the database
            var allTerms = await db.GlossaryTerms.ToListAsync();
            foreach (var term in allTerms) term.IsPassed = false;

            var allQuestions = await db.Questions.ToListAsync();
            foreach (var q in allQuestions) q.IsPassed = false;

            await db.SaveChangesAsync();

            // Wipe Gamification & Save File
            _bonusXP = 0;
            ExperiencePoints = 0;
            QaCoins = 0;
            PassedTrueExams = 0;
            SaveProgress(); // Writes the zeros to the JSON file

            await InitializeFromDatabaseAsync(db);
        }
    }
}