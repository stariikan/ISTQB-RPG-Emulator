using Microsoft.UI.Xaml;

namespace ISTQBEmulator.Core.Models
{
    public class ExamResultItem
    {
        public string QuestionText { get; set; }
        public string UserAnswer { get; set; }
        public string CorrectAnswer { get; set; }
        public string Explanation { get; set; }
        public bool IsCorrect { get; set; }

        // UI Helpers
        public string ColorHex => IsCorrect ? "#FF2E7D32" : "#FFC62828";
        public string Icon => IsCorrect ? "✅ Correct" : "❌ Incorrect";

        // NEW: This tells XAML to only show the explanation if you got it wrong!
        public Visibility ExplanationVisibility => IsCorrect ? Visibility.Collapsed : Visibility.Visible;
    }
}