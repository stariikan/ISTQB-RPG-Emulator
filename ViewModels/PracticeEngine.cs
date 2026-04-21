using ISTQBEmulator.Core.Models;
using ISTQBEmulator.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ISTQBEmulator.ViewModels
{
    public class PracticeEngine : IStudyEngine
    {
        // --- MEMORY & HISTORY ---
        private List<Question> _history = new();
        private int _currentIndex = -1;
        private Dictionary<int, HashSet<string>> _userAnswers = new();

        // --- END-GAME FLAG ---
        private bool _isEndGame = false;

        public async Task LoadNextAsync(MainViewModel vm, AppDbContext db)
        {
            _currentIndex++;

            // If we are at the edge of our history, fetch a brand new question
            if (_currentIndex >= _history.Count)
            {
                var historyIds = _history.Select(h => h.Id).ToList();

                // 1. Try to find a question the user HAS NOT passed yet
                var newQuestion = await db.Questions
                    .Include(q => q.Variants)
                    .Where(q => !q.IsPassed && !historyIds.Contains(q.Id))
                    .OrderBy(r => EF.Functions.Random())
                    .FirstOrDefaultAsync();

                // 2. END-GAME LOOP: If no unpassed questions exist, pull ANY question!
                if (newQuestion == null)
                {
                    _isEndGame = true;
                    newQuestion = await db.Questions
                        .Include(q => q.Variants)
                        .Where(q => !historyIds.Contains(q.Id)) // Still avoid immediate repeats
                        .OrderBy(r => EF.Functions.Random())
                        .FirstOrDefaultAsync();
                }

                if (newQuestion != null)
                {
                    _history.Add(newQuestion);
                }
                else
                {
                    // Fallback if the database is literally empty
                    _currentIndex--;
                    return;
                }
            }

            await LoadQuestionDataAsync(vm);
        }

        public async Task LoadPreviousAsync(MainViewModel vm, AppDbContext db)
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                await LoadQuestionDataAsync(vm);
            }
        }

        private async Task LoadQuestionDataAsync(MainViewModel vm)
        {
            vm.IsExplanationVisible = false;
            vm.FeedbackMessage = string.Empty;
            vm.IsInteractionEnabled = true;
            vm.SelectedOptions.Clear();

            // Force Exam UI off when Practice Mode loads
            vm.ExamNavVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            vm.TimerVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;

            // Clear old images
            vm.QuestionImage = null;
            vm.QuestionImageVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            vm.ExplanationImage = null;
            vm.ExplanationImageVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;

            vm.CurrentQuestion = _history[_currentIndex];

            // 1. Load Question Image
            if (!string.IsNullOrEmpty(vm.CurrentQuestion.ImageName))
            {
                vm.QuestionImage = await ResourceHelper.LoadEmbeddedImageAsync(vm.CurrentQuestion.ImageName);
                vm.QuestionImageVisibility = Microsoft.UI.Xaml.Visibility.Visible;
            }

            // 2. Load Explanation Image
            if (!string.IsNullOrEmpty(vm.CurrentQuestion.ExplanationImageName))
            {
                vm.ExplanationImage = await ResourceHelper.LoadEmbeddedImageAsync(vm.CurrentQuestion.ExplanationImageName);
                vm.ExplanationImageVisibility = Microsoft.UI.Xaml.Visibility.Visible;
            }

            // 3. Restore History & Setup Variants
            int qId = vm.CurrentQuestion.Id;
            var savedLabels = _userAnswers.ContainsKey(qId) ? _userAnswers[qId] : new HashSet<string>();
            var correctList = vm.CurrentQuestion.CorrectAnswer?.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>();

            foreach (var v in vm.CurrentQuestion.Variants)
            {
                v.BackgroundColor = "Transparent";

                if (!string.IsNullOrEmpty(v.ImageName))
                {
                    v.VariantImage = await ResourceHelper.LoadEmbeddedImageAsync(v.ImageName);
                    v.VariantImageVisibility = Microsoft.UI.Xaml.Visibility.Visible;
                }
                else
                {
                    v.VariantImageVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                }

                // If user answered this previously, highlight their choice
                if (savedLabels.Contains(v.Label))
                {
                    vm.SelectedOptions.Add(v.Label);
                    v.BackgroundColor = "#802196F3";
                }
            }

            vm.HasVariants = vm.CurrentQuestion.Variants.Count > 0;

            // Show Back button only if we have history to go back to
            vm.BackButtonVisibility = _currentIndex > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

            // 4. Anti-Farming Lock: If they already answered this previously in this session
            if (savedLabels.Count >= correctList.Count && correctList.Count > 0)
            {
                vm.IsInteractionEnabled = false;
                vm.IsExplanationVisible = true;

                bool isCompletelyCorrect = true;
                foreach (var variant in vm.CurrentQuestion.Variants)
                {
                    bool isCorrectAnswer = correctList.Contains(variant.Label);
                    bool wasSelectedByUser = savedLabels.Contains(variant.Label);

                    if (isCorrectAnswer) { variant.BackgroundColor = "#FF2E7D32"; if (!wasSelectedByUser) isCompletelyCorrect = false; }
                    else if (wasSelectedByUser) { variant.BackgroundColor = "#FFC62828"; isCompletelyCorrect = false; }
                    else { variant.BackgroundColor = "Transparent"; }
                }
                vm.FeedbackMessage = isCompletelyCorrect ? "✅ Correct! (Previously Answered)" : $"❌ Incorrect. The correct answer was {vm.CurrentQuestion.CorrectAnswer}.";
            }

            vm.ShowView("Quiz");
        }

        public void SubmitAnswer(MainViewModel vm, AppDbContext db, string selectedLabel)
        {
            if (vm.CurrentQuestion == null || !vm.IsInteractionEnabled) return;

            int qId = vm.CurrentQuestion.Id;
            if (!_userAnswers.ContainsKey(qId)) _userAnswers[qId] = new HashSet<string>();
            var selected = _userAnswers[qId];

            var correctList = vm.CurrentQuestion.CorrectAnswer?.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>();
            int requiredAnswers = correctList.Count;

            // Single-Choice / Multi-Choice logic
            if (vm.SelectedOptions.Contains(selectedLabel))
            {
                vm.SelectedOptions.Remove(selectedLabel);
                selected.Remove(selectedLabel);
                vm.CurrentQuestion.Variants.First(v => v.Label == selectedLabel).BackgroundColor = "Transparent";
            }
            else
            {
                vm.SelectedOptions.Add(selectedLabel);
                selected.Add(selectedLabel);
                vm.CurrentQuestion.Variants.First(v => v.Label == selectedLabel).BackgroundColor = "#802196F3";
            }

            if (vm.SelectedOptions.Count >= requiredAnswers)
            {
                vm.IsInteractionEnabled = false;
                bool isCompletelyCorrect = true;

                foreach (var variant in vm.CurrentQuestion.Variants)
                {
                    bool isCorrectAnswer = correctList.Contains(variant.Label);
                    bool wasSelectedByUser = vm.SelectedOptions.Contains(variant.Label);

                    if (isCorrectAnswer) { variant.BackgroundColor = "#FF2E7D32"; if (!wasSelectedByUser) isCompletelyCorrect = false; }
                    else if (wasSelectedByUser) { variant.BackgroundColor = "#FFC62828"; isCompletelyCorrect = false; }
                    else { variant.BackgroundColor = "Transparent"; }
                }

                var qToUpdate = db.Questions.Find(vm.CurrentQuestion.Id);
                bool wasAlreadyPassed = qToUpdate != null && qToUpdate.IsPassed;

                if (isCompletelyCorrect)
                {
                    vm.FeedbackMessage = "✅ Correct! (+10 XP)";
                    vm.HandleCombat(true); // ALWAYS give XP and trigger RPG combat!

                    // Only increment the DB/UI Progress Bar if it's their FIRST time passing it
                    if (qToUpdate != null && !wasAlreadyPassed)
                    {
                        qToUpdate.IsPassed = true;
                        db.SaveChanges();

                        // 🔥 Update Live Memory for Instant Achievement Popups
                        vm.UserProgress.PassedPracticeQuestions++;
                    }
                }
                else
                {
                    vm.FeedbackMessage = $"❌ Incorrect. The correct answer was {vm.CurrentQuestion.CorrectAnswer}.";
                    vm.HandleCombat(false); // ALWAYS trigger RPG combat (combo break)
                }

                vm.IsExplanationVisible = true;
            }
            vm.RefreshQuestion();
        }
    }
}