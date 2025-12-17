using AIArbitration.API.Middleware;
using AIArbitration.Core;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Services;
using AIArbitration.Infrastructure.ServiceSupport;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Threading.RateLimiting;
using UAParser.Extensions;
using UAParser.Interfaces;
using AIArbitration.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add memory cache
builder.Services.AddMemoryCache();

// Add database context
builder.Services.AddDbContext<AIArbitrationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddUserAgentParser();
builder.Services.AddProviderConfigurations(builder.Configuration);
builder.Services.AddProviderAdapters();
builder.Services.AddHttpClient();

// using StackExchange.Redis;
var redis = ConnectionMultiplexer.Connect("localhost:6379"); // use config from settings
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton<IRateLimiter, RedisRateLimiter>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
// Add exception handling middleware
app.UseAIArbitrationExceptionHandler();

app.MapControllers();

app.Run();
