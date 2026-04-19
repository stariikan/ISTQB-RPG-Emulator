using ISTQBEmulator.Data;
using System.Threading.Tasks;

namespace ISTQBEmulator.ViewModels
{
    public interface IStudyEngine
    {
        Task LoadNextAsync(MainViewModel vm, AppDbContext db);
        void SubmitAnswer(MainViewModel vm, AppDbContext db, string selectedLabel);

        // NEW: Allows going backwards! Default implementation does nothing for other modes.
        Task LoadPreviousAsync(MainViewModel vm, AppDbContext db) { return Task.CompletedTask; }
    }
}