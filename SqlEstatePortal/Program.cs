using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AssessmentOptions>(builder.Configuration.GetSection("Assessment"));
builder.Services.Configure<InventorySyncOptions>(builder.Configuration.GetSection("InventorySync"));
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueCountLimit = int.MaxValue;
    options.ValueLengthLimit = int.MaxValue;
    options.KeyLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = long.MaxValue;
});
builder.Services.PostConfigure<AssessmentOptions>(options =>
{
    string Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, path));
    }

    options.ScriptPath = Resolve(options.ScriptPath);
    options.WorkingDirectory = Resolve(options.WorkingDirectory);
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<AssessmentRunnerService>();
builder.Services.AddScoped<ServerReachabilityService>();
builder.Services.AddScoped<InventorySyncService>();
builder.Services.AddScoped<AssessmentCompareService>();
builder.Services.AddScoped<ServerQaCompareService>();
builder.Services.AddScoped<FindingOverrideService>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await AssessmentSchema.ApplyAsync(db);
    await CtInventorySchema.ApplyAsync(db);
    await DbSeeder.SeedAsync(db, app.Environment);

    // Remove auto-imported sample assessment data (never re-seed into the grid).
    await db.Database.ExecuteSqlRawAsync(
        """
        DELETE FROM AssessmentRuns
        WHERE TriggeredBy = N'html-report'
           OR ReportJsonPath LIKE N'%sql-estate-20260824-182501.json';
        """);

    // Re-rank findings against the current register. This runs after the sample
    // data is deleted so it does not waste work on rows about to go, and after
    // CtInventorySchema so ct_servers.environment is guaranteed to exist.
    await SeverityRerankPass.ApplyAsync(
        db, scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeverityPolicy"));
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
