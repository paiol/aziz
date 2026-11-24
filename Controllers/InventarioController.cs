using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services.Stock;

namespace ComparacaoPropostas.Controllers;

// Inventário do armazém (contagem física por área). Enquanto está aberto, as contagens podem vir
// do Excel do armazém ou ser escritas na plataforma; ao fechar, o stock da área passa a ser o contado.
public class InventarioController : Controller
{
    private readonly AppDbContext _db;
    private readonly IInventarioExcelService _excel;
    private readonly ILogger<InventarioController> _logger;

    public InventarioController(AppDbContext db, IInventarioExcelService excel, ILogger<InventarioController> logger)
    {
        _db = db;
        _excel = excel;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var inventarios = _db.InventariosStock.Include(i => i.Linhas).OrderByDescending(i => i.Data).ThenByDescending(i => i.Id).AsNoTracking().ToList();
        return View(inventarios);
    }

    public IActionResult Create()
    {
        ViewBag.Armazens = _db.ArmazensStock.OrderBy(a => a.Codigo).AsNoTracking().ToList();
        return View(new InventarioStock());
    }

    // Ficheiro do SAP do armazém com vários depósitos: cria um inventário (aberto) por armazém.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarArmazens(IFormFile? ficheiro, AreaDepartamento area, DateTime? data, string? filtroAreas)
    {
        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Escolha o ficheiro Excel do armazém.";
            return RedirectToAction(nameof(Index));
        }

        List<ArmazemImportado> armazens;
        List<string> avisos;
        try
        {
            using var stream = ficheiro.OpenReadStream();
            armazens = _excel.LerPorArmazem(stream, out avisos, InventarioExcelService.SepararFiltro(filtroAreas));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler o ficheiro do armazém {Ficheiro}.", ficheiro.FileName);
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro. Confirme que é um Excel (.xls ou .xlsx).";
            return RedirectToAction(nameof(Index));
        }
        armazens = armazens.Where(a => a.Linhas.Count > 0).ToList();
        if (armazens.Count == 0)
        {
            TempData["EmailWarning"] = "O ficheiro não tem linhas com depósito (armazém). " + string.Join(" ", avisos);
            return RedirectToAction(nameof(Index));
        }

        var conhecidos = _db.ArmazensStock.ToList().ToDictionary(a => a.Codigo, StringComparer.OrdinalIgnoreCase);
        var criados = new List<string>();
        foreach (var a in armazens)
        {
            if (!conhecidos.TryGetValue(a.Deposito, out var armazem))
            {
                armazem = new ArmazemStock { Codigo = Limitar(a.Deposito, 20)!, Centro = Limitar(a.Centro, 10), Nome = Limitar(a.Nome, 100) };
                _db.ArmazensStock.Add(armazem);
                conhecidos[a.Deposito] = armazem;
            }
            var inventario = new InventarioStock
            {
                Area = area,
                Data = data ?? DateTime.Today,
                Armazem = armazem.Codigo,
                Centro = armazem.Centro,
                Localizacao = Limitar(armazem.Nome, 100),
                Descricao = Limitar($"{armazem.Rotulo}{(string.IsNullOrWhiteSpace(filtroAreas) ? "" : $" ({filtroAreas.Trim()})")}", 200),
                NomeFicheiro = Limitar(Path.GetFileName(ficheiro.FileName), 200)
            };
            _db.InventariosStock.Add(inventario);
            PreencherLinhas(inventario, a.Linhas, out _, out _);
            criados.Add($"{armazem.Rotulo}: {a.Linhas.Count}");
        }
        var nomesDiferentes = _db.ChangeTracker.Entries<DivergenciaNomeMaterial>().Count(e => e.State == EntityState.Added);
        _db.SaveChanges();

