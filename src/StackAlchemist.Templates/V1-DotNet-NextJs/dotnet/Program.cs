using Serilog;
using {{ProjectName}}.Infrastructure;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    // Database
    builder.Services.AddSingleton<IDbConnectionFactory>(
        new NpgsqlConnectionFactory(builder.Configuration.GetConnectionString("DefaultConnection")!));

    // Repositories
    {{!-- LLM_INJECTION_START: RepositoryRegistrations --}}
    {{!-- LLM_INJECTION_END: RepositoryRegistrations --}}

    builder.Services.AddOpenApi();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseCors();
    app.UseSerilogRequestLogging();

    // API Routes
    {{!-- LLM_INJECTION_START: RouteRegistrations --}}
    {{!-- LLM_INJECTION_END: RouteRegistrations --}}

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
