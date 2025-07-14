using AIRES.Core.Domain.ValueObjects;
using AIRES.Foundation.Results;

namespace AIRES.Application.Interfaces;

/// <summary>
/// Interface for the service responsible for persisting research booklets to storage.
/// </summary>
public interface IBookletPersistenceService
{
    /// <summary>
    /// Saves a research booklet to the file system.
    /// </summary>
    Task<AIRESResult<string>> SaveBookletAsync(
        ResearchBooklet booklet,
        string suggestedPath,
        CancellationToken cancellationToken = default);
}