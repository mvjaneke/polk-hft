using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient();
builder.Services.AddScoped<YocoCheckoutService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ApplicationPdfService>();
builder.Services.AddScoped<ScorecardPdfService>();
builder.Services.AddScoped<SahftaMembersClient>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var context = services.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate(); // Apply pending migrations

    SeedData.Initialize(services);

    // Seed default SAHFTA API base URL if missing
    if (!context.SiteSettings.Any(s => s.Key == "Sahfta:ApiBaseUrl"))
    {
        context.SiteSettings.Add(new SiteSettings { Key = "Sahfta:ApiBaseUrl", Value = "https://braaifever.co.za" });
        context.SaveChanges();
    }
}

app.UseSession();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapControllers();

app.Run();
