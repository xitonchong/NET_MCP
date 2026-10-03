using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;


public static class ValidationErrorResponseHelper
{
    public static ProblemDetails CreateValidationProblemDetails(this ValidationResult failedValidationResult, HttpContext ctx)
    {
        var ProblemDetails = new ProblemDetails
        {
            Title = "Validation error",
            Status = StatusCodes.Status400BadRequest,
            Detail = "One or more validation errors occured", 
            Instance = ctx.Request.Path,
        }; 
        foreach (var (key, value) in failedValidationResult.ToDictionary())
        {
            ProblemDetails.Extensions.Add(key, string.Join('|', value)); 
        }
        return ProblemDetails;
    }
}