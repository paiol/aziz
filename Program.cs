using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Services;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.AddScoped<IScoringService, ScoringService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPropostaExcelService, PropostaExcelService>();
builder.Services.AddScoped<IScoringObraService, ScoringObraService>();
builder.Services.AddScoped<IMqtExcelService, MqtExcelService>();
builder.Services.AddScoped<IEmailObraService, EmailObraService>();

var app = builder.Build();

var ptPT = new CultureInfo("pt-PT");
// Proposals are priced in Cabo Verde Escudos, not Euros — override just the
// currency symbol/pattern on the pt-PT culture so every ToString("C") call
// across the app renders as "17 979 097,49 CVE" instead of "€".
ptPT.NumberFormat.CurrencySymbol = "CVE";
ptPT.NumberFormat.CurrencyPositivePattern = 3; // "n $" -> amount, space, symbol
ptPT.NumberFormat.CurrencyNegativePattern = 8; // "-n $"
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(ptPT),
    SupportedCultures = new[] { ptPT },
    SupportedUICultures = new[] { ptPT }
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Processos}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
