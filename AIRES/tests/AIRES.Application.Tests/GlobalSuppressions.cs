using System.Diagnostics.CodeAnalysis;

// Allow underscores in test method names for better readability
[assembly: SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", 
    Justification = "Test method names should be descriptive and underscores improve readability", 
    Scope = "module")]

// Allow Test suffix in test class names
[assembly: SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Test classes should end with 'Tests' suffix",
    Scope = "namespaceanddescendants",
    Target = "~N:AIRES.Application.Tests")]

// Allow unused parameters in test methods (for theory data that might not be used in assertions)
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter",
    Justification = "Test methods may have parameters for test data that are not directly used",
    Scope = "namespaceanddescendants",
    Target = "~N:AIRES.Application.Tests")]

// Suppress requirement for ConfigureAwait in test code
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "ConfigureAwait is not needed in test code",
    Scope = "module")]

// Allow async methods without await in tests (for testing synchronous paths)
[assembly: SuppressMessage("AsyncUsage", "CS1998:Async method lacks await",
    Justification = "Test methods may be async to match interface without actually awaiting",
    Scope = "namespaceanddescendants",
    Target = "~N:AIRES.Application.Tests")]

// StyleCop suppressions for test projects
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1633:File should have header",
    Justification = "Test files do not need headers",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:Prefix local calls with this",
    Justification = "Test code can be more readable without 'this' prefix",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.NamingRules", "SA1309:Field names should not begin with underscore",
    Justification = "Common convention for private fields in test classes",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1028:Code should not contain trailing whitespace",
    Justification = "Minor formatting issue in test code",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1518:Use line endings correctly at end of file",
    Justification = "Minor formatting issue in test code",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type",
    Justification = "Test helper classes can be in same file",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1124:Do not use regions",
    Justification = "Regions can help organize test code",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1117:Parameters should be on same line or separate lines",
    Justification = "Flexible parameter formatting in tests",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1116:Split parameters should start on line after declaration",
    Justification = "Flexible parameter formatting in tests",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1200:Using directives should be placed correctly",
    Justification = "Using directives at top of file is standard in test projects",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1210:Using directives should be ordered alphabetically by namespace",
    Justification = "Using ordering is not critical in test files",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1009:Closing parenthesis should not be preceded by a space",
    Justification = "Minor formatting issue in tests",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1111:Closing parenthesis should be on line of last parameter",
    Justification = "Flexible formatting in tests",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1512:Single-line comments should not be followed by blank line",
    Justification = "Flexible comment formatting in tests",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1208:System using directives should be placed before other using directives",
    Justification = "Using ordering is not critical in test files",
    Scope = "module")]

[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1515:Single-line comment should be preceded by blank line",
    Justification = "Flexible comment formatting in tests",
    Scope = "module")]

// Additional suppressions for current test build issues
[assembly: SuppressMessage("Globalization", "CA1304:Specify a culture or use an invariant version", Justification = "Test code", Scope = "namespaceanddescendants", Target = "~N:AIRES.Application.Tests")]
[assembly: SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "Test code", Scope = "namespaceanddescendants", Target = "~N:AIRES.Application.Tests")]
[assembly: SuppressMessage("Design", "CA1852:Seal internal types", Justification = "Test handlers", Scope = "namespaceanddescendants", Target = "~N:AIRES.Application.Tests")]
[assembly: SuppressMessage("AsyncUsage", "xUnit1031:Do not use blocking task operations", Justification = "Test code", Scope = "namespaceanddescendants", Target = "~N:AIRES.Application.Tests")]
[assembly: SuppressMessage("Assertions", "xUnit2012:Do not use boolean assertions", Justification = "Test code", Scope = "namespaceanddescendants", Target = "~N:AIRES.Application.Tests")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1513:Closing brace should be followed by blank line", Justification = "Test code", Scope = "namespaceanddescendants", Target = "~N:AIRES.Application.Tests")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1413:Use trailing comma in multi-line initializers", Justification = "Test code", Scope = "namespaceanddescendants", Target = "~N:AIRES.Application.Tests")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1516:Elements should be separated by blank line", Justification = "Test code", Scope = "namespaceanddescendants", Target = "~N:AIRES.Application.Tests")]