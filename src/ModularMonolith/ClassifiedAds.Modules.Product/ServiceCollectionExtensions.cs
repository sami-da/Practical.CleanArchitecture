﻿using ClassifiedAds.CrossCuttingConcerns.Csv;
using ClassifiedAds.CrossCuttingConcerns.Html;
using ClassifiedAds.CrossCuttingConcerns.Pdf;
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Product.ConfigurationOptions;
using ClassifiedAds.Modules.Product.Csv;
using ClassifiedAds.Modules.Product.Entities;
using ClassifiedAds.Modules.Product.HostedServices;
using ClassifiedAds.Modules.Product.Html;
using ClassifiedAds.Modules.Product.Pdf;
using ClassifiedAds.Modules.Product.Pdf.DinkToPdf;
using ClassifiedAds.Modules.Product.RateLimiterPolicies;
using ClassifiedAds.Modules.Product.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProductModule(this IServiceCollection services, Action<ProductModuleOptions> configureOptions)
    {
        var settings = new ProductModuleOptions();
        configureOptions(settings);

        services.Configure(configureOptions);

        services.AddDbContext<ProductDbContext>(options => options.UseSqlServer(settings.ConnectionStrings.Default, sql =>
        {
            if (!string.IsNullOrEmpty(settings.ConnectionStrings.MigrationsAssembly))
            {
                sql.MigrationsAssembly(settings.ConnectionStrings.MigrationsAssembly);
            }
        }));

        services
            .AddScoped<IRepository<Product, Guid>, Repository<Product, Guid>>()
            .AddScoped(typeof(IProductRepository), typeof(ProductRepository))
            .AddScoped<IRepository<AuditLogEntry, Guid>, Repository<AuditLogEntry, Guid>>()
            .AddScoped<IRepository<OutboxEvent, Guid>, Repository<OutboxEvent, Guid>>();

        services.AddMessageHandlers(Assembly.GetExecutingAssembly());

        services.AddAuthorizationPolicies(Assembly.GetExecutingAssembly());

        services.AddRateLimiter(options =>
        {
            options.AddPolicy<string, DefaultRateLimiterPolicy>(RateLimiterPolicyNames.DefaultPolicy);
        });

        services.AddScoped<ICsvReader<ImportProductsFromCsv>, ImportProductsFromCsvHandler>();
        services.AddScoped<ICsvWriter<ExportProductsToCsv>, ExportProductsToCsvHandler>();

        services.AddScoped<IHtmlWriter<ExportProductsToHtml>, ExportProductsToHtmlHandler>();

        services.AddScoped<IPdfWriter<ExportProductsToPdf>, ExportProductsToPdfHandler>();

        return services;
    }

    public static IMvcBuilder AddProductModule(this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public static void MigrateProductDb(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<ProductDbContext>().Database.Migrate();
    }

    public static void MigrateProductDb(this IHost app)
    {
        using var serviceScope = app.Services.GetService<IServiceScopeFactory>().CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<ProductDbContext>().Database.Migrate();
    }

    public static IServiceCollection AddHostedServicesProductModule(this IServiceCollection services)
    {
        services.AddMessageBusConsumers(Assembly.GetExecutingAssembly());
        services.AddOutboxEventPublishers(Assembly.GetExecutingAssembly());

        services.AddHostedService<PublishEventWorker>();

        return services;
    }
}
