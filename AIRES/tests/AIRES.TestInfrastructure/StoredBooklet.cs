// <copyright file="StoredBooklet.cs" company="AIRES Team">
// Copyright (c) AIRES Team. All rights reserved.
// </copyright>

using System;

using AIRES.Core.Domain.ValueObjects;

namespace AIRES.TestInfrastructure;

/// <summary>
/// Represents a stored booklet with metadata.
/// </summary>
public class StoredBooklet
{
    public ResearchBooklet Booklet { get; set; } = null!;

    public string Path { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } = string.Empty;

    public DateTime SavedAt { get; set; }
}
