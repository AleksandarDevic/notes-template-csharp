using Domain.Notes;

namespace Application.Abstractions.AI;

public interface INoteCategoryService
{
    Task<NoteCategory> GetCategoryAsync(string noteContent);
}
