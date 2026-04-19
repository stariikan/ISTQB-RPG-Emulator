using ISTQBEmulator.Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace ISTQBEmulator.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Question> Questions { get; set; }
        public DbSet<GlossaryTerm> GlossaryTerms { get; set; }

        // 🔥 NEW: Tells the database to create an Achievements table!
        public DbSet<Achievement> Achievements { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // 1. Use the standard .NET LocalAppData folder
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // 2. Create a dedicated folder for your app
            string appFolder = Path.Combine(localAppData, "ISTQBEmulator");
            Directory.CreateDirectory(appFolder); // Ensure the folder exists!

            // 3. Set the DB file path
            string dbPath = Path.Combine(appFolder, "istqb_master.db");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}