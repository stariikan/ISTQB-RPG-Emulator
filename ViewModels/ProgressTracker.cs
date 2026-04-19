using CommunityToolkit.Mvvm.ComponentModel;
using ISTQBEmulator.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
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

        // NEW: Property for the "X/100 XP" UI label
        [ObservableProperty] private string _xpDisplay = "0/100 XP";

        public void GainXP(int amount)
        {
            ExperiencePoints += amount;
            CalculateLevelAndRank();
        }

        private void CalculateLevelAndRank()
        {
            // Level up every 100 XP
            int newLevel = (ExperiencePoints / 100) + 1;

            if (newLevel > Level)
            {
                int levelsGained = newLevel - Level;
                QaCoins += (levelsGained * 5); // Reward 5 coins per level
            }

            Level = newLevel;

            // Calculate progress toward the next level (0-99)
            int currentLevelXP = ExperiencePoints % 100;
            LevelProgressPercentage = currentLevelXP;

            // Update the string display for the UI
            XpDisplay = $"{currentLevelXP}/100 XP";

            // Updated Corporate Ladder with funny ranks
            CurrentRank = Level switch
            {
                1 => "Clueless Intern",
                < 5 => "Junior Tester",
                < 10 => "QA Analyst",
                < 20 => "Automation Engineer",
                < 30 => "Senior SDET",
                < 40 => "QA Lead",
                < 50 => "Test Manager",
                _ => "ISTQB Grandmaster"
            };
        }

        public void UpdateStats(AppDbContext db)
        {
            db.Database.EnsureCreated();
            TotalGlossaryTerms = db.GlossaryTerms.Count(t => t.Type == "GlossaryTerm");
            PassedGlossaryTerms = db.GlossaryTerms.Count(t => t.Type == "GlossaryTerm" && t.IsPassed);
            TotalSyllabusConcepts = db.GlossaryTerms.Count(t => t.Type == "SyllabusConcept");
            PassedSyllabusConcepts = db.GlossaryTerms.Count(t => t.Type == "SyllabusConcept" && t.IsPassed);
            TotalPracticeQuestions = db.Questions.Count();
            PassedPracticeQuestions = db.Questions.Count(q => q.IsPassed);
        }

        public async Task ResetAllProgressAsync(AppDbContext db)
        {
            var allTerms = await db.GlossaryTerms.ToListAsync();
            foreach (var term in allTerms) term.IsPassed = false;

            var allQuestions = await db.Questions.ToListAsync();
            foreach (var q in allQuestions) q.IsPassed = false;

            await db.SaveChangesAsync();

            // Reset Gamification
            ExperiencePoints = 0;
            QaCoins = 0;
            PassedTrueExams = 0;
            CalculateLevelAndRank();

            UpdateStats(db);
        }
    }
}