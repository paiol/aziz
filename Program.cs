using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.Services.Airtable;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Integração App → Airtable: o interceptor põe cada alteração numa fila (tabela
// AirtableSyncPendentes) e o AirtableSyncWorker envia-a em segundo plano. Ver Services/Airtable.
builder.Services.Configure<AirtableOptions>(builder.Configuration.GetSection(AirtableOptions.Secao));
builder.Services.AddSingleton<AirtableSyncSinal>();
builder.Services.AddSingleton<AirtableSyncInterceptor>();
builder.Services.AddHttpClient<IAirtableClient, AirtableClient>(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<IAirtableSyncService, AirtableSyncService>();
builder.Services.AddHostedService<AirtableSyncWorker>();

// Plano Capex → Airtable (base SCOPE): projetos aceites dos planos concluídos. Ver Services/Airtable/Scope.
builder.Services.Configure<ComparacaoPropostas.Services.Airtable.Scope.ScopeOptions>(builder.Configuration.GetSection(ComparacaoPropostas.Services.Airtable.Scope.ScopeOptions.Secao));
builder.Services.AddSingleton<ComparacaoPropostas.Services.Airtable.Scope.ScopePendenteInterceptor>();
builder.Services.AddHttpClient<ComparacaoPropostas.Services.Airtable.Scope.IScopeClient, ComparacaoPropostas.Services.Airtable.Scope.ScopeClient>(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<ComparacaoPropostas.Services.Airtable.Scope.ScopeSyncService>();
builder.Services.AddHostedService<ComparacaoPropostas.Services.Airtable.Scope.ScopeSyncWorker>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
        .AddInterceptors(sp.GetRequiredService<AirtableSyncInterceptor>(),
            sp.GetRequiredService<ComparacaoPropostas.Services.Airtable.Scope.ScopePendenteInterceptor>()));

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.AddScoped<IScoringService, ScoringService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPropostaExcelService, PropostaExcelService>();
builder.Services.AddScoped<IScoringObraService, ScoringObraService>();
builder.Services.AddScoped<IMqtExcelService, MqtExcelService>();
builder.Services.AddScoped<IEmailObraService, EmailObraService>();
builder.Services.AddScoped<IPlanoExcelService, PlanoExcelService>();
builder.Services.AddScoped<ComparacaoPropostas.Services.Stock.IPackingListService, ComparacaoPropostas.Services.Stock.PackingListService>();
builder.Services.AddScoped<ComparacaoPropostas.Services.Stock.IInventarioExcelService, ComparacaoPropostas.Services.Stock.InventarioExcelService>();

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
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
