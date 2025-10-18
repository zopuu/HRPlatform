using HRPlatform.Data;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;
using HRPlatform.Common.Errors;
using Microsoft.AspNetCore.Diagnostics;
using HRPlatform.Services.Interfaces;
using HRPlatform.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();

// swagger configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ISkillsService, SkillsService>();

builder .Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.UseExceptionHandler(handlerApp => {
    handlerApp.Run(async context => {
        var feature = context.Features.Get<IExceptionHandlerPathFeature>();
        var ex = feature?.Error;

        var problem = ex switch {
            NotFoundException nf => (status: StatusCodes.Status404NotFound, title: "Not Found", detail: nf.Message),
            ConflictException cf => (status: StatusCodes.Status409Conflict, title: "Conflict", detail: cf.Message),
            _ => (status: StatusCodes.Status500InternalServerError, title: "Server Error", detail: "An unexpected error occurred.")
        };

        context.Response.StatusCode = problem.status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new {
            type = "about:blank",
            title = problem.title,
            status = problem.status,
            detail = problem.detail,
            traceId = context.TraceIdentifier
        });
    });
});


app.Run();
