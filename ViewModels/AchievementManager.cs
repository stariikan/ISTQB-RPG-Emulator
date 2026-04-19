using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ISTQBEmulator.Data;
using ISTQBEmulator.Core.Models;
using ISTQBEmulator.ViewModels;

namespace ISTQBEmulator.Services
{
    public class AchievementManager
    {
        // =================================================================
        // 1. STATE-BASED ACHIEVEMENTS (Triggered when returning to menu)
        // =================================================================
        public async Task<List<Achievement>> CheckGlobalProgressAsync(ProgressTracker progress)
        {
            var newlyUnlocked = new List<Achievement>();
            using var db = new AppDbContext();

            // Only pull achievements that are NOT unlocked yet
            var lockedAchievements = await db.Achievements.Where(a => !a.IsUnlocked).ToListAsync();
            bool dbNeedsUpdate = false;

            foreach (var ach in lockedAchievements)
            {
                bool justUnlocked = false;

                // Match progress stats to the exact Categories defined in your DataSeeder
                if (ach.Category == "Glossary")
                {
                    ach.CurrentProgress = progress.PassedGlossaryTerms;
                }
                else if (ach.Category == "Syllabus")
                {
                    ach.CurrentProgress = progress.PassedSyllabusConcepts;
                }
                else if (ach.Category == "PracticeExam")
                {
                    // For the total practice questions answered over time
                    ach.CurrentProgress = progress.PassedPracticeQuestions;
                }
                else if (ach.Category == "TrueExam")
                {
                    ach.CurrentProgress = progress.PassedTrueExams;
                }
                else if (ach.Category == "Global" && !ach.Title.Contains("Streak") && !ach.Title.Contains("Combo"))
                {
                    // Handles "Getting Started" (Level 5), "Rising Star" (Level 10), etc.
                    ach.CurrentProgress = progress.Level;
                }

                // Did they hit the target?
                if (ach.CurrentProgress >= ach.TargetProgress)
                {
                    ach.CurrentProgress = ach.TargetProgress; // Max it out neatly
                    ach.IsUnlocked = true;
                    newlyUnlocked.Add(ach);
                    justUnlocked = true;
                }

                // If progress changed (even if not fully unlocked), flag the DB for a save
                // so the UI progress bars update (e.g., going from 5/50 to 6/50)
                if (ach.CurrentProgress > 0 || justUnlocked)
                {
                    dbNeedsUpdate = true;
                }
            }

            if (dbNeedsUpdate)
            {
                await db.SaveChangesAsync();
            }

            return newlyUnlocked;
        }

        // =================================================================
        // 2. EVENT-BASED ACHIEVEMENTS (Triggered mid-game by specific engines)
        // =================================================================
        public async Task<List<Achievement>> TriggerEventAchievementAsync(string category, string titleKeyword, int eventValue)
        {
            var newlyUnlocked = new List<Achievement>();
            using var db = new AppDbContext();

            // Find locked achievements matching the Category and a specific Keyword
            // e.g., Category: "Global", Keyword: "Streak"
            var matchingAchievements = await db.Achievements
                .Where(a => !a.IsUnlocked && a.Category == category && a.Title.Contains(titleKeyword))
                .ToListAsync();

            if (!matchingAchievements.Any()) return newlyUnlocked;

            bool dbNeedsUpdate = false;

            foreach (var ach in matchingAchievements)
            {
                // For Streaks/Combos: Only update if the new streak is higher than their previous best
                if (eventValue > ach.CurrentProgress)
                {
                    ach.CurrentProgress = eventValue;
                    dbNeedsUpdate = true;
                }

                // Did this specific event push them over the finish line?
                if (ach.CurrentProgress >= ach.TargetProgress)
                {
                    ach.CurrentProgress = ach.TargetProgress;
                    ach.IsUnlocked = true;
                    newlyUnlocked.Add(ach);
                }
            }

            if (dbNeedsUpdate)
            {
                await db.SaveChangesAsync();
            }

            return newlyUnlocked;
        }
    }
}