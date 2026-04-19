using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ISTQBEmulator.Core.Models
{
    public class Achievement
    {
        [Key]
        public int Id { get; set; }

        // e.g., "Glossary", "Syllabus", "PracticeExam", "TrueExam", "Global"
        public string Category { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }

        // Can be an Emoji like "🏆" or a file name like "ach_master.png"
        public string Icon { get; set; }

        // Progress Tracking
        public int CurrentProgress { get; set; } = 0;
        public int TargetProgress { get; set; }

        // State
        public bool IsUnlocked { get; set; } = false;
        public DateTime? UnlockedDate { get; set; }

        // Converts the bool into a 1.0 (Solid) or 0.4 (Transparent) opacity for XAML!
        [NotMapped]
        public double DisplayOpacity => IsUnlocked ? 1.0 : 0.4;
    }
}