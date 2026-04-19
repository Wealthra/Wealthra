using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Wealthra.Application.Common.Behaviours;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Services;

namespace Wealthra.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
        {
            services.AddAutoMapper(Assembly.GetExecutingAssembly());
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            services.AddMediatR(cfg => {
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

                // Register Pipeline Behaviors (Order matters!)
                // 2. Validation (Throws exception if invalid)
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AdminActivityBehaviour<,>));
            });

            services.AddScoped<IHeuristicRecommendationService, HeuristicRecommendationService>();

            return services;
        }
    }
}