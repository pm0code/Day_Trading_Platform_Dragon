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
        this.LogMethodEntry();

        try
        {
            if (booklet == null)
            {
                this.LogError("Booklet is null");
                this.LogMethodExit();
                return AIRESResult<string>.Failure("INVALID_INPUT", "Booklet cannot be null");
            }

            if (string.IsNullOrWhiteSpace(suggestedPath))
            {
                this.LogError("Suggested path is null or empty");
                this.LogMethodExit();
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
                OutputDirectory = suggestedPath,
            };

            this.booklets[booklet.ErrorBatchId] = storedBooklet;
            this.pathToId[fullPath] = booklet.ErrorBatchId;
            Interlocked.Increment(ref this.saveCount);

            this.LogInfo($"Booklet saved: {booklet.ErrorBatchId} to {fullPath}");
            this.LogMethodExit();
            return AIRESResult<string>.Success(fullPath);
        }
        catch (Exception ex)
        {
            this.LogError("Failed to save booklet", ex);
            this.LogMethodExit();
            return AIRESResult<string>.Failure("SAVE_ERROR", $"Failed to save booklet: {ex.Message}", ex);
        }
    }

    public async Task<AIRESResult<ResearchBooklet>> LoadBookletAsync(
        string bookletPath,
        CancellationToken cancellationToken = default)
    {
        this.LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(bookletPath))
            {
                this.LogError("Booklet path is null or empty");
                this.LogMethodExit();
                return AIRESResult<ResearchBooklet>.Failure("INVALID_INPUT", "Booklet path cannot be null or empty");
            }

            // Simulate async operation
            await Task.Delay(10, cancellationToken);

            if (this.pathToId.TryGetValue(bookletPath, out var bookletId) &&
                this.booklets.TryGetValue(bookletId, out var storedBooklet))
            {
                this.LogInfo($"Booklet loaded: {bookletId} from {bookletPath}");
                this.LogMethodExit();
                return AIRESResult<ResearchBooklet>.Success(storedBooklet.Booklet);
            }

            this.LogWarning($"Booklet not found at path: {bookletPath}");
            this.LogMethodExit();
            return AIRESResult<ResearchBooklet>.Failure("NOT_FOUND", $"Booklet not found at path: {bookletPath}");
        }
        catch (Exception ex)
        {
            this.LogError("Failed to load booklet", ex);
            this.LogMethodExit();
            return AIRESResult<ResearchBooklet>.Failure("LOAD_ERROR", $"Failed to load booklet: {ex.Message}", ex);
        }
    }

    public async Task<AIRESResult<List<string>>> ListBookletsAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        this.LogMethodEntry();

        try
        {
            // Simulate async operation
            await Task.Delay(10, cancellationToken);

            var bookletPaths = this.booklets.Values
                .Where(b => b.OutputDirectory == directory)
                .Select(b => b.Path)
                .OrderBy(p => p)
                .ToList();

            this.LogInfo($"Listed {bookletPaths.Count} booklets in directory: {directory}");
            this.LogMethodExit();
            return AIRESResult<List<string>>.Success(bookletPaths);
        }
        catch (Exception ex)
        {
            this.LogError("Failed to list booklets", ex);
            this.LogMethodExit();
            return AIRESResult<List<string>>.Failure("LIST_ERROR", $"Failed to list booklets: {ex.Message}", ex);
        }
    }

    public async Task<AIRESResult<bool>> DeleteBookletAsync(
        string bookletPath,
        CancellationToken cancellationToken = default)
    {
        this.LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(bookletPath))
            {
                this.LogError("Booklet path is null or empty");
                this.LogMethodExit();
                return AIRESResult<bool>.Failure("INVALID_INPUT", "Booklet path cannot be null or empty");
            }

            // Simulate async operation
            await Task.Delay(10, cancellationToken);

            if (this.pathToId.TryRemove(bookletPath, out var bookletId) &&
                this.booklets.TryRemove(bookletId, out _))
            {
                this.LogInfo($"Booklet deleted: {bookletId} at {bookletPath}");
                this.LogMethodExit();
                return AIRESResult<bool>.Success(true);
            }

            this.LogWarning($"Booklet not found for deletion: {bookletPath}");
            this.LogMethodExit();
            return AIRESResult<bool>.Failure("NOT_FOUND", $"Booklet not found at path: {bookletPath}");
        }
        catch (Exception ex)
        {
            this.LogError("Failed to delete booklet", ex);
            this.LogMethodExit();
            return AIRESResult<bool>.Failure("DELETE_ERROR", $"Failed to delete booklet: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Test helper: Get total number of saves performed.
    /// </summary>
    /// <returns>The total count of save operations performed.</returns>
    public int GetSaveCount() => this.saveCount;

    /// <summary>
    /// Test helper: Get all stored booklets.
    /// </summary>
    /// <returns>A read-only list of all stored booklets.</returns>
    public IReadOnlyList<StoredBooklet> GetAllBooklets() => this.booklets.Values.ToList();

    /// <summary>
    /// Test helper: Clear all stored booklets.
    /// </summary>
    public void Clear()
    {
        this.LogMethodEntry();
        this.booklets.Clear();
        this.pathToId.Clear();
        this.saveCount = 0;
        this.LogInfo("All booklets cleared");
        this.LogMethodExit();
    }

    /// <summary>
    /// Test helper: Check if a booklet exists by ErrorBatchId.
    /// </summary>
    /// <param name="errorBatchId">The error batch ID to check.</param>
    /// <returns>True if booklet exists, false otherwise.</returns>
    public bool HasBooklet(Guid errorBatchId) => this.booklets.ContainsKey(errorBatchId);

    /// <summary>
    /// Test helper: Get a booklet by ID.
    /// </summary>
    /// <param name="bookletId">The booklet ID to retrieve.</param>
    /// <returns>The stored booklet if found, null otherwise.</returns>
    public StoredBooklet? GetBookletById(Guid bookletId)
    {
        this.booklets.TryGetValue(bookletId, out var booklet);
        return booklet;
    }
}
