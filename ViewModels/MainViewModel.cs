using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ISTQBEmulator.Core.Models;
using ISTQBEmulator.Data;
using ISTQBEmulator.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ISTQBEmulator.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AppDbContext _db;

        // --- THE ENGINE ---
        private IStudyEngine _activeEngine;

        // ==========================================
        // --- UI STATE TRACKING ---
        // ==========================================
        [ObservableProperty] private Visibility _mainMenuVisibility = Visibility.Visible;
        [ObservableProperty] private Visibility _quizVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _learningCardVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _resultsVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _achievementsVisibility = Visibility.Collapsed;

        [ObservableProperty] private Visibility _playerCardVisibility = Visibility.Visible;

        // ==========================================
        // --- ACHIEVEMENTS ---
        // ==========================================
        [ObservableProperty] private Visibility _achievementPopupVisibility = Visibility.Collapsed;
        [ObservableProperty] private string _achievementPopupIcon = "🏆";
        [ObservableProperty] private string _achievementPopupTitle = "Achievement Unlocked!";
        [ObservableProperty] private string _achievementPopupDescription = "You did a thing!";
        private readonly AchievementManager _achievementManager = new();

        // ==========================================
        // --- PROGRESS & CORE DATA ---
        // ==========================================
        [ObservableProperty] private ProgressTracker _userProgress = new ProgressTracker();
        [ObservableProperty] private List<Achievement> _achievementsList = new();
        [ObservableProperty] private Question _currentQuestion = new Question();
        [ObservableProperty] private GlossaryTerm _currentLearningTerm = new GlossaryTerm();

        // IMAGE CONTAINERS FOR THE UI
        [ObservableProperty] private Microsoft.UI.Xaml.Media.Imaging.BitmapImage? _questionImage;
        [ObservableProperty] private Visibility _questionImageVisibility = Visibility.Collapsed;

        [ObservableProperty] private Microsoft.UI.Xaml.Media.Imaging.BitmapImage? _explanationImage;
        [ObservableProperty] private Visibility _explanationImageVisibility = Visibility.Collapsed;

        // ==========================================
        // --- QUIZ UI HELPERS ---
        // ==========================================
        [ObservableProperty] private string _feedbackMessage = string.Empty;
        [ObservableProperty] private bool _isExplanationVisible = false;
        [ObservableProperty] private bool _isInteractionEnabled = true;

        [ObservableProperty] private string _nextButtonText = "Next ➔";
        [ObservableProperty] private Microsoft.UI.Xaml.Visibility _backButtonVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FallbackButtonVisibility))]
        private bool _hasVariants = true;
        public Visibility FallbackButtonVisibility => HasVariants ? Visibility.Collapsed : Visibility.Visible;

        public HashSet<string> SelectedOptions { get; } = new HashSet<string>();

        // ==========================================
        // --- PASSIVE RPG BATTLE DATA ---
        // ==========================================
        [ObservableProperty] private Visibility _rpgHeaderVisibility = Visibility.Collapsed;
        [ObservableProperty] private RpgManager _rpgStats = new RpgManager();

        // ==========================================
        // --- TRUE EXAM RESULTS & TIMER DATA ---
        // ==========================================
        [ObservableProperty] private List<ExamResultItem> _examResultsList = new();
        [ObservableProperty] private string _examScoreText = string.Empty;
        [ObservableProperty] private string _examPassFailText = string.Empty;
        [ObservableProperty] private string _examPassFailColor = "#FFFFFF";

        [ObservableProperty] private Visibility _timerVisibility = Visibility.Collapsed;
        [ObservableProperty] private string _timerText = "60:00";
        private DispatcherTimer _examTimer;
        private int _timeRemainingInSeconds;

        // ==========================================
        // --- NAV SYS FOR TRUE EXAM --- 
        // ==========================================
        [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<QuestionNavItem> _examNavigationList = new();
        [ObservableProperty] private Microsoft.UI.Xaml.Visibility _examNavVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        [ObservableProperty] private Microsoft.UI.Xaml.Visibility _nextButtonVisibility = Microsoft.UI.Xaml.Visibility.Visible;
        [ObservableProperty] private Microsoft.UI.Xaml.Visibility _finishButtonVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        [ObservableProperty] private bool _canFinishExam = false;

        public MainViewModel()
        {
            _db = new AppDbContext();

            // Fire off our safe startup sequence
            _ = RunStartupSequenceAsync();

            ShowView("Menu");
        }

        // --- NEW STARTUP HELPER ---
        private async Task RunStartupSequenceAsync()
        {
            try
            {
                ISTQBEmulator.Services.AppLogger.Log("--- APP LAUNCHED ---");
                ISTQBEmulator.Services.AppLogger.Log("Starting DataSeeder...");

                await DataSeeder.InitializeAsync(_db);

                ISTQBEmulator.Services.AppLogger.Log("DataSeeder finished. Loading User Progress...");

                await UserProgress.InitializeFromDatabaseAsync(_db);

                ISTQBEmulator.Services.AppLogger.Log("Startup Sequence Complete.");
            }
            catch (Exception ex)
            {
                // This is the most important log! If it crashes, we will know exactly why.
                ISTQBEmulator.Services.AppLogger.Log($"CRITICAL STARTUP ERROR: {ex.Message}");
                ISTQBEmulator.Services.AppLogger.Log(ex.StackTrace);
            }
        }
        // ==========================================
        // --- ACHIEVEMENTS LOGIC ---
        // ==========================================
        public async Task EvaluateAchievementsAsync()
        {
            // Use the Global check for items that track over time
            List<Achievement> newlyUnlocked = await _achievementManager.CheckGlobalProgressAsync(UserProgress);

            if (newlyUnlocked.Any())
            {
                // If the user is staring at the Achievements page right now, refresh it!
                if (AchievementsVisibility == Visibility.Visible)
                {
                    using var db = new AppDbContext();
                    AchievementsList = await db.Achievements.ToListAsync();
                }

                // Show popups one by one (in case they unlocked 2 at the same time!)
                foreach (var ach in newlyUnlocked)
                {
                    await ShowAchievementPopupAsync(ach);
                }
            }
        }

        // The UI ACHIEVEMENTS Animation Method
        private async Task ShowAchievementPopupAsync(Achievement ach)
        {
            AchievementPopupIcon = ach.Icon;
            AchievementPopupTitle = ach.Title;
            AchievementPopupDescription = "Achievement Unlocked!";
            AchievementPopupVisibility = Visibility.Visible;

            // Wait 4 seconds so the user can read it
            await Task.Delay(4000);

            AchievementPopupVisibility = Visibility.Collapsed;

            // Tiny pause between multiple popups
            await Task.Delay(500);
        }
        // ==========================================
        // NAVIGATION & ROUTING
        // ==========================================
        public void ShowView(string viewName)
        {
            MainMenuVisibility = viewName == "Menu" ? Visibility.Visible : Visibility.Collapsed;
            QuizVisibility = viewName == "Quiz" ? Visibility.Visible : Visibility.Collapsed;
            LearningCardVisibility = viewName == "LearningCard" ? Visibility.Visible : Visibility.Collapsed;
            ResultsVisibility = viewName == "Results" ? Visibility.Visible : Visibility.Collapsed;
            AchievementsVisibility = viewName == "Achievements" ? Visibility.Visible : Visibility.Collapsed;

            RpgHeaderVisibility = (viewName == "Quiz" && _activeEngine is not TrueExamEngine) ? Visibility.Visible : Visibility.Collapsed;
        }

        public void RefreshQuestion() => OnPropertyChanged(nameof(CurrentQuestion));

        [RelayCommand]
        public void JumpToQuestion(int index)
        {
            if (_activeEngine is TrueExamEngine trueExam)
            {
                trueExam.JumpToQuestion(this, index);
            }
        }

        [RelayCommand]
        public async Task ReturnToMenuAsync()
        {
            StopTimer();

            // 1. Save their current stats to the database
            await UserProgress.InitializeFromDatabaseAsync(_db); // <--- 2. Now this is perfectly legal!

            // Only check the long-term "Global" progress when leaving a session
            _ = EvaluateAchievementsAsync();

            PlayerCardVisibility = Visibility.Visible;
            ShowView("Menu");
        }

        [RelayCommand]
        public async Task RouteBackClickAsync()
        {
            if (_activeEngine != null)
            {
                using var db = new AppDbContext();
                await _activeEngine.LoadPreviousAsync(this, db);
            }
        }

        [RelayCommand]
        public async Task OpenAchievementsAsync()
        {
            using var db = new AppDbContext();

            // Load all 25 achievements from the database!
            AchievementsList = await db.Achievements.ToListAsync();

            PlayerCardVisibility = Visibility.Visible;
            ShowView("Achievements");
        }

        [RelayCommand]
        public async Task ReturnToMenuFromResultsAsync() // 1. Added "async Task" and "Async"
        {
            ExamResultsList?.Clear();
            await ReturnToMenuAsync(); // 2. Added "await" and the new method name
        }

        [RelayCommand]
        public async Task RouteNextClickAsync()
        {
            if (_activeEngine != null) await _activeEngine.LoadNextAsync(this, _db);
        }

        // ==========================================
        // ACHIEVEMENT POPUP TRIGGER 
        // ==========================================
        public async Task CheckEventAchievementAsync(string category, string keyword, int value)
        {
            var newlyUnlocked = await _achievementManager.TriggerEventAchievementAsync(category, keyword, value);

            foreach (var ach in newlyUnlocked)
            {
                await ShowAchievementPopupAsync(ach);
            }
        }

        // ==========================================
        // GAME MODE LAUNCHERS
        // ==========================================
        [RelayCommand]
        public async Task StartGlossaryModeAsync()
        {
            _activeEngine = new LearningEngine("GlossaryTerm");
            await _activeEngine.LoadNextAsync(this, _db);
        }

        [RelayCommand]
        public async Task StartSyllabusModeAsync()
        {
            _activeEngine = new LearningEngine("SyllabusConcept");
            await _activeEngine.LoadNextAsync(this, _db);
        }

        [RelayCommand]
        public async Task StartPracticeModeAsync()
        {
            _activeEngine = new PracticeEngine();
            await _activeEngine.LoadNextAsync(this, _db);
        }

        [RelayCommand]
        public async Task StartTrueExamMode()
        {
            _activeEngine = new TrueExamEngine();
            _timeRemainingInSeconds = 60 * 60;

            PlayerCardVisibility = Visibility.Collapsed;
            TimerVisibility = Visibility.Visible;
            StartTimer();

            await _activeEngine.LoadNextAsync(this, _db);
        }

        // ==========================================
        // COMBAT HELPER FOR ENGINES
        // ==========================================
        public void HandleCombat(bool isCorrect)
        {
            RpgStats.HandleCombat(isCorrect, UserProgress);

            // This covers "Combo Breaker" in db
            _ = CheckEventAchievementAsync("Global", "Combo", RpgStats.ComboStreak);

            // This covers "Godlike Streak" in db
            _ = CheckEventAchievementAsync("Global", "Streak", RpgStats.ComboStreak);

            // This checks cumulative practice correct answers for instant popups
            _ = CheckEventAchievementAsync("PracticeExam", "", UserProgress.PassedPracticeQuestions);
        }

        // ==========================================
        // TIMER LOGIC
        // ==========================================
        private void StartTimer()
        {
            _examTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _examTimer.Tick += (s, e) =>
            {
                if (_timeRemainingInSeconds > 0)
                {
                    _timeRemainingInSeconds--;
                    TimeSpan time = TimeSpan.FromSeconds(_timeRemainingInSeconds);
                    TimerText = time.ToString(@"mm\:ss");
                }
                else
                {
                    StopTimer();
                    FeedbackMessage = "TIME IS UP! Please submit your exam.";
                    IsInteractionEnabled = false;
                }
            };
            _examTimer.Start();
        }

        public void StopTimer()
        {
            if (_examTimer != null) { _examTimer.Stop(); }
            TimerVisibility = Visibility.Collapsed;
        }

        // ==========================================
        // QUESTION EVALUATION & PROGRESS
        // ==========================================
        public void SubmitAnswer(string selectedLabel)
        {
            if (_activeEngine != null) _activeEngine.SubmitAnswer(this, _db, selectedLabel);
        }

        [RelayCommand]
        public void RevealAnswer()
        {
            FeedbackMessage = $"Answer: {CurrentQuestion?.CorrectAnswer}";
            IsExplanationVisible = true;
        }

        [RelayCommand]
        public async Task ResetProgressAsync()
        {
            await UserProgress.ResetAllProgressAsync(_db);
        }
    }

    public partial class QuestionNavItem : ObservableObject
    {
        [ObservableProperty] private int _index;
        [ObservableProperty] private string _displayNumber;
        [ObservableProperty] private string _bgColor;
        [ObservableProperty] private string _textColor;
    }
}