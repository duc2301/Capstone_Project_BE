using Capstone_Project.DataHandler.Validation;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Extensions
{
    public static class PresentationExtensions
    {
        public static IServiceCollection AddGlobalValidation(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var response = ValidationHandler.Handle(context.ModelState);
                    return new BadRequestObjectResult(response);
                };
            });

            return services;
        }
    }
}
