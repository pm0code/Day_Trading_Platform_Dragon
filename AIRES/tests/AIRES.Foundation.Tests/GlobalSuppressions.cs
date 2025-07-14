using System.Diagnostics.CodeAnalysis;

// Suppress CA1707 for test methods as underscore naming is a common convention in tests
[assembly: SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
    Justification = "Test naming convention uses underscores for readability",
    Scope = "namespaceanddescendants",
    Target = "~N:AIRES.Foundation.Tests")]