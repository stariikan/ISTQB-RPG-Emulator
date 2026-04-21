using ISTQBEmulator.Core.Models;
using ISTQBEmulator.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ISTQBEmulator.ViewModels
{
    public class LearningEngine : IStudyEngine
    {
        private string _termType;
        private int _termsViewed = 0;
        private List<GlossaryTerm> _recentTermsQueue = new();
        private Queue<Question> _activeQuizQueue = new();

        // --- NEW: SEGMENTED MEMORY & END-GAME STATE ---
        private List<GlossaryTerm> _flashcardHistory = new();
        private int _flashcardIndex = -1;
        private bool _isEndGame = false;

        public LearningEngine(string termType)
        {
            _termType = termType; // Accepts "GlossaryTerm" or "SyllabusConcept"
        }

        public async Task LoadNextAsync(MainViewModel vm, AppDbContext db)
        {
            vm.IsExplanationVisible = false;
            vm.FeedbackMessage = string.Empty;
            vm.IsInteractionEnabled = true;
            vm.SelectedOptions.Clear();

            // Force Exam UI off when Learning Mode loads
            vm.ExamNavVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            vm.TimerVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;

            // Clear any lingering images
            vm.QuestionImage = null;
            vm.QuestionImageVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            vm.ExplanationImage = null;
            vm.ExplanationImageVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;

            // 1. Serve Quiz if active
            if (_activeQuizQueue.Any())
            {
                var nextQuestion = _activeQuizQueue.Dequeue();
                foreach (var v in nextQuestion.Variants) v.BackgroundColor = "Transparent";
                vm.CurrentQuestion = nextQuestion;

                // 🔥 Load Web Image for the Quiz (if the term had one)
                if (!string.IsNullOrEmpty(nextQuestion.ImageName))
                {
                    vm.QuestionImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(nextQuestion.ImageName));
                    vm.QuestionImageVisibility = Microsoft.UI.Xaml.Visibility.Visible;
                }

                vm.HasVariants = true;
                // 🔥 NO BACKTRACKING DURING QUIZ
                vm.BackButtonVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                vm.ShowView("Quiz");
                return;
            }

            // 2. Generate Quiz if 5 terms viewed AND we are at the end of the batch
            if (_termsViewed >= 5 && _recentTermsQueue.Any() && _flashcardIndex == _flashcardHistory.Count - 1)
            {
                await GenerateProgressQuizAsync(db);
                await LoadNextAsync(vm, db);
                return;
            }

            // 3. Navigate forward in history OR fetch a new Flashcard
            _flashcardIndex++;

            if (_flashcardIndex < _flashcardHistory.Count)
            {
                // We are moving forward through our CURRENT batch of 5
                LoadFlashcardUI(vm, _flashcardHistory[_flashcardIndex]);
            }
            else
            {
                // We need a brand new term
                var historyIds = _flashcardHistory.Select(h => h.Id).ToList();

                var nextTerm = await db.GlossaryTerms
                    .Where(t => t.Type == _termType && !t.IsPassed && !historyIds.Contains(t.Id))
                    .OrderBy(t => EF.Functions.Random())
                    .FirstOrDefaultAsync();

                // 🔥 END-GAME LOGIC: If no unpassed terms remain, pull ANY term
                if (nextTerm == null)
                {
                    _isEndGame = true;
                    nextTerm = await db.GlossaryTerms
                        .Where(t => t.Type == _termType && !historyIds.Contains(t.Id))
                        .OrderBy(t => EF.Functions.Random())
                        .FirstOrDefaultAsync();
                }

                if (nextTerm != null)
                {
                    _flashcardHistory.Add(nextTerm);
                    _recentTermsQueue.Add(nextTerm);
                    _termsViewed++;
                    LoadFlashcardUI(vm, nextTerm);
                }
                else
                {
                    // Fallback
                    _flashcardIndex--;
                }
            }
        }

        // 🔥 ALLOW BACKTRACKING (Only during the flashcard phase)
        public Task LoadPreviousAsync(MainViewModel vm, AppDbContext db)
        {
            if (_activeQuizQueue.Any()) return Task.CompletedTask; // Blocked during Quiz

            if (_flashcardIndex > 0)
            {
                _flashcardIndex--;
                LoadFlashcardUI(vm, _flashcardHistory[_flashcardIndex]);
            }
            return Task.CompletedTask;
        }

        private void LoadFlashcardUI(MainViewModel vm, GlossaryTerm term)
        {
            vm.CurrentLearningTerm = term;

            // Show back button only if there is history IN THIS CURRENT BATCH
            vm.BackButtonVisibility = _flashcardIndex > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

            if (!string.IsNullOrEmpty(term.ImageUrl))
            {
                vm.QuestionImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(term.ImageUrl));
                vm.QuestionImageVisibility = Microsoft.UI.Xaml.Visibility.Visible;
            }

            vm.ShowView("LearningCard");
        }

        private async Task GenerateProgressQuizAsync(AppDbContext db)
        {
            _activeQuizQueue.Clear();
            var rnd = new Random();
            var shuffledTerms = _recentTermsQueue.OrderBy(x => Guid.NewGuid()).ToList();

            foreach (var targetTerm in shuffledTerms)
            {
                var incorrectTerms = await db.GlossaryTerms
                    .Where(t => t.Type == _termType && t.Id != targetTerm.Id)
                    .OrderBy(t => EF.Functions.Random())
                    .Take(3)
                    .ToListAsync();

                bool askForDefinition = rnd.Next(2) == 0;
                var variants = new List<AnswerVariant>();
                char label = 'A';

                if (askForDefinition)
                {
                    var allOptions = incorrectTerms.Select(t => t.Definition).ToList();
                    allOptions.Add(targetTerm.Definition);
                    allOptions = allOptions.OrderBy(x => Guid.NewGuid()).ToList();
                    foreach (var opt in allOptions) variants.Add(new AnswerVariant { Label = (label++).ToString(), Text = opt });

                    _activeQuizQueue.Enqueue(new Question
                    {
                        Text = $"What is the correct definition for: {targetTerm.Term}?",
                        SelectionType = "Select ONE option.",
                        Variants = variants,
                        CorrectAnswer = variants.First(v => v.Text == targetTerm.Definition).Label,
                        Explanation = $"The official syllabus definition for '{targetTerm.Term}' is:\n{targetTerm.Definition}",
                        Source = targetTerm.Id.ToString(),
                        ImageName = targetTerm.ImageUrl
                    });
                }
                else
                {
                    var allOptions = incorrectTerms.Select(t => t.Term).ToList();
                    allOptions.Add(targetTerm.Term);
                    allOptions = allOptions.OrderBy(x => Guid.NewGuid()).ToList();
                    foreach (var opt in allOptions) variants.Add(new AnswerVariant { Label = (label++).ToString(), Text = opt });

                    _activeQuizQueue.Enqueue(new Question
                    {
                        Text = $"Which term matches this definition?\n\n\"{targetTerm.Definition}\"",
                        SelectionType = "Select ONE option.",
                        Variants = variants,
                        CorrectAnswer = variants.First(v => v.Text == targetTerm.Term).Label,
                        Explanation = $"The correct term is '{targetTerm.Term}'.",
                        Source = targetTerm.Id.ToString(),
                        ImageName = targetTerm.ImageUrl
                    });
                }
            }

            // 🔥 WIPE MEMORY FOR THE NEXT BATCH
            _recentTermsQueue.Clear();
            _flashcardHistory.Clear();
            _flashcardIndex = -1;
            _termsViewed = 0;
        }

        public void SubmitAnswer(MainViewModel vm, AppDbContext db, string selectedLabel)
        {
            if (vm.CurrentQuestion == null || !vm.IsInteractionEnabled) return;

            var correctList = vm.CurrentQuestion.CorrectAnswer?.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>();
            int requiredAnswers = correctList.Count;

            if (vm.SelectedOptions.Contains(selectedLabel))
            {
                vm.SelectedOptions.Remove(selectedLabel);
                vm.CurrentQuestion.Variants.First(v => v.Label == selectedLabel).BackgroundColor = "Transparent";
            }
            else
            {
                vm.SelectedOptions.Add(selectedLabel);
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

                bool wasAlreadyPassed = false;
                GlossaryTerm termToUpdate = null;

                if (int.TryParse(vm.CurrentQuestion.Source, out int termId))
                {
                    termToUpdate = db.GlossaryTerms.Find(termId);
                    wasAlreadyPassed = termToUpdate != null && termToUpdate.IsPassed;
                }

                if (isCompletelyCorrect)
                {
                    vm.FeedbackMessage = "✅ Correct! (+10 XP)";
                    vm.HandleCombat(true); // ALWAYS give XP and trigger RPG combat!

                    // Only increment the DB/UI Progress Bar if it's their FIRST time passing it
                    if (termToUpdate != null && !wasAlreadyPassed)
                    {
                        termToUpdate.IsPassed = true;
                        db.SaveChanges();

                        if (_termType == "GlossaryTerm") vm.UserProgress.PassedGlossaryTerms++;
                        else vm.UserProgress.PassedSyllabusConcepts++;
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