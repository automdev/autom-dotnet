using System.Text.Json;

namespace Autom;

/// <summary>Base class for every SDK-defined exception.</summary>
public class AutomException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">Cause.</param>
    public AutomException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>The client configuration is missing or unusable.</summary>
public sealed class AutomConfigurationException : AutomException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Error message.</param>
    public AutomConfigurationException(string message)
        : base(message)
    {
    }
}

/// <summary>A required parameter is missing or a value cannot be represented on the wire.</summary>
public sealed class AutomSerializationException : AutomException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="parameter">The parameter at fault.</param>
    public AutomSerializationException(string message, string? parameter = null)
        : base(message)
    {
        Parameter = parameter;
    }

    /// <summary>The parameter at fault, or null.</summary>
    public string? Parameter { get; }
}

/// <summary>The request failed before a complete HTTP response was received (network error, timeout).</summary>
public sealed class AutomTransportException : AutomException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">Cause.</param>
    public AutomTransportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Autom returned a non-success HTTP status.</summary>
public sealed class AutomApiException : AutomException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="rawResponse">The buffered response.</param>
    /// <param name="responseBody">The parsed JSON body, if any.</param>
    /// <param name="errorMessage">The "error" field of the body, if any.</param>
    public AutomApiException(RawResponse rawResponse, JsonElement? responseBody, string? errorMessage)
        : base(errorMessage ?? $"Autom API request failed with status {rawResponse.StatusCode}.")
    {
        RawResponse = rawResponse;
        ResponseBody = responseBody;
        ErrorMessage = errorMessage;
    }

    /// <summary>HTTP status code.</summary>
    public int StatusCode => RawResponse.StatusCode;

    /// <summary>The "error" (or "message") field of the JSON body, or null.</summary>
    public string? ErrorMessage { get; }

    /// <summary>The parsed JSON body, or null when the body is not JSON.</summary>
    public JsonElement? ResponseBody { get; }

    /// <summary>The buffered response.</summary>
    public RawResponse RawResponse { get; }
}

/// <summary>A successful response could not be decoded as the documented representation.</summary>
public sealed class AutomResponseDecodingException : AutomException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="rawResponse">The buffered response.</param>
    /// <param name="innerException">Cause.</param>
    public AutomResponseDecodingException(string message, RawResponse rawResponse, Exception? innerException = null)
        : base(message, innerException)
    {
        RawResponse = rawResponse;
    }

    /// <summary>The buffered response.</summary>
    public RawResponse RawResponse { get; }
}

/// <summary>An async job did not reach a terminal status before the deadline.</summary>
public sealed class AutomJobTimeoutException : AutomException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="jobId">The job id.</param>
    /// <param name="lastResponse">The last job document received.</param>
    public AutomJobTimeoutException(string jobId, ApiResponse lastResponse)
        : base($"Job {jobId} did not finish before the deadline.")
    {
        JobId = jobId;
        LastResponse = lastResponse;
    }

    /// <summary>The job id.</summary>
    public string JobId { get; }

    /// <summary>The last job document received.</summary>
    public ApiResponse LastResponse { get; }
}
