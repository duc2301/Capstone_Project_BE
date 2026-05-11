using Application.DTOs.ApiResponseDTO;
using Application.ExceptionMidleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Application.DataHandler.Exceptions
{
    /// <summary>
    /// Global exception filter that intercepts custom API exceptions and returns a structured JSON response.
    /// </summary>
    public class ExceptionFilter : IExceptionFilter
    {
        /// <summary>
        /// Invoked when an exception is thrown during the execution of a controller action.
        /// </summary>
        /// <param name="context">The context containing information about the exception and the request.</param>
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is ApiExceptionResponse ex)
            {
                context.Result = new ObjectResult(new
                {
                    isSuccess = false,
                    statusCode = ex.StatusCode,
                    message = ex.Message,
                    data = (object?)null
                })
                {
                    StatusCode = ex.StatusCode
                };

                context.ExceptionHandled = true;
            }
        }
    }
}