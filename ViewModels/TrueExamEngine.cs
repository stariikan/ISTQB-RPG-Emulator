using ISTQBEmulator.Core.Models;
using ISTQBEmulator.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ISTQBEmulator.ViewModels
{
    public class TrueExamEngine : IStudyEngine
    {
        private List<Question> _examQuestions = new();
        private int _currentIndex = 0;
        private Dictionary<int, HashSet<string>> _userAnswers = new();

        public async Task LoadNextAsync(MainViewModel vm, AppDbContext db)
        {
            if (!_examQuestions.Any())
            {
                // First load: Get 40 questions and setup the 1-40 Navigation Grid
                _examQuestions = await db.Questions.Include(q => q.Variants).OrderBy(r => EF.Functions.Random()).Take(40).ToListAsync();
                _currentIndex = 0;

                vm.ExamNavigationList.Clear();
                for (int i = 0; i < _examQuestions.Count; i++)
                {
                    vm.ExamNavigationList.Add(new QuestionNavItem { Index = i, DisplayNumber = (i + 1).ToString(), BgColor = "Transparent", TextColor = "Gray" });
                }
                vm.ExamNavVisibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            else
            {
                // If we hit next on the last question, Finish!
                if (_currentIndex >= _examQuestions.Count - 1)
                {
                    FinishExam(vm);
                    return;
                }
                _currentIndex++;
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

        public async void JumpToQuestion(MainViewModel vm, int index)
        {
            if (index >= 0 && index < _examQuestions.Count)
            {
                _currentIndex = index;
                await LoadQuestionDataAsync(vm);
            }
        }

        // 🔥 PART 1: Async Loader - Pulls heavy image data from memory
        private async Task LoadQuestionDataAsync(MainViewModel vm)
        {
            vm.IsExplanationVisible = false;
            vm.IsInteractionEnabled = true;
            vm.CurrentQuestion = _examQuestions[_currentIndex];

            // 1. Load Question Image
            if (!string.IsNullOrEmpty(vm.CurrentQuestion.ImageName))
            {
                vm.QuestionImage = await ResourceHelper.LoadEmbeddedImageAsync(vm.CurrentQuestion.ImageName);
                vm.QuestionImageVisibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            else { vm.QuestionImageVisibility = Microsoft.UI.Xaml.Visibility.Collapsed; }

            // 2. Load Explanation Image
            if (!string.IsNullOrEmpty(vm.CurrentQuestion.ExplanationImageName))
            {
                vm.ExplanationImage = await ResourceHelper.LoadEmbeddedImageAsync(vm.CurrentQuestion.ExplanationImageName);
                vm.ExplanationImageVisibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            else { vm.ExplanationImageVisibility = Microsoft.UI.Xaml.Visibility.Collapsed; }

            int qId = vm.CurrentQuestion.Id;
            var selectedLabels = _userAnswers.ContainsKey(qId) ? _userAnswers[qId] : new HashSet<string>();

            // 3. Load Variant Images AND Restore Highlights
            foreach (var v in vm.CurrentQuestion.Variants)
            {
                if (!string.IsNullOrEmpty(v.ImageName))
                {
                    v.VariantImage = await ResourceHelper.LoadEmbeddedImageAsync(v.ImageName);
                    v.VariantImageVisibility = Microsoft.UI.Xaml.Visibility.Visible;
                }
                else { v.VariantImageVisibility = Microsoft.UI.Xaml.Visibility.Collapsed; }

                v.BackgroundColor = selectedLabels.Contains(v.Label) ? "#802196F3" : "Transparent";
            }

            vm.HasVariants = vm.CurrentQuestion.Variants.Count > 0;
            UpdateNavigationUI(vm);
            vm.ShowView("Quiz");
        }

        // 🔥 PART 2: Fast UI Updater - Changes colors instantly without reloading images
        private void UpdateNavigationUI(MainViewModel vm)
        {
            int answeredCount = _examQuestions.Count(q => _userAnswers.ContainsKey(q.Id) && _userAnswers[q.Id].Count > 0);
            vm.CanFinishExam = (answeredCount == _examQuestions.Count);

            for (int i = 0; i < vm.ExamNavigationList.Count; i++)
            {
                var navItem = vm.ExamNavigationList[i];
                int loopQId = _examQuestions[i].Id;
                bool isAnswered = _userAnswers.ContainsKey(loopQId) && _userAnswers[loopQId].Count > 0;

                if (i == _currentIndex) { navItem.BgColor = "#2196F3"; navItem.TextColor = "White"; }
                else if (isAnswered) { navItem.BgColor = "#E0E0E0"; navItem.TextColor = "Black"; }
                else { navItem.BgColor = "Transparent"; navItem.TextColor = "Gray"; }
            }

            vm.BackButtonVisibility = (_currentIndex == 0) ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

            if (_currentIndex == _examQuestions.Count - 1)
            {
                vm.NextButtonVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                vm.FinishButtonVisibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            else
            {
                vm.NextButtonVisibility = Microsoft.UI.Xaml.Visibility.Visible;
                vm.FinishButtonVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }

            vm.RpgStats.CombatMessage = $"Question {_currentIndex + 1} of {_examQuestions.Count}";
            vm.RpgStats.CombatMessageColor = "Gray";
        }

        public void SubmitAnswer(MainViewModel vm, AppDbContext db, string selectedLabel)
        {
            if (vm.CurrentQuestion == null || !vm.IsInteractionEnabled) return;

            int qId = vm.CurrentQuestion.Id;
            if (!_userAnswers.ContainsKey(qId)) _userAnswers[qId] = new HashSet<string>();

            var selected = _userAnswers[qId];
            var correctList = vm.CurrentQuestion.CorrectAnswer?.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>();
            int requiredAnswers = correctList.Count;

            // Single-Choice logic: Clear previous if they click a DIFFERENT answer
            if (requiredAnswers <= 1 && !selected.Contains(selectedLabel))
            {
                selected.Clear();
            }

            // Multi-Choice logic: Prevent selecting more than allowed
            if (requiredAnswers > 1 && !selected.Contains(selectedLabel) && selected.Count >= requiredAnswers) return;

            // Toggle selection
            if (selected.Contains(selectedLabel))
            {
                selected.Remove(selectedLabel);
            }
            else
            {
                selected.Add(selectedLabel);
            }

            // Update the Answer Variant highlights using our new observable property!
            foreach (var v in vm.CurrentQuestion.Variants)
            {
                v.BackgroundColor = selected.Contains(v.Label) ? "#802196F3" : "Transparent";
            }

            // Fast UI update without reloading the images!
            UpdateNavigationUI(vm);
        }

        private void FinishExam(MainViewModel vm)
        {
            vm.StopTimer();
            int correctCount = 0;
            var results = new List<ExamResultItem>();

            foreach (var q in _examQuestions)
            {
                var correctLabels = q.CorrectAnswer?.Split(',').Select(s => s.Trim()).ToList() ?? new List<string>();
                var userLabels = _userAnswers.ContainsKey(q.Id) ? _userAnswers[q.Id] : new HashSet<string>();

                bool isCorrect = correctLabels.Count == userLabels.Count && correctLabels.All(c => userLabels.Contains(c));
                if (isCorrect) correctCount++;

                results.Add(new ExamResultItem
                {
                    QuestionText = q.Text,
                    UserAnswer = userLabels.Any() ? string.Join(", ", userLabels.OrderBy(l => l)) : "No Answer",
                    CorrectAnswer = q.CorrectAnswer,
                    Explanation = q.Explanation,
                    IsCorrect = isCorrect
                });
            }

            double scorePercentage = (double)correctCount / _examQuestions.Count * 100;
            bool passed = scorePercentage >= 65;

            int earnedXP = correctCount * 10;
            if (passed)
            {
                earnedXP += 500;
                // Increment the permanent stat so Achievements can track it!
                vm.UserProgress.PassedTrueExams++;
            }
            vm.UserProgress.GainXP(earnedXP);

            vm.ExamScoreText = $"Final Score: {correctCount} / {_examQuestions.Count} ({scorePercentage:F1}%)";
            vm.ExamPassFailText = passed ? "🎉 YOU PASSED!" : "❌ YOU FAILED";
            vm.ExamPassFailColor = passed ? "#FF2E7D32" : "#FFC62828";

            // Cleanup UI for next time
            vm.ExamNavVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            vm.NextButtonVisibility = Microsoft.UI.Xaml.Visibility.Visible;
            vm.FinishButtonVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            vm.BackButtonVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;

            vm.ExamResultsList = results;
            vm.ShowView("Results");
        }
    }
}