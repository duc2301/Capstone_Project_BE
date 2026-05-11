using Application.DTOs.ApiResponseDTO;
using Application.ExceptionMiddleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Capstone_Project.DataHandler.Exceptions
{
    public class ExceptionFilter : IExceptionFilter
    {
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