        TempData["Sucesso"] = $"Criados {criados.Count} inventário(s), um por armazém — {string.Join("; ", criados)} material(is). Reveja cada um e feche-o para acertar o stock desse armazém. "
            + string.Join(" ", avisos)
            + (nomesDiferentes > 0 ? $" Atenção: {nomesDiferentes} material(is) têm no armazém um nome diferente do catálogo — veja em Alarmes." : "");
        return RedirectToAction(nameof(Index));
    }

    // comStockAtual: começa com uma linha por material com saldo na área (e no armazém, se indicado).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(InventarioStock inventario, bool comStockAtual)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Armazens = _db.ArmazensStock.OrderBy(a => a.Codigo).AsNoTracking().ToList();
            return View(inventario);
        }
        if (inventario.Armazem != null && _db.ArmazensStock.AsNoTracking().FirstOrDefault(a => a.Codigo == inventario.Armazem) is { } arm)
        {
            inventario.Centro = arm.Centro;
            inventario.Localizacao ??= arm.Nome;
        }

        if (comStockAtual)
        {
            foreach (var m in _db.MateriaisStock.Include(x => x.Movimentos).AsNoTracking().ToList()
                         .Where(m => StockCalculos.Saldo(m.Movimentos, inventario.Area, inventario.Armazem) != 0).OrderBy(m => m.Descricao))
            {
                inventario.Linhas.Add(new LinhaInventario { MaterialStockId = m.Id, Codigo = m.Codigo, Descricao = m.Descricao, Unidade = m.Unidade, AreaTecnica = m.AreaTecnica });
            }
        }
        _db.InventariosStock.Add(inventario);
        _db.SaveChanges();

        TempData["Sucesso"] = comStockAtual
            ? $"Inventário criado com {inventario.Linhas.Count} material(is) em stock na área. Preencha as quantidades contadas."
            : "Inventário criado. Importe o Excel da contagem ou acrescente as linhas.";
        return RedirectToAction(nameof(Details), new { id = inventario.Id });
    }

    public IActionResult Details(int id, string? at)
    {
        ViewBag.AreaTecnica = at;
        var inventario = _db.InventariosStock.Include(i => i.Linhas).AsNoTracking().FirstOrDefault(i => i.Id == id);
        if (inventario == null) return NotFound();

        // Saldo atual da plataforma por código (só interessa enquanto está aberto; fechado usa o valor guardado).
        var codigos = inventario.Linhas.Select(l => l.Codigo).ToList();
        ViewBag.Saldos = _db.MateriaisStock.Where(m => codigos.Contains(m.Codigo)).Include(m => m.Movimentos).AsNoTracking().ToList()
            .ToDictionary(m => m.Codigo, m => StockCalculos.Saldo(m.Movimentos, inventario.Area, inventario.Armazem), StringComparer.OrdinalIgnoreCase);
        ViewBag.NomesCatalogo = _db.MateriaisStock.Where(m => codigos.Contains(m.Codigo)).AsNoTracking()
            .ToDictionary(m => m.Codigo, m => m.Descricao, StringComparer.OrdinalIgnoreCase);
        return View(inventario);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Importar(int id, IFormFile? ficheiro, string? filtroAreas)
    {
        var inventario = CarregarAberto(id, out var erro);
        if (inventario == null) return erro!;

        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Escolha o ficheiro Excel do inventário.";
            return RedirectToAction(nameof(Details), new { id });
        }

        List<LinhaInventarioImportada> linhas;
        List<string> avisos;
        try
        {
            using var stream = ficheiro.OpenReadStream();
            linhas = _excel.Ler(stream, out avisos, InventarioExcelService.SepararFiltro(filtroAreas), inventario.Armazem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler o inventário {Ficheiro}.", ficheiro.FileName);
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro. Confirme que é um Excel (.xls ou .xlsx).";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (linhas.Count == 0)
        {
            TempData["EmailWarning"] = "O ficheiro não tem linhas de inventário. " + string.Join(" ", avisos);
            return RedirectToAction(nameof(Details), new { id });
        }
        PreencherLinhas(inventario, linhas, out var novas, out var atualizadas);
        inventario.NomeFicheiro = Limitar(Path.GetFileName(ficheiro.FileName), 200);
        var nomesDiferentes = _db.ChangeTracker.Entries<DivergenciaNomeMaterial>().Count(e => e.State == EntityState.Added);
        _db.SaveChanges();

        TempData["Sucesso"] = $"Inventário importado: {novas} linha(s) nova(s), {atualizadas} atualizada(s)."
            + (avisos.Count > 0 ? " " + string.Join(" ", avisos) : "")
            + (nomesDiferentes > 0 ? $" Atenção: {nomesDiferentes} material(is) têm no armazém um nome diferente do catálogo (mesmo código) — veja em Alarmes." : "");
        return RedirectToAction(nameof(Details), new { id });
    }

    // Junta as linhas lidas do Excel ao inventário (soma por código; o nome do armazém prevalece na linha).
    private void PreencherLinhas(InventarioStock inventario, List<LinhaInventarioImportada> linhas, out int novas, out int atualizadas)
    {

        var codigos = linhas.Select(l => l.Codigo).ToList();
        var materiais = _db.MateriaisStock.Where(m => codigos.Contains(m.Codigo)).ToList().ToDictionary(m => m.Codigo, StringComparer.OrdinalIgnoreCase);
        novas = 0; atualizadas = 0;
        foreach (var l in linhas)
        {
            var existente = inventario.Linhas.FirstOrDefault(x => x.Codigo.Equals(l.Codigo, StringComparison.OrdinalIgnoreCase));
            if (existente == null)
            {
                existente = new LinhaInventario { Codigo = Limitar(l.Codigo, 60)! };
                inventario.Linhas.Add(existente);
                novas++;
            }
            else atualizadas++;

            materiais.TryGetValue(l.Codigo, out var material);
            existente.MaterialStockId = material?.Id ?? existente.MaterialStockId;
            // O nome usado pelo armazém prevalece na linha; se for diferente do catálogo fica um alerta.
            if (!string.IsNullOrWhiteSpace(l.Descricao)) existente.Descricao = l.Descricao.Trim();
            existente.Descricao ??= material?.Descricao;
            if (material != null) NomesMaterial.Registar(_db, material, l.Descricao, OrigemNomeMaterial.Inventario, RefInventario(inventario), inventario: inventario);
            existente.Unidade ??= Limitar(l.Unidade, 20) ?? material?.Unidade;
            if (l.Quantidade != null) existente.QuantidadeContada = l.Quantidade;
            if (!string.IsNullOrWhiteSpace(l.AreaArmazem)) existente.Observacoes = l.AreaArmazem;
            existente.AreaTecnica = l.AreaTecnica ?? existente.AreaTecnica ?? material?.AreaTecnica;
            if (l.Series.Count > 0)
                existente.Series = string.Join(" ", InventarioCalculos.SepararSeries(existente.Series).Concat(l.Series).Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }

    // Grava as quantidades contadas escritas na página (campos contado_<idLinha> e series_<idLinha>).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult GuardarContagem(int id, IFormCollection form)
    {
        var inventario = CarregarAberto(id, out var erro);
        if (inventario == null) return erro!;

        foreach (var l in inventario.Linhas)
        {
            if (form.TryGetValue($"contado_{l.Id}", out var v))
            {
                var texto = v.ToString().Trim();
                l.QuantidadeContada = texto.Length == 0 ? null
                    : decimal.TryParse(texto, System.Globalization.NumberStyles.Number, new System.Globalization.CultureInfo("pt-PT"), out var d) ? d : l.QuantidadeContada;
            }
            if (form.TryGetValue($"series_{l.Id}", out var s)) l.Series = string.IsNullOrWhiteSpace(s) ? null : s.ToString().Trim();
        }
        _db.SaveChanges();
        TempData["Sucesso"] = "Contagem guardada.";
        return RedirectToAction(nameof(Details), new { id, at = string.IsNullOrEmpty(form["at"]) ? null : form["at"].ToString() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AdicionarLinha(int id, string? codigo, string? descricao, string? unidade, decimal? contado)
    {
        var inventario = CarregarAberto(id, out var erro);
        if (inventario == null) return erro!;

        codigo = codigo?.Trim();
        if (string.IsNullOrEmpty(codigo))
        {
            TempData["EmailWarning"] = "Indique o código do material.";
        }
        else if (inventario.Linhas.Any(l => l.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["EmailWarning"] = $"O código {codigo} já está no inventário.";
        }
        else
        {
            var material = _db.MateriaisStock.FirstOrDefault(m => m.Codigo == codigo);
            inventario.Linhas.Add(new LinhaInventario
            {
                Codigo = Limitar(codigo, 60)!,
                MaterialStockId = material?.Id,
                Descricao = string.IsNullOrWhiteSpace(descricao) ? material?.Descricao : descricao.Trim(),
                Unidade = string.IsNullOrWhiteSpace(unidade) ? material?.Unidade : Limitar(unidade.Trim(), 20),
                AreaTecnica = material?.AreaTecnica,
                QuantidadeContada = contado
            });
            if (material != null && !string.IsNullOrWhiteSpace(descricao))
                NomesMaterial.Registar(_db, material, descricao, OrigemNomeMaterial.Inventario, RefInventario(inventario), inventario: inventario);
            _db.SaveChanges();
            TempData["Sucesso"] = $"{codigo} acrescentado.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoverLinha(int id, int linhaId)
    {
        var inventario = CarregarAberto(id, out var erro);
        if (inventario == null) return erro!;
        var linha = inventario.Linhas.FirstOrDefault(l => l.Id == linhaId);
        if (linha != null)
        {
            _db.LinhasInventario.Remove(linha);
            _db.SaveChanges();
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // Fecha o inventário: o saldo da área passa a ser o contado (ajustes) e os nºs de série contados ficam em armazém.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Fechar(int id, bool zerarNaoContados = false)
    {
        var inventario = CarregarAberto(id, out var erro);
        if (inventario == null) return erro!;

        var contadas = inventario.Linhas.Where(l => l.QuantidadeContada != null).ToList();
        if (contadas.Count == 0)
        {
            TempData["EmailWarning"] = "Não há quantidades contadas: preencha ou importe a contagem antes de fechar.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var codigos = contadas.Select(l => l.Codigo).ToList();
        var materiais = _db.MateriaisStock.Include(m => m.Movimentos).Include(m => m.NumerosSerie)
            .Where(m => codigos.Contains(m.Codigo)).ToList().ToDictionary(m => m.Codigo, StringComparer.OrdinalIgnoreCase);

        int ajustes = 0, novosMateriais = 0, novasSeries = 0;
        foreach (var l in contadas)
        {
            if (!materiais.TryGetValue(l.Codigo, out var material))
            {
                material = new MaterialStock { Codigo = l.Codigo, Descricao = l.Descricao ?? l.Codigo, Unidade = l.Unidade, AreaTecnica = l.AreaTecnica };
                _db.MateriaisStock.Add(material);
                materiais[l.Codigo] = material;
                novosMateriais++;
            }
            l.Material = material;
            material.AreaTecnica ??= l.AreaTecnica;

            var saldo = StockCalculos.Saldo(material.Movimentos, inventario.Area, inventario.Armazem);
            l.QuantidadeSistema = saldo;
            if (InventarioCalculos.Ajuste(l.QuantidadeContada, saldo) is decimal ajuste)
            {
                _db.MovimentosStock.Add(new MovimentoStock
                {
                    Material = material,
                    Area = inventario.Area,
                    Tipo = TipoMovimentoStock.Ajuste,
                    Quantidade = ajuste,
                    Data = inventario.Data,
                    Inventario = inventario,
                    Armazem = inventario.Armazem,
                    Observacoes = $"Inventário {inventario.Data:dd/MM/yyyy}{(inventario.Localizacao != null ? " · " + inventario.Localizacao : "")}"
                });
                ajustes++;
            }

            foreach (var serie in InventarioCalculos.SepararSeries(l.Series).Where(s => s.Length <= 80))
            {
                var existente = material.NumerosSerie.FirstOrDefault(s => s.Serie.Equals(serie, StringComparison.OrdinalIgnoreCase));
                if (existente == null)
                {
                    material.NumerosSerie.Add(new NumeroSerieStock { Serie = serie, Area = inventario.Area, Estado = EstadoNumeroSerie.EmArmazem });
                    novasSeries++;
                }
                else if (existente.Estado == EstadoNumeroSerie.EmArmazem && existente.Area != inventario.Area)
                {
                    existente.Area = inventario.Area; // o equipamento foi encontrado nesta área
                }
            }
        }

        // Opcional: o que tinha stock neste armazém e não apareceu na contagem passa a 0 (a contagem é a mais recente).
        var zerados = 0;
        if (zerarNaoContados && inventario.Armazem != null)
        {
            var contados = new HashSet<string>(contadas.Select(l => l.Codigo), StringComparer.OrdinalIgnoreCase);
            foreach (var m in _db.MateriaisStock.Include(x => x.Movimentos).Where(x => x.Movimentos.Any(mv => mv.Armazem == inventario.Armazem)).ToList()
                         .Where(x => !contados.Contains(x.Codigo)))
            {
                var saldo = StockCalculos.Saldo(m.Movimentos, inventario.Area, inventario.Armazem);
                if (saldo == 0) continue;
                _db.MovimentosStock.Add(new MovimentoStock
                {
                    Material = m, Area = inventario.Area, Armazem = inventario.Armazem, Tipo = TipoMovimentoStock.Ajuste, Quantidade = -saldo,
                    Data = inventario.Data, Inventario = inventario,
                    Observacoes = $"Inventário {inventario.Data:dd/MM/yyyy} · {inventario.Armazem}: não contado, passa a 0"
                });
                zerados++;
            }
        }

        inventario.Estado = EstadoInventario.Fechado;
        inventario.FechadoEm = DateTime.UtcNow;
        _db.SaveChanges();

        TempData["Sucesso"] = $"Inventário fechado: {ajustes} ajuste(s) de stock, {novosMateriais} material(is) novo(s), {novasSeries} nº(s) de série registado(s)." + (zerados > 0 ? $" {zerados} material(is) não contados passaram a 0." : "");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var inventario = _db.InventariosStock.Find(id);
        if (inventario == null) return NotFound();
        if (inventario.Estado == EstadoInventario.Fechado)
        {
            TempData["EmailWarning"] = "Um inventário fechado não pode ser apagado (já acertou o stock).";
            return RedirectToAction(nameof(Details), new { id });
        }
        _db.InventariosStock.Remove(inventario);
        _db.SaveChanges();
        TempData["Sucesso"] = "Inventário apagado.";
        return RedirectToAction(nameof(Index));
    }

    private InventarioStock? CarregarAberto(int id, out IActionResult? erro)
    {
        erro = null;
        var inventario = _db.InventariosStock.Include(i => i.Linhas).FirstOrDefault(i => i.Id == id);
        if (inventario == null)
        {
            erro = NotFound();
            return null;
        }
        if (inventario.Estado == EstadoInventario.Fechado)
        {
            TempData["EmailWarning"] = "O inventário já está fechado.";
            erro = RedirectToAction(nameof(Details), new { id });
            return null;
        }
        return inventario;
    }

    private static string? Limitar(string? s, int max) => s == null ? null : s.Length > max ? s[..max] : s;

    private static string RefInventario(InventarioStock i) =>
        $"Inventário #{i.Id} · {i.Area.ToLabel()} · {i.Data:dd/MM/yyyy}";
}
