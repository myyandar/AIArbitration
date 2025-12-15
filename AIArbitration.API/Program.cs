using StackExchange.Redis;
using System.Threading.RateLimiting;
using UAParser.Extensions;
using UAParser.Interfaces;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddUserAgentParser();

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
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
