using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services.Stock;
using ComparacaoPropostas.ViewModels.Stock;

namespace ComparacaoPropostas.Controllers;

// Gestão de Stock: materiais comprados pela DEI em armazém, por área.
// Entradas: Packing List Huawei (contrato 1+1_Ano), compras DEI e entradas manuais. Saídas: para projeto/site.
public class StockController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPackingListService _packingList;
    private readonly ILogger<StockController> _logger;

    public StockController(AppDbContext db, IPackingListService packingList, ILogger<StockController> logger)
    {
        _db = db;
        _packingList = packingList;
        _logger = logger;
    }

    // ---- Saldos ------------------------------------------------------------------------------

    public IActionResult Index(AreaDepartamento? area, string? q, bool soComStock = true)
    {
        var materiais = _db.MateriaisStock.Include(m => m.Movimentos).Include(m => m.NumerosSerie).AsNoTracking().ToList();
        var linhas = materiais
            .Select(m => new LinhaSaldoVM
            {
                Material = m,
                PorArea = StockCalculos.SaldoPorArea(m.Movimentos),
                SeriesEmArmazem = m.NumerosSerie.Count(s => s.Estado == EstadoNumeroSerie.EmArmazem && (area == null || s.Area == area))
            })
            .Where(l => string.IsNullOrWhiteSpace(q)
                        || l.Material.Codigo.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || l.Material.Descricao.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || (l.Material.Modelo ?? "").Contains(q, StringComparison.OrdinalIgnoreCase))
            .Where(l => !soComStock || (area == null ? l.Total != 0 : l.PorArea.GetValueOrDefault(area.Value) != 0))
            .OrderBy(l => l.Material.Descricao)
            .ToList();

        var movimentos = materiais.SelectMany(m => m.Movimentos).ToList();
        var inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
        var vm = new StockIndexVM
        {
            Area = area,
            Pesquisa = q,
            SoComStock = soComStock,
            Linhas = linhas,
            NumEntradas = _db.EntradasStock.Count(),
            NumSaidas = movimentos.Count(m => m.Tipo == TipoMovimentoStock.Saida),
            MovimentosPorMes = Enumerable.Range(0, 12).Select(i => inicio.AddMonths(i)).Select(mes => (
                mes.ToString("MM/yy"),
                movimentos.Count(m => m.Tipo == TipoMovimentoStock.Entrada && m.Data.Year == mes.Year && m.Data.Month == mes.Month),
                movimentos.Count(m => m.Tipo == TipoMovimentoStock.Saida && m.Data.Year == mes.Year && m.Data.Month == mes.Month))).ToList(),
            MateriaisPorArea = Enum.GetValues<AreaDepartamento>()
                .Select(a => (a, materiais.Count(m => StockCalculos.Saldo(m.Movimentos, a) > 0)))
                .ToList()
        };
        return View(vm);
    }

    public IActionResult Material(int id)
    {
        var material = _db.MateriaisStock
            .Include(m => m.Movimentos).ThenInclude(mv => mv.Entrada)
            .Include(m => m.Movimentos).ThenInclude(mv => mv.ProjetoPlano)
            .Include(m => m.NumerosSerie).ThenInclude(s => s.MovimentoSaida)
            .AsNoTracking()
            .FirstOrDefault(m => m.Id == id);
        if (material == null) return NotFound();
        return View(material);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditarMaterial(int id, string? modelo, string descricao, string? unidade, string? fabricante, decimal? stockMinimo)
    {
        var m = _db.MateriaisStock.Find(id);
        if (m == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(descricao)) m.Descricao = descricao.Trim();
        m.Modelo = Vazio(modelo);
        m.Unidade = Vazio(unidade);
        m.Fabricante = Vazio(fabricante);
        m.StockMinimo = stockMinimo;
        _db.SaveChanges();
        TempData["Sucesso"] = "Material atualizado.";
        return RedirectToAction(nameof(Material), new { id });
    }

    // ---- Entradas ----------------------------------------------------------------------------

    public IActionResult Entradas(AreaDepartamento? area, string? contrato)
    {
        var entradas = _db.EntradasStock
            .Include(e => e.ProjetoPlano)
            .Include(e => e.Movimentos)
            .Where(e => (area == null || e.Area == area) && (contrato == null || e.Contrato1Mais1 == contrato))
            .OrderByDescending(e => e.Data).ThenByDescending(e => e.Id)
            .AsNoTracking()
            .ToList();

        ViewBag.Area = area;
        ViewBag.Contrato = contrato;
        ViewBag.Contratos = _db.EntradasStock.Where(e => e.Contrato1Mais1 != null).Select(e => e.Contrato1Mais1!).Distinct().OrderByDescending(c => c).ToList();
        ViewBag.ProjetosCapex = CapexProjetosHelper.ListaProjetos(_db, null);
        return View(entradas);
    }

    public IActionResult Entrada(int id)
    {
        var entrada = _db.EntradasStock
            .Include(e => e.ProjetoPlano)
            .Include(e => e.PedidoProposta)
            .Include(e => e.Movimentos).ThenInclude(m => m.Material)
            .AsNoTracking()
            .FirstOrDefault(e => e.Id == id);
        if (entrada == null) return NotFound();

        ViewBag.Series = _db.NumerosSerieStock.Include(s => s.Material).Where(s => s.EntradaStockId == id).AsNoTracking()
            .OrderBy(s => s.Material.Codigo).ThenBy(s => s.Serie).ToList();
        return View(entrada);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarPackingList(IFormFile? ficheiro, AreaDepartamento area, int? projetoPlanoId, string? contrato1Mais1, string? localizacao)
    {
        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Escolha o ficheiro do Packing List (.xls ou .xlsx).";
            return RedirectToAction(nameof(Entradas));
        }

        PackingListImportado pl;
        try
        {
            using var stream = ficheiro.OpenReadStream();
            pl = _packingList.Ler(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler o Packing List {Ficheiro}.", ficheiro.FileName);
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro. Confirme que é o Packing List da Huawei em Excel (.xls ou .xlsx).";
            return RedirectToAction(nameof(Entradas));
        }

        if (pl.Linhas.Count == 0)
        {
            TempData["EmailWarning"] = "O ficheiro não tem linhas de material. " + string.Join(" ", pl.Avisos);
            return RedirectToAction(nameof(Entradas));
        }

        var numero = pl.Numero ?? Path.GetFileNameWithoutExtension(ficheiro.FileName);
        var existente = _db.EntradasStock.FirstOrDefault(e => e.Numero == numero && e.Tipo == TipoEntradaStock.PackingListHuawei);
        if (existente != null)
        {
            TempData["EmailWarning"] = $"O Packing List {numero} já foi importado.";
            return RedirectToAction(nameof(Entrada), new { id = existente.Id });
        }

        if (projetoPlanoId != null)
        {
            var erroProjeto = ValidarProjeto(projetoPlanoId);
            if (erroProjeto != null)
            {
                TempData["EmailWarning"] = erroProjeto;
                return RedirectToAction(nameof(Entradas));
            }
        }

        var entrada = new EntradaStock
        {
            Tipo = TipoEntradaStock.PackingListHuawei,
            Area = area,
            Numero = numero,
            Data = pl.Data ?? DateTime.Today,
            Fornecedor = pl.Fornecedor ?? "Huawei",
            Contrato = pl.Contrato,
            Contrato1Mais1 = Vazio(contrato1Mais1) ?? pl.Contrato1Mais1,
            Fatura = pl.Fatura,
            PurchaseOrder = pl.PurchaseOrder,
            ProjetoFornecedor = Limitar(pl.ProjetoFornecedor, 200),
            ProjetoPlanoId = projetoPlanoId,
            Localizacao = Vazio(localizacao),
            NomeFicheiro = Limitar(Path.GetFileName(ficheiro.FileName), 200)
        };

        var materiais = ObterMateriais(pl.Linhas.Select(l => (l.Codigo, l.Modelo, l.Descricao, l.Unidade)), "Huawei");
        foreach (var l in pl.Linhas)
        {
            entrada.Movimentos.Add(new MovimentoStock
            {
                Material = materiais[l.Codigo],
                Area = area,
                Tipo = TipoMovimentoStock.Entrada,
                Quantidade = l.Quantidade,
                Data = entrada.Data,
                ProjetoPlanoId = projetoPlanoId,
                Caixa = Limitar(l.Caixa, 30),
                Embalagem = Limitar(l.Embalagem, 60),
                Site = Limitar(l.Site, 150)
            });
        }
        _db.EntradasStock.Add(entrada);

        var avisosSeries = AdicionarSeries(entrada, pl.Series.Select(s => (s.Codigo, s.Serie, s.Caixa)), materiais);
        _db.SaveChanges();

        TempData["Sucesso"] = $"Packing List {numero} importado: {pl.Linhas.Count} linha(s), {pl.Series.Count - avisosSeries} nº(s) de série."
            + (entrada.Contrato1Mais1 != null ? $" Contrato {entrada.Contrato1Mais1}." : "")
            + (avisosSeries > 0 ? $" {avisosSeries} nº(s) de série já existiam e foram ignorados." : "");
        return RedirectToAction(nameof(Entrada), new { id = entrada.Id });
    }

    public IActionResult NovaEntrada(TipoEntradaStock tipo = TipoEntradaStock.CompraDei, int? projetoPlanoId = null)
    {
        var vm = new NovaEntradaVM { Entrada = new EntradaStock { Tipo = tipo, ProjetoPlanoId = projetoPlanoId } };
        CarregarListasEntrada(vm.Entrada);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult NovaEntrada(NovaEntradaVM vm)
    {
        var linhas = vm.Linhas.Where(l => !string.IsNullOrWhiteSpace(l.Codigo) || !string.IsNullOrWhiteSpace(l.Descricao)).ToList();
        if (linhas.Count == 0) ModelState.AddModelError("", "Acrescente pelo menos um material.");
        foreach (var (l, i) in linhas.Select((l, i) => (l, i)))
        {
            if (string.IsNullOrWhiteSpace(l.Codigo)) ModelState.AddModelError("", $"Linha {i + 1}: indique o código do material.");
            if (l.Quantidade is null or <= 0) ModelState.AddModelError("", $"Linha {i + 1}: a quantidade tem de ser maior que zero.");
        }
        if (vm.Entrada.ProjetoPlanoId != null && ValidarProjeto(vm.Entrada.ProjetoPlanoId) is { } erro) ModelState.AddModelError("Entrada.ProjetoPlanoId", erro);
        if (vm.Entrada.Tipo == TipoEntradaStock.PackingListHuawei) vm.Entrada.Tipo = TipoEntradaStock.Manual;

        if (!ModelState.IsValid)
        {
            CarregarListasEntrada(vm.Entrada);
            return View(vm);
        }

        var entrada = vm.Entrada;
        var materiais = ObterMateriais(linhas.Select(l => (l.Codigo!.Trim(), (string?)null, string.IsNullOrWhiteSpace(l.Descricao) ? l.Codigo!.Trim() : l.Descricao!.Trim(), Vazio(l.Unidade))), entrada.Fornecedor);
        foreach (var l in linhas)
        {
            entrada.Movimentos.Add(new MovimentoStock
            {
                Material = materiais[l.Codigo!.Trim()],
                Area = entrada.Area,
                Tipo = TipoMovimentoStock.Entrada,
                Quantidade = l.Quantidade!.Value,
                Data = entrada.Data,
                ProjetoPlanoId = entrada.ProjetoPlanoId
            });
        }
        _db.EntradasStock.Add(entrada);
        var series = linhas.SelectMany(l => (l.Series ?? "").Split(new[] { ' ', ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => (l.Codigo!.Trim(), s.Trim(), (string?)null)));
        var ignorados = AdicionarSeries(entrada, series, materiais);
        _db.SaveChanges();

        TempData["Sucesso"] = "Entrada registada." + (ignorados > 0 ? $" {ignorados} nº(s) de série já existiam e foram ignorados." : "");
        return RedirectToAction(nameof(Entrada), new { id = entrada.Id });
    }

    // Apagar uma entrada só se nada dela já saiu (o saldo da área não pode ficar negativo).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ApagarEntrada(int id)
    {
        var entrada = _db.EntradasStock.Include(e => e.Movimentos).FirstOrDefault(e => e.Id == id);
        if (entrada == null) return NotFound();

        var series = _db.NumerosSerieStock.Where(s => s.EntradaStockId == id).ToList();
        if (series.Any(s => s.Estado != EstadoNumeroSerie.EmArmazem))
        {
            TempData["EmailWarning"] = "Não é possível apagar: há equipamentos desta entrada que já saíram do armazém.";
            return RedirectToAction(nameof(Entrada), new { id });
        }

        foreach (var g in entrada.Movimentos.GroupBy(m => m.MaterialStockId))
        {
            var saldo = StockCalculos.Saldo(_db.MovimentosStock.Where(m => m.MaterialStockId == g.Key && m.Area == entrada.Area).ToList());
            if (saldo - g.Sum(StockCalculos.Efeito) < 0)
            {
                TempData["EmailWarning"] = "Não é possível apagar: parte deste material já saiu do armazém (o saldo ficaria negativo).";
                return RedirectToAction(nameof(Entrada), new { id });
            }
        }

        _db.NumerosSerieStock.RemoveRange(series);
        _db.EntradasStock.Remove(entrada);
        _db.SaveChanges();
        TempData["Sucesso"] = $"Entrada {entrada.Numero} apagada.";
        return RedirectToAction(nameof(Entradas));
    }

    // ---- Saídas ------------------------------------------------------------------------------

    public IActionResult Saidas(AreaDepartamento? area, int? projetoPlanoId)
    {
        var saidas = _db.MovimentosStock
            .Include(m => m.Material)
            .Include(m => m.ProjetoPlano)
            .Where(m => m.Tipo == TipoMovimentoStock.Saida && (area == null || m.Area == area) && (projetoPlanoId == null || m.ProjetoPlanoId == projetoPlanoId))
            .OrderByDescending(m => m.Data).ThenByDescending(m => m.Id)
            .AsNoTracking()
            .ToList();

        ViewBag.Area = area;
        ViewBag.ProjetoPlanoId = projetoPlanoId;
        ViewBag.SeriesPorSaida = _db.NumerosSerieStock.Where(s => s.MovimentoSaidaId != null).AsNoTracking().ToList()
            .GroupBy(s => s.MovimentoSaidaId!.Value).ToDictionary(g => g.Key, g => g.Select(s => s.Serie).ToList());
        return View(saidas);
    }

    public IActionResult NovaSaida(int? materialStockId, AreaDepartamento? area, int? projetoPlanoId)
    {
        var vm = new NovaSaidaVM { MaterialStockId = materialStockId, Area = area ?? AreaDepartamento.DepInf, ProjetoPlanoId = projetoPlanoId };
        CarregarListasSaida(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult NovaSaida(NovaSaidaVM vm)
    {
        var material = vm.MaterialStockId is int mid ? _db.MateriaisStock.Find(mid) : null;
        if (material == null) ModelState.AddModelError(nameof(vm.MaterialStockId), "Escolha o material.");
        if (vm.ProjetoPlanoId != null && ValidarProjeto(vm.ProjetoPlanoId) is { } erroProjeto) ModelState.AddModelError(nameof(vm.ProjetoPlanoId), erroProjeto);

        var series = new List<NumeroSerieStock>();
        if (material != null)
        {
            var saldo = StockCalculos.Saldo(_db.MovimentosStock.Where(m => m.MaterialStockId == material.Id && m.Area == vm.Area).ToList());
            var emArmazem = _db.NumerosSerieStock.Where(s => s.MaterialStockId == material.Id && s.Area == vm.Area && s.Estado == EstadoNumeroSerie.EmArmazem).ToList();
            series = emArmazem.Where(s => vm.SeriesEscolhidas.Contains(s.Id)).ToList();
            foreach (var e in StockCalculos.ValidarSaida(vm.Quantidade, saldo, emArmazem.Count, series.Count))
                ModelState.AddModelError(nameof(vm.Quantidade), e);
        }

        if (!ModelState.IsValid)
        {
            CarregarListasSaida(vm);
            return View(vm);
        }

        var movimento = new MovimentoStock
        {
            MaterialStockId = material!.Id,
            Area = vm.Area,
            Tipo = TipoMovimentoStock.Saida,
            Quantidade = vm.Quantidade,
            Data = vm.Data,
            ProjetoPlanoId = vm.ProjetoPlanoId,
            Site = Vazio(vm.Site),
            Destino = Vazio(vm.Destino),
            Responsavel = Vazio(vm.Responsavel),
            Observacoes = Vazio(vm.Observacoes)
        };
        _db.MovimentosStock.Add(movimento);
        foreach (var s in series)
        {
            s.Estado = EstadoNumeroSerie.Saiu;
            s.MovimentoSaida = movimento;
        }
        _db.SaveChanges();

        TempData["Sucesso"] = $"Saída registada: {vm.Quantidade:0.###} {material.Unidade} de {material.Descricao}.";
        return RedirectToAction(nameof(Material), new { id = material.Id });
    }

    // ---- Auxiliares --------------------------------------------------------------------------

    // Devolve (criando se preciso) os materiais do catálogo pelos códigos indicados.
    private Dictionary<string, MaterialStock> ObterMateriais(IEnumerable<(string Codigo, string? Modelo, string Descricao, string? Unidade)> linhas, string? fabricante)
    {
        var lista = linhas.GroupBy(l => l.Codigo, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
        var codigos = lista.Select(l => l.Codigo).ToList();
        var existentes = _db.MateriaisStock.Where(m => codigos.Contains(m.Codigo)).ToList()
            .ToDictionary(m => m.Codigo, StringComparer.OrdinalIgnoreCase);

        foreach (var l in lista.Where(l => !existentes.ContainsKey(l.Codigo)))
        {
            var m = new MaterialStock
            {
                Codigo = Limitar(l.Codigo, 60)!,
                Modelo = Limitar(l.Modelo, 100),
                Descricao = l.Descricao,
                Unidade = Limitar(l.Unidade, 20),
                Fabricante = Limitar(fabricante, 100)
            };
            _db.MateriaisStock.Add(m);
            existentes[l.Codigo] = m;
        }
        return existentes;
    }

    // Acrescenta os números de série; os que já existem para o mesmo material são ignorados (devolve quantos).
    private int AdicionarSeries(EntradaStock entrada, IEnumerable<(string Codigo, string Serie, string? Caixa)> series, Dictionary<string, MaterialStock> materiais)
    {
        var lista = series.Where(s => materiais.ContainsKey(s.Codigo)).ToList();
        var ids = materiais.Values.Where(m => m.Id > 0).Select(m => m.Id).ToList();
        var existentes = _db.NumerosSerieStock.Where(s => ids.Contains(s.MaterialStockId)).Select(s => new { s.MaterialStockId, s.Serie }).ToList()
            .Select(s => (s.MaterialStockId, s.Serie.ToLowerInvariant())).ToHashSet();

        var ignorados = 0;
        var vistos = new HashSet<(string, string)>();
        foreach (var s in lista)
        {
            var material = materiais[s.Codigo];
            if (s.Serie.Length > 80 || !vistos.Add((s.Codigo.ToLowerInvariant(), s.Serie.ToLowerInvariant()))
                || (material.Id > 0 && existentes.Contains((material.Id, s.Serie.ToLowerInvariant()))))
            {
                ignorados++;
                continue;
            }
            _db.NumerosSerieStock.Add(new NumeroSerieStock
            {
                Material = material,
                Serie = s.Serie,
                Area = entrada.Area,
                Caixa = Limitar(s.Caixa, 30),
                Entrada = entrada
            });
        }
        return ignorados;
    }

    private string? ValidarProjeto(int? projetoPlanoId)
    {
        var p = _db.ProjetosPlano.AsNoTracking().FirstOrDefault(x => x.Id == projetoPlanoId);
        return p == null || !CapexProjetosHelper.Aceite(p) ? "Só é possível associar a projetos aceites no Capex." : null;
    }

    private void CarregarListasEntrada(EntradaStock entrada)
    {
        ViewBag.ProjetosCapex = CapexProjetosHelper.ListaProjetos(_db, entrada.ProjetoPlanoId);
        ViewBag.Pedidos = _db.Pedidos.OrderByDescending(p => p.CriadoEm).AsNoTracking().ToList()
            .Select(p => new SelectListItem($"{p.TipoProposta} ({p.CriadoEm:dd/MM/yyyy})", p.Id.ToString(), p.Id == entrada.PedidoPropostaId)).ToList();
    }

    private void CarregarListasSaida(NovaSaidaVM vm)
    {
        var materiais = _db.MateriaisStock.Include(m => m.Movimentos).AsNoTracking().ToList()
            .Select(m => (m, Saldo: StockCalculos.Saldo(m.Movimentos, vm.Area)))
            .Where(x => x.Saldo > 0 || x.m.Id == vm.MaterialStockId)
            .OrderBy(x => x.m.Descricao)
            .ToList();
        ViewBag.Materiais = materiais.Select(x => new SelectListItem($"{x.m.Codigo} · {x.m.Descricao} — {x.Saldo:0.###} {x.m.Unidade}", x.m.Id.ToString(), x.m.Id == vm.MaterialStockId)).ToList();
        ViewBag.Saldo = materiais.FirstOrDefault(x => x.m.Id == vm.MaterialStockId).Saldo;
        ViewBag.SeriesDisponiveis = vm.MaterialStockId == null ? new List<NumeroSerieStock>()
            : _db.NumerosSerieStock.Where(s => s.MaterialStockId == vm.MaterialStockId && s.Area == vm.Area && s.Estado == EstadoNumeroSerie.EmArmazem)
                .OrderBy(s => s.Serie).AsNoTracking().ToList();
        ViewBag.ProjetosCapex = CapexProjetosHelper.ListaProjetos(_db, vm.ProjetoPlanoId);
    }

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? Limitar(string? s, int max) => s == null ? null : s.Length > max ? s[..max] : s;
}
