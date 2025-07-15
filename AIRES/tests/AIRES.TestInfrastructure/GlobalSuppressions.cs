// <copyright file="GlobalSuppressions.cs" company="AIRES Team">
// Copyright (c) AIRES Team. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

// Temporarily suppress StyleCop issues to get tests running
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1514:Element documentation header should be preceded by blank line", Justification = "Test infrastructure - temporary suppression", Scope = "type", Target = "~T:AIRES.TestInfrastructure.TestConfiguration")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1508:A closing brace should not be preceded by a blank line", Justification = "Test infrastructure - temporary suppression", Scope = "member", Target = "~M:AIRES.TestInfrastructure.TestCompositionRoot.ConfigureTestServices")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1508:A closing brace should not be preceded by a blank line", Justification = "Test infrastructure - temporary suppression", Scope = "type", Target = "~T:AIRES.TestInfrastructure.TestHttpMessageHandler")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1413:Use trailing comma in multi-line initializers", Justification = "Test infrastructure - temporary suppression", Scope = "member", Target = "~M:AIRES.TestInfrastructure.TestCompositionRoot.ConfigureTestServices")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1518:File is required to end with a single newline character", Justification = "Test infrastructure - temporary suppression")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1508:A closing brace should not be preceded by a blank line", Justification = "Test infrastructure - entire file suppression")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1413:Use trailing comma in multi-line initializers", Justification = "Test infrastructure - entire file suppression")]
