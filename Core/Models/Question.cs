using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ISTQBEmulator.Core.Models
{
    public class Question
    {
        [Key]
        public int Id { get; set; }
        public string? Source { get; set; }
        public string? Text { get; set; }
        public string? SelectionType { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? Explanation { get; set; }

        // 🔥 NEW: QUESTION & EXPLANATION IMAGES
        public string? ImageName { get; set; }
        public string? ExplanationImageName { get; set; }

        public bool IsPassed { get; set; } = false;

        public List<AnswerVariant> Variants { get; set; } = new();
    }

    public partial class AnswerVariant : ObservableObject
    {
        [Key]
        public int Id { get; set; }
        public string? Label { get; set; }
        public string? Text { get; set; }
        public int QuestionId { get; set; }

        // 🔥 NEW: VARIANT IMAGE (Saved to Database)
        public string? ImageName { get; set; }

        // --- UI VISUAL PROPERTIES (Ignored by Database) ---
        [property: NotMapped]
        [ObservableProperty]
        private string _backgroundColor = "Transparent";

        [property: NotMapped]
        [ObservableProperty]
        private Microsoft.UI.Xaml.Media.Imaging.BitmapImage? _variantImage;

        [property: NotMapped]
        [ObservableProperty]
        private Microsoft.UI.Xaml.Visibility _variantImageVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
    }
}