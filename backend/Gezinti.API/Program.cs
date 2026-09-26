using Gezinti.Application.Interfaces;
using Gezinti.Infrastructure.Context;
using Gezinti.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Gezinti.Infrastructure.Providers.OpenStreetMap;
using Gezinti.Application.Services;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddScoped<PlaceImportService>();
builder.Services.AddHttpClient("Nominatim", client =>
{
    client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Gezinti/1.0 (+https://github.com/EmircanBeyan/gezinti)");
});
builder.Services.AddHttpClient<IPlacesProvider, OsmPlacesProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
});

builder.Services.AddDbContext<GezintiDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.UseNetTopologySuite();
        }));

builder.Services.AddScoped<IPlaceRepository, EfPlaceRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.MapControllers();

app.Run();
