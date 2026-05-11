namespace Application.ExceptionMiddleware
{
    public class ApiExceptionResponse : Exception
    {
        public int StatusCode { get; }

        public ApiExceptionResponse(string message, int statusCode = 400)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
