using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Infrastructure.Identity.Models;
using Wealthra.Infrastructure.Identity.Services;
using Wealthra.Infrastructure.Persistence;
using Wealthra.Infrastructure.Services;

namespace Wealthra.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureLayer(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Database
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

            services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

            // 2. Identity
            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            // 3. JWT Authentication
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JwtSettings:Secret"]!)),
                    ValidateIssuer = true,
                    ValidIssuer = configuration["JwtSettings:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["JwtSettings:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token))
                        {
                            context.Token = context.Request.Cookies["access-token"];
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            services.AddAuthorization();

            // Antiforgery — protect mutating endpoints from CSRF.
            services.AddAntiforgery(options =>
            {
                options.HeaderName  = "X-XSRF-TOKEN"; // header the SPA must send
                options.Cookie.Name = "XSRF-TOKEN";   // readable (non-HttpOnly) cookie
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
                options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            });

            // 4. Caching
            services.AddDistributedMemoryCache(); 
            services.AddScoped<ICacheService, CacheService>();

            // Currency Exchange Service
            services.AddHttpClient<ICurrencyExchangeService, FrankfurterExchangeService>();

            // 5. Services
            services.AddTransient<DateTimeService>();
            services.AddTransient<IIdentityService, IdentityService>();
            services.AddTransient<TokenGenerator>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IUsageTrackerService, UsageTrackerService>();
            services.AddHttpContextAccessor();

            // 6. Expense extraction gateways
            services.AddScoped<IExpenseExtractionService, ExpenseExtractionService>();
            services.AddScoped<IOcrService, RemoteOcrService>();
            services.AddHttpClient("OcrClient", client =>
            {
                client.BaseAddress = new Uri(configuration["ExtractionServices:OcrBaseUrl"]!);
                client.Timeout = TimeSpan.FromSeconds(configuration.GetValue<int?>("ExtractionServices:TimeoutSeconds") ?? 60);
            });
            services.AddHttpClient("SttClient", client =>
            {
                client.BaseAddress = new Uri(configuration["ExtractionServices:SttBaseUrl"]!);
                client.Timeout = TimeSpan.FromSeconds(configuration.GetValue<int?>("ExtractionServices:TimeoutSeconds") ?? 60);
            });

            services.AddScoped<ICopilotService, CopilotService>();
            services.AddHttpClient("CopilotClient", client =>
            {
                client.BaseAddress = new Uri(configuration["ExtractionServices:CopilotBaseUrl"]!);
                client.Timeout = TimeSpan.FromSeconds(configuration.GetValue<int?>("ExtractionServices:TimeoutSeconds") ?? 120);
            });

            services.AddHttpClient("GroqClient", client =>
            {
                client.BaseAddress = new Uri("https://api.groq.com/");
                client.Timeout = TimeSpan.FromSeconds(configuration.GetValue<int?>("Groq:TimeoutSeconds") ?? 120);
            });

            if (!string.IsNullOrWhiteSpace(configuration["Groq:ApiKey"]))
            {
                services.AddScoped<IExpenseExtractionEnrichmentService, GroqExpenseExtractionEnrichmentService>();
            }
            else
            {
                services.AddScoped<IExpenseExtractionEnrichmentService, NullExpenseExtractionEnrichmentService>();
            }

            return services;
        }
    }
}