using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Pgvector.EntityFrameworkCore;
using System.Text;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Infrastructure.Identity.Models;
using Wealthra.Infrastructure.Identity.Services;
using Wealthra.Infrastructure.Persistence;
using Wealthra.Infrastructure.Services;
using System.Security.Claims;
using Wealthra.Infrastructure.Settings;

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
                    b =>
                    {
                        b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                        b.UseVector();
                    })
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
                    ClockSkew = TimeSpan.Zero // Remove default 5 min delay
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && 
                            (path.StartsWithSegments("/hubs/admin") || path.StartsWithSegments("/hubs/notifications")))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                        var claimsPrincipal = context.Principal;
                        if (claimsPrincipal == null)
                        {
                            context.Fail("Unauthorized");
                            return;
                        }

                        var userId = claimsPrincipal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                                    ?? claimsPrincipal.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
                        var stampInToken = claimsPrincipal.FindFirstValue("AspNet.Identity.SecurityStamp");

                        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(stampInToken))
                        {
                            context.Fail("Unauthorized");
                            return;
                        }

                        var user = await userManager.FindByIdAsync(userId);
                        if (user == null /*|| user.SecurityStamp != stampInToken*/)
                        {
                            context.Fail("Unauthorized");
                        }
                    }
                };
            });

            // 4. Caching
            services.AddMemoryCache();
            services.AddDistributedMemoryCache(); 
            services.AddScoped<ICacheService, CacheService>();

            // Currency: Frankfurter HTTP client + chained service (manual rates + provider order)
            services.AddHttpClient<FrankfurterExchangeService>();
            services.AddScoped<FrankfurterExchangeService>();
            services.AddScoped<ICurrencyExchangeService, ChainedCurrencyExchangeService>();
            services.AddScoped<IDisplayCurrencyService, DisplayCurrencyService>();
            services.AddScoped<IMonthlyCategoryMetricsCalculator, MonthlyCategoryMetricsCalculator>();

            // 5. Services
            services.AddTransient<DateTimeService>();
            services.AddTransient<IIdentityService, IdentityService>();
            services.AddTransient<TokenGenerator>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IUsageTrackerService, UsageTrackerService>();
            services.AddScoped<IUsageDailyAggregateService, UsageDailyAggregateService>();
            services.AddScoped<IAdminAuditService, AdminAuditService>();
            services.AddScoped<IAiUsageRecorder, AiUsageRecorderService>();
            services.AddScoped<IRuntimeAppSettings, RuntimeAppSettingsService>();
            services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
            services.AddScoped<IEmailSender, EmailService>();
            services.AddScoped<IEmailTemplateService, EmailTemplateService>();
            services.AddScoped<IReportExportService, ReportExportService>();

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

            services.AddScoped<IGroqModelCatalog, GroqModelCatalog>();

            if (!string.IsNullOrWhiteSpace(configuration["Groq:ApiKey"]))
            {
                services.AddScoped<IExpenseExtractionEnrichmentService, GroqExpenseExtractionEnrichmentService>();
            }
            else
            {
                services.AddScoped<IExpenseExtractionEnrichmentService, NullExpenseExtractionEnrichmentService>();
            }

            services.AddScoped<IRecommendationFeatureFlags, RecommendationFeatureFlags>();
            services.AddScoped<ICollaborativeRecommendationService, CollaborativeRecommendationService>();
            services.AddScoped<ITextEmbeddingService, DeterministicTextEmbeddingService>();
            services.AddScoped<ISemanticTipRecommendationService, SemanticTipRecommendationService>();

            return services;
        }
    }
}