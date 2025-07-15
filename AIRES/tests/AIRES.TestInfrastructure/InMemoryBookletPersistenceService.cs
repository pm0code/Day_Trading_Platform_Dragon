// <copyright file="InMemoryBookletPersistenceService.cs" company="AIRES Team">
// Copyright (c) AIRES Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AIRES.Application.Interfaces;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;

namespace AIRES.TestInfrastructure;

/// <summary>
/// In-memory implementation of IBookletPersistenceService for testing.
/// This is a REAL implementation (not a mock) that stores booklets in memory.
/// </summary>
public class InMemoryBookletPersistenceService : AIRESServiceBase, IBookletPersistenceService
{
    private readonly ConcurrentDictionary<Guid, StoredBooklet> booklets = new();
    private readonly ConcurrentDictionary<string, Guid> pathToId = new();
    private int saveCount;

    public InMemoryBookletPersistenceService(IAIRESLogger logger)
        : base(logger, nameof(InMemoryBookletPersistenceService))
    {
    }

    public async Task<AIRESResult<string>> SaveBookletAsync(
        ResearchBooklet booklet,
        string suggestedPath,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (booklet == null)
            {
                LogError("Booklet is null");
                LogMethodExit();
                return AIRESResult<string>.Failure("INVALID_INPUT", "Booklet cannot be null");
            }

            if (string.IsNullOrWhiteSpace(suggestedPath))
            {
                LogError("Suggested path is null or empty");
                LogMethodExit();
                return AIRESResult<string>.Failure("INVALID_INPUT", "Suggested path cannot be null or empty");
            }

            // Simulate async operation
            await Task.Delay(10, cancellationToken);

            // Generate a unique path
            var fileName = $"booklet_{booklet.ErrorBatchId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var fullPath = $"{suggestedPath}/{fileName}";

            var storedBooklet = new StoredBooklet
            {
                Booklet = booklet,
                Path = fullPath,
                SavedAt = DateTime.UtcNow,
                OutputDirectory = suggestedPath
            };

            this.booklets[booklet.ErrorBatchId] = storedBooklet;
            this.pathToId[fullPath] = booklet.ErrorBatchId;
            Interlocked.Increment(ref this.saveCount);

            LogInfo($"Booklet saved: {booklet.ErrorBatchId} to {fullPath}");
            LogMethodExit();
            return AIRESResult<string>.Success(fullPath);
        }
        catch (Exception ex)
        {
            LogError("Failed to save booklet", ex);
            LogMethodExit();
            return AIRESResult<string>.Failure("SAVE_ERROR", $"Failed to save booklet: {ex.Message}", ex);
        }
    }

    public async Task<AIRESResult<ResearchBooklet>> LoadBookletAsync(
        string bookletPath,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(bookletPath))
            {
                LogError("Booklet path is null or empty");
                LogMethodExit();
                return AIRESResult<ResearchBooklet>.Failure("INVALID_INPUT", "Booklet path cannot be null or empty");
            }

            // Simulate async operation
            await Task.Delay(10, cancellationToken);

            if (this.pathToId.TryGetValue(bookletPath, out var bookletId) &&
                this.booklets.TryGetValue(bookletId, out var storedBooklet))
            {
                LogInfo($"Booklet loaded: {bookletId} from {bookletPath}");
                LogMethodExit();
                return AIRESResult<ResearchBooklet>.Success(storedBooklet.Booklet);
            }

            LogWarning($"Booklet not found at path: {bookletPath}");
            LogMethodExit();
            return AIRESResult<ResearchBooklet>.Failure("NOT_FOUND", $"Booklet not found at path: {bookletPath}");
        }
        catch (Exception ex)
        {
            LogError("Failed to load booklet", ex);
            LogMethodExit();
            return AIRESResult<ResearchBooklet>.Failure("LOAD_ERROR", $"Failed to load booklet: {ex.Message}", ex);
        }
    }

    public async Task<AIRESResult<List<string>>> ListBookletsAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            // Simulate async operation
            await Task.Delay(10, cancellationToken);

            var bookletPaths = this.booklets.Values
                .Where(b => b.OutputDirectory == directory)
                .Select(b => b.Path)
                .OrderBy(p => p)
                .ToList();

            LogInfo($"Listed {bookletPaths.Count} booklets in directory: {directory}");
            LogMethodExit();
            return AIRESResult<List<string>>.Success(bookletPaths);
        }
        catch (Exception ex)
        {
            LogError("Failed to list booklets", ex);
            LogMethodExit();
            return AIRESResult<List<string>>.Failure("LIST_ERROR", $"Failed to list booklets: {ex.Message}", ex);
        }
    }

    public async Task<AIRESResult<bool>> DeleteBookletAsync(
        string bookletPath,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(bookletPath))
            {
                LogError("Booklet path is null or empty");
                LogMethodExit();
                return AIRESResult<bool>.Failure("INVALID_INPUT", "Booklet path cannot be null or empty");
            }

            // Simulate async operation
            await Task.Delay(10, cancellationToken);

            if (this.pathToId.TryRemove(bookletPath, out var bookletId) &&
                this.booklets.TryRemove(bookletId, out _))
            {
                LogInfo($"Booklet deleted: {bookletId} at {bookletPath}");
                LogMethodExit();
                return AIRESResult<bool>.Success(true);
            }

            LogWarning($"Booklet not found for deletion: {bookletPath}");
            LogMethodExit();
            return AIRESResult<bool>.Failure("NOT_FOUND", $"Booklet not found at path: {bookletPath}");
        }
        catch (Exception ex)
        {
            LogError("Failed to delete booklet", ex);
            LogMethodExit();
            return AIRESResult<bool>.Failure("DELETE_ERROR", $"Failed to delete booklet: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Test helper: Get total number of saves performed.
    /// </summary>
    public int GetSaveCount() => this.saveCount;

    /// <summary>
    /// Test helper: Get all stored booklets.
    /// </summary>
    public IReadOnlyList<StoredBooklet> GetAllBooklets() => this.booklets.Values.ToList();

    /// <summary>
    /// Test helper: Clear all stored booklets.
    /// </summary>
    public void Clear()
    {
        LogMethodEntry();
        this.booklets.Clear();
        this.pathToId.Clear();
        this.saveCount = 0;
        LogInfo("All booklets cleared");
        LogMethodExit();
    }

    /// <summary>
    /// Test helper: Check if a booklet exists by ErrorBatchId.
    /// </summary>
    public bool HasBooklet(Guid errorBatchId) => this.booklets.ContainsKey(errorBatchId);

    /// <summary>
    /// Test helper: Get a booklet by ID.
    /// </summary>
    public StoredBooklet? GetBookletById(Guid bookletId)
    {
        this.booklets.TryGetValue(bookletId, out var booklet);
        return booklet;
    }
}


