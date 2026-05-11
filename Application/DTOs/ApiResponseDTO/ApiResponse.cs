using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.ApiResponseDTO
{
    /// <summary>
    /// Standard wrapper for API responses.
    /// </summary>
    public class ApiResponse
    {
        /// <summary>
        /// A message describing the result of the operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the request was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// The payload data returned if the request is successful.
        /// </summary>
        public object? Result { get; set; }

        /// <summary>
        /// Contains error details if the request fails.
        /// </summary>
        public object? Errors { get; set; }

        /// <summary>
        /// Creates a success response.
        /// </summary>
        /// 
        /// <param name="message">Success message (default is "Success").</param>
        /// <param name="result">The data to return.</param>
        /// <returns>An <see cref="ApiResponse"/> with IsSuccess set to true.</returns>
        public static ApiResponse Success(string message = "Success", object? result = null)
            => new()
            {
                Message = message,
                IsSuccess = true,
                Result = result
            };

        /// <summary>
        /// Creates a failed response.
        /// </summary>
        /// 
        /// <param name="message">Error message.</param>
        /// <param name="error">Error details (e.g., validation errors, exceptions).</param>
        /// <returns>An <see cref="ApiResponse"/> with IsSuccess set to false.</returns>
        public static ApiResponse Fail(string message, object? error = null)
            => new()
            {
                Message = message,
                IsSuccess = false,
                Errors = error
            };
    }
}