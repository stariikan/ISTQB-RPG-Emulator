using System.ComponentModel.DataAnnotations;

namespace ISTQBEmulator.Core.Models
{
    public class GlossaryTerm
    {
        [Key]
        public int Id { get; set; }
        public string? Type { get; set; } // "GlossaryTerm" or "SyllabusConcept"
        public string? Term { get; set; }
        public string? Definition { get; set; }
        public string? ImageUrl { get; set; }

        // NEW: Progress Tracking
        public bool IsPassed { get; set; } = false;
    }
}