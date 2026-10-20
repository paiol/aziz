using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Services.Airtable;

namespace ComparacaoPropostas.Controllers;

/// <summary>Página de estado da integração App → Airtable e botão "Ressincronizar tudo".</summary>
public class AirtableController : Controller
{
    private readonly AppDbContext _db;
    private readonly IAirtableSyncService _sync;
    private readonly AirtableSyncSinal _sinal;
    private readonly IOptionsMonitor<AirtableOptions> _options;

    public AirtableController(AppDbContext db, IAirtableSyncService sync, AirtableSyncSinal sinal, IOptionsMonitor<AirtableOptions> options)
    {
        _db = db;
        _sync = sync;
        _sinal = sinal;
        _options = options;
    }

    public async Task<IActionResult> Index()
    {
        var opcoes = _options.CurrentValue;
        var desde = DateTime.UtcNow.AddDays(-1);

        var vm = new AirtableEstadoViewModel
        {
            Ativo = opcoes.Ativo,
            Enabled = opcoes.Enabled,
            TokenConfigurado = !string.IsNullOrWhiteSpace(opcoes.Token),
            BaseId = opcoes.BaseId,
            Pendentes = await _db.AirtableSyncPendentes.CountAsync(p => p.ProcessadoEm == null && p.Tentativas == 0),
            ComErro = await _db.AirtableSyncPendentes.CountAsync(p => p.ProcessadoEm == null && p.Tentativas > 0),
            Processados24h = await _db.AirtableSyncPendentes.CountAsync(p => p.ProcessadoEm != null && p.ProcessadoEm >= desde),
            UltimoEnvio = await _db.AirtableSyncPendentes.Where(p => p.ProcessadoEm != null).MaxAsync(p => p.ProcessadoEm),
            RegistosLigados = await _db.AirtableLinks.CountAsync(),
            Erros = await _db.AirtableSyncPendentes.AsNoTracking()
                .Where(p => p.ProcessadoEm == null && p.Tentativas > 0)
                .OrderByDescending(p => p.Id)
                .Take(20)
                .ToListAsync()
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResincronizarTudo()
    {
        if (!_options.CurrentValue.Ativo)
        {
            TempData["EmailWarning"] = "A integração com o Airtable não está ativa — configura o token primeiro.";
            return RedirectToAction(nameof(Index));
        }

        var n = await _sync.EnfileirarTudoAsync(HttpContext.RequestAborted);
        _sinal.Sinalizar();
        TempData["Sucesso"] = $"{n} registo(s) postos na fila para envio ao Airtable.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TentarAgora()
    {
        var agora = DateTime.UtcNow;
        await _db.AirtableSyncPendentes
            .Where(p => p.ProcessadoEm == null && p.Tentativas > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ProximaTentativaEm, agora));
        _sinal.Sinalizar();
        TempData["Sucesso"] = "As alterações com erro vão ser tentadas de novo agora.";
        return RedirectToAction(nameof(Index));
    }
}

public class AirtableEstadoViewModel
{
    public bool Ativo { get; set; }
    public bool Enabled { get; set; }
    public bool TokenConfigurado { get; set; }
    public string BaseId { get; set; } = "";
    public int Pendentes { get; set; }
    public int ComErro { get; set; }
    public int Processados24h { get; set; }
    public DateTime? UltimoEnvio { get; set; }
    public int RegistosLigados { get; set; }
    public List<Models.Entities.AirtableSyncPendente> Erros { get; set; } = new();
}
