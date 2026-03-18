using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Filters;
using DiabetesPatientApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<ActiveAccountFilter>();
});
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add DbContext
var configuredDatabasePath = builder.Configuration.GetConnectionString("DefaultConnection");
var databasePath = string.IsNullOrWhiteSpace(configuredDatabasePath)
    ? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DiabetesPatientApp.db")
    : Path.IsPathRooted(configuredDatabasePath)
        ? configuredDatabasePath
        : Path.Combine(builder.Environment.ContentRootPath, configuredDatabasePath);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
var sqliteConnectionString = $"Data Source={databasePath}";

builder.Services.AddDbContext<DiabetesDbContext>(options =>
    options.UseSqlite(sqliteConnectionString)
);

// Add Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBloodSugarService, BloodSugarService>();
builder.Services.AddScoped<IWoundService, WoundService>();
builder.Services.AddScoped<IConsultationService, ConsultationService>();
builder.Services.AddScoped<IFootPressureService, FootPressureService>();
builder.Services.AddHttpClient<IFootPressureSuggestionService, FootPressureSuggestionService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IHighRiskAlertService, HighRiskAlertService>();
builder.Services.AddScoped<ICommunityService, CommunityService>();
builder.Services.AddScoped<DataSeedingService>();
builder.Services.AddHttpClient<IReportAnalysisService, ReportAnalysisService>();
builder.Services.AddHttpClient<IWoundImageAnalysisService, WoundImageAnalysisService>();
builder.Services.AddHttpClient<IQuestionnaireGenerationService, QuestionnaireGenerationService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<ActiveAccountFilter>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

// 数据种子：确保有默认管理员
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeedingService>();
    seeder.SeedAsync().GetAwaiter().GetResult();
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();

