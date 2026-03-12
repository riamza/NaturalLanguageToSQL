namespace Application.Interfaces;

public interface IDatabaseSchemaService
{
    Task<string> GetDatabaseSchemaDescriptionAsync();
    Task<List<object>> GetDatabaseSchemaJsonAsync();
}