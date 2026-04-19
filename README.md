# 🎮 ISTQB RPG Emulator

Level up your software testing knowledge! **ISTQB RPG Emulator** is a gamified WinUI 3 desktop application designed to make studying for the ISTQB Foundation Level certification engaging, rewarding, and fun. 

By combining rigorous study materials (flashcards, practice questions, and true exams) with classic RPG mechanics, you can defeat monsters, build combo streaks, earn XP, and unlock achievements while mastering software testing concepts.

---

## ✨ Features

### ⚔️ Gamified Studying (RPG Mode)
* **Battle 100+ Unique Monsters:** Answer questions correctly to deal damage to enemies like the *Shadow Drake* 🐉 and *Moonlit Banshee* 🧛‍♀️.
* **Combos & XP:** Build your combo streak by getting consecutive correct answers to deal massive damage and earn bonus XP and QA Coins.
* **Dynamic Feedback:** Battle logs keep track of monster evades, hits, and victory messages.

### 📚 Three Distinct Study Modes
1. **Learning Engine:** Segmented flashcards for Glossary Terms and Syllabus Concepts. Every 5 flashcards triggers a mini-quiz to test your retention.
2. **Practice Engine:** Endless mode! Battle monsters while answering random unpassed questions. Includes an **End-Game Review Loop** for continuous play once you've mastered everything.
3. **True Exam Simulator:** A realistic 40-question exam. Navigate freely between questions, track your answered/unanswered questions on a grid, and get a final grade (65% to pass) with a detailed breakdown.

### 🏆 Achievement System
* **Live Popups:** Trophies and achievements unlock seamlessly mid-battle as you reach specific milestones (e.g., passing 50 questions, mastering the glossary).

### 🧠 Smart Memory Engine
* **Backtracking:** Navigate backwards through your current flashcard batch or practice history.
* **Anti-Farming Security:** The engine remembers your previous answers during a session and prevents XP farming on old questions.

### 💾 Offline & Fast
* **SQLite Database:** All progress, levels, and unlocked achievements are saved locally.
* **Auto-Seeding:** Questions, terms, and monster stats are automatically seeded from embedded JSON files on the first launch.

---

## 🛠️ Built With

* **[C# & .NET 8 (or your version)]** * **[WinUI 3 / Windows App SDK]** - For a modern, native Windows desktop UI.
* **[CommunityToolkit.Mvvm]** - For clean, boilerplate-free MVVM architecture and observable properties.
* **[Entity Framework Core (SQLite)]** - For local database management and querying.
* **[XAML]** - For responsive frontend UI design.

---

## 🚀 Getting Started

### Prerequisites
* **Visual Studio 2022** (Version 17.0 or higher)
* **.NET Desktop Development** workload installed.
* **Windows App SDK** installed (Usually included with Visual Studio WinUI templates).

### Installation
Download: https://drive.google.com/file/d/10q0G3RXfYjcgnSt1Woak7gx4_UwuRizb/view?usp=drive_link
Run: ISTQBEmulator.exe
