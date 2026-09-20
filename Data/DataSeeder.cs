using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ISTQBEmulator.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ISTQBEmulator.Data
{
    public static class DataSeeder
    {
        public static async Task InitializeAsync(AppDbContext db)
        {
            await db.Database.EnsureCreatedAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // ==========================================
            // 1. UPSERT GLOSSARY TERMS
            // ==========================================
            // Load existing terms into a Dictionary for instant lookup by their Term name
            // The new, safe way that ignores duplicates
            var existingTermsList = await db.GlossaryTerms.ToListAsync();
            var existingTerms = existingTermsList
                .GroupBy(t => t.Term)
                .ToDictionary(g => g.Key, g => g.First());
            var termJsonStrings = ResourceHelper.ReadAllEmbeddedJsonsWithKeyword("Terms_");

            foreach (var jsonString in termJsonStrings)
            {
                try
                {
                    using var doc = JsonDocument.Parse(jsonString);
                    List<JsonTermDto> rawTerms = new();

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        rawTerms = JsonSerializer.Deserialize<List<JsonTermDto>>(jsonString, options) ?? new();
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        var singleItem = JsonSerializer.Deserialize<JsonTermDto>(jsonString, options);
                        if (singleItem != null) rawTerms.Add(singleItem);
                    }

                    foreach (var termItem in rawTerms)
                    {
                        if (existingTerms.TryGetValue(termItem.Term, out var dbTerm))
                        {
                            // UPDATE: Fix typos, update definitions/images. 
                            // Notice we do NOT touch dbTerm.IsPassed!
                            dbTerm.Definition = termItem.Definition;
                            dbTerm.Type = termItem.Type ?? "GlossaryTerm";
                            dbTerm.ImageUrl = termItem.ImageUrl;
                        }
                        else
                        {
                            // INSERT: It's a brand new term
                            db.GlossaryTerms.Add(new GlossaryTerm
                            {
                                Type = termItem.Type ?? "GlossaryTerm",
                                Term = termItem.Term,
                                Definition = termItem.Definition,
                                ImageUrl = termItem.ImageUrl
                            });
                        }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"🔥 ERROR parsing Term: {ex.Message}"); }
            }

            // ==========================================
            // 2. UPSERT QUESTIONS
            // ==========================================
            // Load existing questions. We use the Question 'Text' as the unique identifier.
            var existingQuestionsList = await db.Questions.Include(q => q.Variants).ToListAsync();
            var existingQuestions = existingQuestionsList
                .GroupBy(q => q.Text)
                .ToDictionary(g => g.Key, g => g.First());

            var examJsonStrings = ResourceHelper.ReadAllEmbeddedJsonsWithKeyword("Sample Exam");

            foreach (var jsonString in examJsonStrings)
            {
                try
                {
                    using var doc = JsonDocument.Parse(jsonString);
                    List<JsonQuestionDto> rawData = new();

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        rawData = JsonSerializer.Deserialize<List<JsonQuestionDto>>(jsonString, options) ?? new();
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        var singleItem = JsonSerializer.Deserialize<JsonQuestionDto>(jsonString, options);
                        if (singleItem != null) rawData.Add(singleItem);
                    }

                    foreach (var item in rawData)
                    {
                        string qText = item.Question ?? item.Term ?? item.Text ?? "Missing Question Text";

                        if (existingQuestions.TryGetValue(qText, out var dbQuestion))
                        {
                            // UPDATE: Update explanations, correct answers, or images.
                            // Do NOT touch dbQuestion.IsPassed!
                            dbQuestion.Source = item.ID ?? "Embedded Data";
                            dbQuestion.SelectionType = item.SelectionType ?? "Select ONE option.";
                            dbQuestion.CorrectAnswer = item.Answer ?? item.CorrectAnswer ?? "A";
                            dbQuestion.Explanation = item.Explanation ?? item.Definition ?? "No explanation.";
                            dbQuestion.ImageName = item.ImageName;
                            dbQuestion.ExplanationImageName = item.ExplanationImageName;

                            // Sync Variants safely
                            if (item.Variants != null)
                            {
                                // Remove old variants that no longer exist in JSON
                                var jsonLabels = item.Variants.Select(v => v.Label).ToList();
                                dbQuestion.Variants.RemoveAll(v => !jsonLabels.Contains(v.Label));

                                foreach (var jsonVariant in item.Variants)
                                {
                                    var dbVariant = dbQuestion.Variants.FirstOrDefault(v => v.Label == jsonVariant.Label);
                                    if (dbVariant != null)
                                    {
                                        // Update existing variant (e.g. fixed a typo in option C)
                                        dbVariant.Text = jsonVariant.Text;
                                        dbVariant.ImageName = jsonVariant.ImageName;
                                    }
                                    else
                                    {
                                        // Add new variant
                                        dbQuestion.Variants.Add(new AnswerVariant
                                        {
                                            Label = jsonVariant.Label,
                                            Text = jsonVariant.Text,
                                            ImageName = jsonVariant.ImageName
                                        });
                                    }
                                }
                            }
                        }
                        else
                        {
                            // INSERT: Brand new question
                            db.Questions.Add(new Question
                            {
                                Source = item.ID ?? "Embedded Data",
                                Text = qText,
                                SelectionType = item.SelectionType ?? "Select ONE option.",
                                CorrectAnswer = item.Answer ?? item.CorrectAnswer ?? "A",
                                Explanation = item.Explanation ?? item.Definition ?? "No explanation.",
                                ImageName = item.ImageName,
                                ExplanationImageName = item.ExplanationImageName,
                                Variants = item.Variants?.Select(v => new AnswerVariant
                                {
                                    Label = v.Label,
                                    Text = v.Text,
                                    ImageName = v.ImageName
                                }).ToList() ?? new List<AnswerVariant>()
                            });
                        }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"🔥 ERROR parsing Exam: {ex.Message}"); }
            }

            // ==========================================
            // 3. UPSERT ACHIEVEMENTS 🏆
            // ==========================================
            ISTQBEmulator.Services.AppLogger.Log("Fetching Achievements from DB...");
            var existingAchievements = await db.Achievements.ToDictionaryAsync(a => a.Id);

            ISTQBEmulator.Services.AppLogger.Log("Reading Achievements_seed.json...");
            string jsonAch = ResourceHelper.ReadEmbeddedJson("Achievements_seed.json");

            if (string.IsNullOrEmpty(jsonAch))
            {
                ISTQBEmulator.Services.AppLogger.Log("❌ ERROR: jsonAch is empty! Check if the file's Build Action is set to 'Embedded Resource'.");
            }
            else
            {
                ISTQBEmulator.Services.AppLogger.Log($"Success: Found JSON string. Length: {jsonAch.Length}");
                try
                {
                    var achievements = JsonSerializer.Deserialize<List<Achievement>>(jsonAch, options);
                    if (achievements != null)
                    {
                        ISTQBEmulator.Services.AppLogger.Log($"Deserialized {achievements.Count} achievements. Updating DB...");
                        foreach (var ach in achievements)
                        {
                            if (existingAchievements.TryGetValue(ach.Id, out var dbAch))
                            {
                                // UPDATE: In case you change descriptions or targets in the future
                                // Do NOT touch dbAch.IsUnlocked or dbAch.CurrentProgress!
                                dbAch.Category = ach.Category;
                                dbAch.Icon = ach.Icon;
                                dbAch.Title = ach.Title;
                                dbAch.Description = ach.Description;
                                dbAch.TargetProgress = ach.TargetProgress;
                            }
                            else
                            {
                                // INSERT: You added a 26th achievement!
                                db.Achievements.Add(ach);
                            }
                        }
                        ISTQBEmulator.Services.AppLogger.Log("Finished processing achievements loop.");
                    }
                }
                catch (Exception ex)
                {
                    ISTQBEmulator.Services.AppLogger.Log($"❌ JSON PARSE ERROR in Achievements: {ex.Message}");
                }
            }

            // Save all updates and inserts in one powerful transaction
            await db.SaveChangesAsync();
        }

        // --- DTOs remain exactly the same ---
        public class JsonQuestionDto
        {
            public string ID { get; set; }
            public string Question { get; set; }
            public string Term { get; set; }
            public string Text { get; set; }
            public string SelectionType { get; set; }
            public string ImageName { get; set; }
            public string ExplanationImageName { get; set; }
            public List<JsonVariantDto> Variants { get; set; } = new List<JsonVariantDto>();
            public string Answer { get; set; }
            public string CorrectAnswer { get; set; }
            public string Explanation { get; set; }
            public string Definition { get; set; }
        }

        public class JsonVariantDto
        {
            public string Label { get; set; }
            public string Text { get; set; }
            public string ImageName { get; set; }
        }

        public class JsonTermDto
        {
            public string Type { get; set; }
            public string Term { get; set; }
            public string Definition { get; set; }
            public string ImageUrl { get; set; }
        }
    }
}