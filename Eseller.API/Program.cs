//namespaces
using Eseller.Application;
using Eseller.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
// Infrastructure services (DbContext, future: repositories, JWT, etc.)
builder.Services.AddInfrastructureServices(builder.Configuration);
// Application services (MediatR, FluentValidation, Mapster)
builder.Services.AddApplicationServices();
// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
