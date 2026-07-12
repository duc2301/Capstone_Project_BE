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
                return;
            }

            if (context.Exception is OperationCanceledException || context.Exception is TaskCanceledException)
            {
                context.Result = new ObjectResult(new
                {
                    isSuccess = false,
                    statusCode = 499,
                    message = "Client closed request",
                    data = (object?)null
                })
                {
                    StatusCode = 499
                };
                context.ExceptionHandled = true;
                return;
            }

            var inner = context.Exception.InnerException?.Message;
            context.Result = new ObjectResult(new
            {
                isSuccess = false,
                statusCode = 500,
                message = context.Exception.Message,
                detail = inner,
                data = (object?)null
            })
            {
                StatusCode = 500
            };

            context.ExceptionHandled = true;
        }
    }
}
