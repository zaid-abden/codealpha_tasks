using System.Text.Json.Serialization;
using JobBoard.Api.Data;
using JobBoard.Api.Middleware;
using JobBoard.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure controllers with string enum serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var databaseProvider = builder.Configuration.GetValue<string>("DatabaseProvider");

builder.Services.AddDbContext<JobBoardDbContext>(options =>
{
    if (string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var sqliteConnection = builder.Configuration.GetConnectionString("SqliteConnection")
            ?? "DataSource=InMemoryTest;Mode=Memory;Cache=Shared";
        options.UseSqlite(sqliteConnection);
    }
    else
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=JobBoardDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        });
    }
});

// Register domain services
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();

var app = builder.Build();

// Centralized exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Enable Swagger UI in development and test environments
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
