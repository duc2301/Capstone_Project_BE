using Application.DTOs.ApiResponseDTO;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Capstone_Project.DataHandler.Validation
{
    public static class ValidationHandler
    {
        public static ApiResponse Handle(ModelStateDictionary modelState)
        {
            var errors = modelState
                .SelectMany(x => x.Value!.Errors.Select(e => new
                {
                    Field = x.Key,
                    Message = e.ErrorMessage,
                }))
                .ToList();

            return ApiResponse.Fail("Validation failed", errors);
        }
    }
}
