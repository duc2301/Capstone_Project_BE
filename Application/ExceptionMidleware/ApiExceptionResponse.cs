namespace Application.ExceptionMidleware
{
    /// <summary>
    /// Represents a custom exception used to return specific HTTP status codes and error messages from the API.
    /// </summary>
    public class ApiExceptionResponse : Exception
    {
        /// <summary>
        /// Gets the HTTP status code associated with this exception.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiExceptionResponse"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="statusCode">The HTTP status code (default is 400 Bad Request).</param>
        public ApiExceptionResponse(string message, int statusCode = 400)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}