namespace AIRES.Application.Exceptions;

/// <summary>
/// Base exception for AI service failures.
/// </summary>
public abstract class AIServiceException : Exception
{
    public string ServiceName { get; }
    public string? ErrorCode { get; }

    protected AIServiceException(string serviceName, string message, string? errorCode = null) 
        : base(message)
    {
        ServiceName = serviceName;
        ErrorCode = errorCode;
    }

    protected AIServiceException(string serviceName, string message, Exception innerException, string? errorCode = null) 
        : base(message, innerException)
    {
        ServiceName = serviceName;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exception thrown when Mistral documentation analysis fails.
/// </summary>
public class MistralAnalysisFailedException : AIServiceException
{
    public MistralAnalysisFailedException(string message, string? errorCode = null) 
        : base("Mistral", message, errorCode) { }

    public MistralAnalysisFailedException(string message, Exception innerException, string? errorCode = null) 
        : base("Mistral", message, innerException, errorCode) { }
}

/// <summary>
/// Exception thrown when DeepSeek context analysis fails.
/// </summary>
public class DeepSeekContextAnalysisException : AIServiceException
{
    public DeepSeekContextAnalysisException(string message, string? errorCode = null) 
        : base("DeepSeek", message, errorCode) { }

    public DeepSeekContextAnalysisException(string message, Exception innerException, string? errorCode = null) 
        : base("DeepSeek", message, innerException, errorCode) { }
}

/// <summary>
/// Exception thrown when CodeGemma pattern validation fails.
/// </summary>
public class CodeGemmaValidationException : AIServiceException
{
    public CodeGemmaValidationException(string message, string? errorCode = null) 
        : base("CodeGemma", message, errorCode) { }

    public CodeGemmaValidationException(string message, Exception innerException, string? errorCode = null) 
        : base("CodeGemma", message, innerException, errorCode) { }
}

/// <summary>
/// Exception thrown when Gemma2 booklet generation fails.
/// </summary>
public class Gemma2GenerationException : AIServiceException
{
    public Gemma2GenerationException(string message, string? errorCode = null) 
        : base("Gemma2", message, errorCode) { }

    public Gemma2GenerationException(string message, Exception innerException, string? errorCode = null) 
        : base("Gemma2", message, innerException, errorCode) { }
}

/// <summary>
/// Exception thrown when error parsing fails.
/// </summary>
public class CompilerErrorParsingException : Exception
{
    public CompilerErrorParsingException(string message) : base(message) { }
    public CompilerErrorParsingException(string message, Exception innerException) : base(message, innerException) { }
}