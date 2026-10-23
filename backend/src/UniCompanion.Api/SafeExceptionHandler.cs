using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace UniCompanion.Api;

public sealed class SafeExceptionHandler(IProblemDetailsService problems, ILogger<SafeExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // Log only a type and request ID: exceptions may embed provider URLs or database values.
        logger.LogError("Request {RequestId} failed ({ExceptionType})", context.TraceIdentifier, exception.GetType().Name);
        var status = exception is UniCompanion.Core.Study.StudyRequestException requestError ? requestError.Status
            : exception is UniCompanion.Infrastructure.Ai.AiUnavailableException ? 503 : 500;
        if (status == 429) context.Response.Headers.RetryAfter = "60";
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = exception is UniCompanion.Core.Study.StudyRequestException study ? study.Code : status == 503 ? "service_unavailable" : "request_failed",
                Extensions = { ["requestId"] = context.TraceIdentifier }
            }
        });
    }
}
