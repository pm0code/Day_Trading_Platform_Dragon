// <copyright file="TestScope.cs" company="AIRES Team">
// Copyright (c) AIRES Team. All rights reserved.
// </copyright>

using System;

namespace AIRES.TestInfrastructure;

/// <summary>
/// Simple scope implementation for testing.
/// </summary>
internal sealed class TestScope : IDisposable
{
    private readonly Action onDispose;

    public TestScope(Action onDispose)
    {
        this.onDispose = onDispose;
    }

    public void Dispose()
    {
        this.onDispose?.Invoke();
    }
}
