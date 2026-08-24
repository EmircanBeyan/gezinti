using Gezinti.Application.Interfaces;
using Gezinti.Infrastructure.Context;
using Gezinti.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Gezinti.Infrastructure.Providers.OpenStreetMap;
using Gezinti.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<PlaceImportService>();
builder.Services.AddHttpClient<IPlacesProvider, OsmPlacesProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddDbContext<GezintiDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPlaceRepository, EfPlaceRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();