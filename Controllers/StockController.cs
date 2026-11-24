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

    public IActionResult Index(AreaDepartamento? area, string? q, bool soComStock = true, string? at = null, string? arm = null)
    {
        var materiais = _db.MateriaisStock.Include(m => m.Movimentos).Include(m => m.NumerosSerie).Include(m => m.CodigosFornecedor).AsNoTracking().ToList();
        var linhas = materiais
            .Select(m => new LinhaSaldoVM
            {
                Material = m,
                PorArea = StockCalculos.SaldoPorArea(m.Movimentos.Where(mv => arm == null || string.Equals(mv.Armazem, arm, StringComparison.OrdinalIgnoreCase))),
                SeriesEmArmazem = m.NumerosSerie.Count(s => s.Estado == EstadoNumeroSerie.EmArmazem && (area == null || s.Area == area))
            })
            .Where(l => string.IsNullOrWhiteSpace(q)
                        || l.Material.Codigo.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || l.Material.Descricao.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || (l.Material.Modelo ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                        || l.Material.CodigosFornecedor.Any(c => c.Codigo.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .Where(l => !soComStock || (area == null ? l.Total != 0 : l.PorArea.GetValueOrDefault(area.Value) != 0))
            .Where(l => at == null || (at == "sem" ? l.Material.AreaTecnica == null : l.Material.AreaTecnica == at))
            .OrderBy(l => l.Material.Descricao)
            .ToList();

        var movimentos = materiais.SelectMany(m => m.Movimentos).ToList();
        var inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
        var vm = new StockIndexVM
        {
            Area = area,
            Pesquisa = q,
            AreaTecnica = at,
            Armazem = arm,
            Armazens = _db.ArmazensStock.OrderBy(a => a.Codigo).AsNoTracking().ToList(),
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
            .Include(m => m.DivergenciasNome)
            .Include(m => m.CodigosFornecedor)
            .AsNoTracking()
            .FirstOrDefault(m => m.Id == id);
        if (material == null) return NotFound();
        ViewBag.NomesArmazens = _db.ArmazensStock.AsNoTracking().ToList().ToDictionary(a => a.Codigo, a => a.Rotulo, StringComparer.OrdinalIgnoreCase);
        ViewBag.SugestoesSap = material.CodigoProvisorio
            ? SugestoesSap.Sugerir(material, _db.MateriaisStock.Where(m => !m.CodigoProvisorio).AsNoTracking().ToList(), 5)
            : new List<SugestaoSap>();
        ViewBag.FornecedoresMaterial = _db.Fornecedores.Where(f => f.Ativo).Select(f => f.Nome).ToList()
            .Concat(_db.EntradasStock.Where(e => e.Fornecedor != null).Select(e => e.Fornecedor!).Distinct().ToList())
            .Concat(_db.CodigosFornecedorMaterial.Select(c => c.Fornecedor).Distinct().ToList())
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
        return View(material);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditarMaterial(int id, string? modelo, string descricao, string? unidade, string? fabricante, decimal? stockMinimo,
        decimal? precoUnitario, int? diasParado, string? areaTecnica)
    {
        var m = _db.MateriaisStock.Find(id);
        if (m == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(descricao)) m.Descricao = descricao.Trim();
        m.Modelo = Vazio(modelo);
        m.Unidade = Vazio(unidade);
        m.Fabricante = Vazio(fabricante);
        m.AreaTecnica = Vazio(areaTecnica);
        m.StockMinimo = stockMinimo;
        m.PrecoUnitario = precoUnitario;
        m.DiasParado = diasParado is > 0 ? diasParado : null;
        _db.SaveChanges();
        TempData["Sucesso"] = "Material atualizado.";
        return RedirectToAction(nameof(Material), new { id });
    }

    // ---- Painel e alarmes --------------------------------------------------------------------

    // Página principal da Gestão de Stock: atalhos, indicadores, material escolhido, alarmes e consumos.
    public IActionResult Painel(AreaDepartamento? area, int? materialId)
    {
        var materiais = _db.MateriaisStock
            .Include(m => m.Movimentos).ThenInclude(mv => mv.ProjetoPlano)
            .Include(m => m.DivergenciasNome.Where(d => d.Estado == EstadoDivergenciaNome.Pendente))
            .AsNoTracking().ToList();
        var movs = materiais.SelectMany(m => m.Movimentos.Select(mv => (m, mv))).Where(x => area == null || x.mv.Area == area).ToList();
        decimal Valor(MaterialStock m, decimal q) => (m.PrecoUnitario ?? 0m) * q;

        var comMovimento = materiais.Where(m => m.Movimentos.Any(mv => area == null || mv.Area == area)).OrderBy(m => m.Descricao).ToList();
        var material = comMovimento.FirstOrDefault(m => m.Id == materialId)
                       ?? comMovimento.OrderByDescending(m => m.Movimentos.Count).FirstOrDefault();
        var movsMaterial = material?.Movimentos.Where(mv => area == null || mv.Area == area).ToList() ?? new();

        var inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
        var vm = new PainelStockVM
        {
            Area = area,
            NumMateriaisComStock = comMovimento.Count(m => StockCalculos.Saldo(m.Movimentos.Where(mv => area == null || mv.Area == area)) > 0),
            NumEntradas = movs.Count(x => x.mv.Tipo == TipoMovimentoStock.Entrada),
            NumSaidas = movs.Count(x => x.mv.Tipo == TipoMovimentoStock.Saida),
            ValorEntradas = movs.Where(x => x.mv.Tipo == TipoMovimentoStock.Entrada).Sum(x => Valor(x.m, x.mv.Quantidade)),
            ValorSaidas = movs.Where(x => x.mv.Tipo == TipoMovimentoStock.Saida).Sum(x => Valor(x.m, x.mv.Quantidade)),
            ValorStock = comMovimento.Sum(m => Valor(m, StockCalculos.Saldo(m.Movimentos.Where(mv => area == null || mv.Area == area)))),
            MateriaisSemPreco = comMovimento.Count(m => m.PrecoUnitario == null),
            Alarmes = StockAlarmes.Calcular(materiais, DateTime.Today, area),
            Materiais = comMovimento,
            Material = material,
            QtdEntradas = movsMaterial.Where(x => x.Tipo == TipoMovimentoStock.Entrada).Sum(x => x.Quantidade),
            QtdSaidas = movsMaterial.Where(x => x.Tipo == TipoMovimentoStock.Saida).Sum(x => x.Quantidade),
            QtdAjustes = movsMaterial.Where(x => x.Tipo == TipoMovimentoStock.Ajuste).Sum(x => x.Quantidade),
            QtdStock = StockCalculos.Saldo(movsMaterial),
            PorArea = Enum.GetValues<AreaDepartamento>().Where(a => area == null || a == area)
                .Select(a => (a.ToLabel(),
                    materiais.Sum(m => Valor(m, StockCalculos.Saldo(m.Movimentos, a))),
                    materiais.Count(m => StockCalculos.Saldo(m.Movimentos, a) > 0)))
                .ToList(),
            ValorPorMes = Enumerable.Range(0, 12).Select(i => inicio.AddMonths(i)).Select(mes => (
                mes.ToString("MM/yy"),
                movs.Where(x => x.mv.Tipo == TipoMovimentoStock.Entrada && x.mv.Data.Year == mes.Year && x.mv.Data.Month == mes.Month).Sum(x => Valor(x.m, x.mv.Quantidade)),
                movs.Where(x => x.mv.Tipo == TipoMovimentoStock.Saida && x.mv.Data.Year == mes.Year && x.mv.Data.Month == mes.Month).Sum(x => Valor(x.m, x.mv.Quantidade))))
                .ToList(),
            ConsumoPorProjeto = movs.Where(x => x.mv.Tipo == TipoMovimentoStock.Saida)
                .GroupBy(x => x.mv.ProjetoPlanoId)
                .Select(g => (g.Key, g.First().mv.ProjetoPlano?.Nome ?? "(sem projeto)", g.Count(), g.Sum(x => Valor(x.m, x.mv.Quantidade))))
                .OrderByDescending(x => x.Item4).ThenByDescending(x => x.Item3)
                .ToList(),
            UltimosMovimentos = movs.Select(x => x.mv).OrderByDescending(mv => mv.Data).ThenByDescending(mv => mv.Id).Take(10).ToList()
        };
        return View(vm);
    }

    public IActionResult Alarmes(AreaDepartamento? area, TipoAlarmeStock? tipo)
    {
        var materiais = _db.MateriaisStock.Include(m => m.Movimentos).Include(m => m.DivergenciasNome.Where(d => d.Estado == EstadoDivergenciaNome.Pendente)).AsNoTracking().ToList();
        var alarmes = StockAlarmes.Calcular(materiais, DateTime.Today, area).Where(a => tipo == null || a.Tipo == tipo).ToList();
        ViewBag.Area = area;
        ViewBag.Tipo = tipo;
        return View(alarmes);
    }

    // ---- Código SAP e códigos de fornecedor ----------------------------------------------------

    // Associa um material provisório (que chegou só com o código do fornecedor) ao código SAP.
    // Se o código SAP já existe no catálogo, os dois juntam-se: movimentos, nºs de série, contagens e
    // códigos de fornecedor passam para o material SAP e o provisório desaparece.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AssociarSap(int id, string? codigoSap, string? voltarPara = null)
    {
        var prov = _db.MateriaisStock.Include(m => m.CodigosFornecedor).Include(m => m.NumerosSerie).Include(m => m.DivergenciasNome)
            .FirstOrDefault(m => m.Id == id);
        if (prov == null) return NotFound();
        codigoSap = codigoSap?.Trim();
        if (string.IsNullOrEmpty(codigoSap))
        {
            TempData["EmailWarning"] = "Indique o código SAP.";
            return RedirectToAction(nameof(Material), new { id });
        }

        // O código atual (do fornecedor) fica registado como código de fornecedor.
        if (prov.CodigoProvisorio && !prov.Codigo.Equals(codigoSap, StringComparison.OrdinalIgnoreCase))
            GarantirCodigoFornecedor(prov, prov.Fabricante, prov.Codigo, prov.Modelo, prov.Descricao);

        var alvo = _db.MateriaisStock.Include(m => m.CodigosFornecedor).Include(m => m.NumerosSerie)
            .FirstOrDefault(m => m.Codigo == codigoSap && m.Id != id);
        if (alvo == null)
        {
            prov.Codigo = Limitar(codigoSap, 60)!;
            prov.CodigoProvisorio = false;
            _db.SaveChanges();
            TempData["Sucesso"] = $"Material passou a ter o código SAP {prov.Codigo}.";
            return Url.IsLocalUrl(voltarPara) ? LocalRedirect(voltarPara!) : RedirectToAction(nameof(Material), new { id });
        }

        using var tx = _db.Database.BeginTransaction();
        _db.MovimentosStock.Where(mv => mv.MaterialStockId == prov.Id).ExecuteUpdate(s => s.SetProperty(mv => mv.MaterialStockId, alvo.Id));
        _db.LinhasInventario.Where(l => l.MaterialStockId == prov.Id).ExecuteUpdate(s => s.SetProperty(l => l.MaterialStockId, alvo.Id));
        foreach (var s in prov.NumerosSerie.ToList())
        {
            if (alvo.NumerosSerie.Any(x => x.Serie.Equals(s.Serie, StringComparison.OrdinalIgnoreCase))) _db.NumerosSerieStock.Remove(s);
            else s.MaterialStockId = alvo.Id;
        }
        foreach (var d in prov.DivergenciasNome.ToList()) d.MaterialStockId = alvo.Id;
        foreach (var c in prov.CodigosFornecedor.ToList())
        {
            if (alvo.CodigosFornecedor.Any(x => x.Fornecedor == c.Fornecedor && x.Codigo == c.Codigo)) _db.CodigosFornecedorMaterial.Remove(c);
            else c.MaterialStockId = alvo.Id;
        }
        alvo.Modelo ??= prov.Modelo;
        alvo.Fabricante ??= prov.Fabricante;
        alvo.Unidade ??= prov.Unidade;
        alvo.AreaTecnica ??= prov.AreaTecnica;
        alvo.PrecoUnitario ??= prov.PrecoUnitario;
        alvo.StockMinimo ??= prov.StockMinimo;
        _db.SaveChanges();
        _db.MateriaisStock.Remove(prov);
        _db.SaveChanges();
        tx.Commit();

        TempData["Sucesso"] = $"{prov.Codigo} associado ao código SAP {alvo.Codigo}: o stock e o histórico passaram para esse material.";
        return Url.IsLocalUrl(voltarPara) ? LocalRedirect(voltarPara!) : RedirectToAction(nameof(Material), new { id = alvo.Id });
    }

    // Materiais que chegaram só com o código do fornecedor e os códigos SAP que podem ser o mesmo.
    public IActionResult CodigosSap()
    {
        var todos = _db.MateriaisStock.Include(m => m.Movimentos).Include(m => m.CodigosFornecedor).AsNoTracking().ToList();
        var sap = todos.Where(m => !m.CodigoProvisorio).ToList();
        var linhas = todos.Where(m => m.CodigoProvisorio)
            .OrderBy(m => m.Descricao)
            .Select(m => (Material: m, Saldo: StockCalculos.Saldo(m.Movimentos), Sugestoes: SugestoesSap.Sugerir(m, sap)))
            .ToList();
        return View(linhas);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AdicionarCodigoFornecedor(int id, string? fornecedor, string? codigo, string? modelo)
    {
        var m = _db.MateriaisStock.Include(x => x.CodigosFornecedor).FirstOrDefault(x => x.Id == id);
        if (m == null) return NotFound();
        fornecedor = Vazio(fornecedor);
        codigo = Vazio(codigo);
        if (fornecedor == null || codigo == null)
        {
            TempData["EmailWarning"] = "Indique o fornecedor e o código do fornecedor.";
            return RedirectToAction(nameof(Material), new { id });
        }
        var outro = _db.CodigosFornecedorMaterial.Include(c => c.Material).AsNoTracking()
            .FirstOrDefault(c => c.Fornecedor == fornecedor && c.Codigo == codigo);
        if (outro != null && outro.MaterialStockId != id)
        {
            TempData["EmailWarning"] = $"O código {codigo} de {fornecedor} já está associado ao material {outro.Material.Codigo}.";
            return RedirectToAction(nameof(Material), new { id });
        }
        GarantirCodigoFornecedor(m, fornecedor, codigo, Vazio(modelo), null);
        _db.SaveChanges();
        TempData["Sucesso"] = $"Código {codigo} de {fornecedor} associado a {m.Codigo}.";
        return RedirectToAction(nameof(Material), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoverCodigoFornecedor(int id, int codigoId)
    {
        var c = _db.CodigosFornecedorMaterial.FirstOrDefault(x => x.Id == codigoId && x.MaterialStockId == id);
        if (c != null)
        {
            _db.CodigosFornecedorMaterial.Remove(c);
            _db.SaveChanges();
            TempData["Sucesso"] = $"Código {c.Codigo} de {c.Fornecedor} removido.";
        }
        return RedirectToAction(nameof(Material), new { id });
    }

    private void GarantirCodigoFornecedor(MaterialStock m, string? fornecedor, string codigo, string? modelo, string? descricao)
    {
        var nomeFornecedor = Limitar(Vazio(fornecedor), 150) ?? "(fornecedor não indicado)";
        if (m.CodigosFornecedor.Any(c => c.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase)
                                         && CodigosFornecedor.MesmoFornecedor(c.Fornecedor, nomeFornecedor))) return;
        m.CodigosFornecedor.Add(new CodigoFornecedorMaterial
        {
            Fornecedor = nomeFornecedor,
            Codigo = Limitar(codigo, 60)!,
            Modelo = Limitar(modelo, 100),
            Descricao = descricao
        });
    }

    // Decide um alerta de "nome diferente": manter o nome do catálogo (o recebido fica conhecido como
    // outro nome do mesmo material) ou passar a usar o nome recebido no catálogo.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ResolverNome(int id, bool usarRecebido, string? voltarPara)
    {
        var d = _db.DivergenciasNomeMaterial.Include(x => x.Material).FirstOrDefault(x => x.Id == id);
        if (d == null) return NotFound();
        if (d.Estado == EstadoDivergenciaNome.Pendente)
        {
            if (usarRecebido)
            {
                d.Material.Descricao = d.NomeRecebido;
                d.Estado = EstadoDivergenciaNome.NomeAtualizado;
            }
            else d.Estado = EstadoDivergenciaNome.MantidoCatalogo;
            d.ResolvidoEm = DateTime.UtcNow;
            _db.SaveChanges();
            TempData["Sucesso"] = usarRecebido
                ? $"{d.Material.Codigo}: o catálogo passa a chamar-lhe \"{d.NomeRecebido}\"."
                : $"{d.Material.Codigo}: mantém-se \"{d.Material.Descricao}\"; \"{d.NomeRecebido}\" fica como outro nome do mesmo material.";
        }
        return Url.IsLocalUrl(voltarPara) ? LocalRedirect(voltarPara!) : RedirectToAction(nameof(Alarmes), new { tipo = TipoAlarmeStock.NomeDiferente });
    }

    // ---- Entradas ----------------------------------------------------------------------------

    // processoId: vindo do Processo de compra ("Registar receção de material"), pré-escolhe a compra.
    public IActionResult Entradas(AreaDepartamento? area, string? contrato, string? fornecedor, int? processoId)
    {
        var entradas = _db.EntradasStock
            .Include(e => e.ProjetoPlano)
            .Include(e => e.PedidoProposta).ThenInclude(p => p!.Processo)
            .Include(e => e.Movimentos)
            .Where(e => (area == null || e.Area == area) && (contrato == null || e.Contrato1Mais1 == contrato)
                        && (fornecedor == null || e.Fornecedor == fornecedor))
            .OrderByDescending(e => e.Data).ThenByDescending(e => e.Id)
            .AsNoTracking()
            .ToList();

        var processo = processoId == null ? null : _db.Processos.Include(p => p.PedidoProposta).AsNoTracking().FirstOrDefault(p => p.Id == processoId);
        ViewBag.Area = area ?? processo?.PedidoProposta.Area;
        ViewBag.Contrato = contrato;
        ViewBag.Fornecedor = fornecedor;
        ViewBag.Processo = processo;
        ViewBag.Contratos = _db.EntradasStock.Where(e => e.Contrato1Mais1 != null).Select(e => e.Contrato1Mais1!).Distinct().OrderByDescending(c => c).ToList();
        ViewBag.FornecedoresEntradas = _db.EntradasStock.Where(e => e.Fornecedor != null).Select(e => e.Fornecedor!).Distinct().OrderBy(f => f).ToList();
        CarregarListasCompra(processo?.PedidoProposta.ProjetoPlanoId, processoId);
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
    public IActionResult ImportarPackingList(IFormFile? ficheiro, AreaDepartamento area, int? projetoPlanoId, string? contrato1Mais1,
        string? localizacao, string? fornecedor, int? processoId, string? armazem = null)
    {
        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Escolha o ficheiro do Packing List (Excel, Word .docx ou PDF).";
            return RedirectToAction(nameof(Entradas));
        }

        PackingListImportado pl;
        try
        {
            using var stream = ficheiro.OpenReadStream();
            pl = _packingList.Ler(stream, ficheiro.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler o Packing List {Ficheiro}.", ficheiro.FileName);
            TempData["EmailWarning"] = ex is NotSupportedException ? ex.Message : "Não foi possível ler o ficheiro. Confirme que é um Packing List em Excel (.xls ou .xlsx), Word (.docx) ou PDF com texto (não digitalizado).";
            return RedirectToAction(nameof(Entradas));
        }

        if (pl.Linhas.Count == 0)
        {
            TempData["EmailWarning"] = "O ficheiro não tem linhas de material. " + string.Join(" ", pl.Avisos);
            return RedirectToAction(nameof(Entradas));
        }

        // Compra feita na plataforma: o Processo dá o pedido, o projeto Capex e o fornecedor vencedor.
        var compra = ResolverCompra(processoId);
        if (processoId != null && compra == null)
        {
            TempData["EmailWarning"] = "O processo de compra escolhido não existe.";
            return RedirectToAction(nameof(Entradas));
        }
        projetoPlanoId ??= compra?.ProjetoPlanoId;
        var nomeFornecedor = Limitar(Vazio(fornecedor) ?? compra?.Fornecedor ?? pl.Fornecedor, 150);
        if (nomeFornecedor == null)
        {
            TempData["EmailWarning"] = "Indique o fornecedor do Packing List (ou escolha o processo de compra).";
            return RedirectToAction(nameof(Entradas), new { processoId });
        }

        var numero = Limitar(pl.Numero ?? Path.GetFileNameWithoutExtension(ficheiro.FileName), 60)!;
        var existente = _db.EntradasStock.FirstOrDefault(e => e.Numero == numero && e.Fornecedor == nomeFornecedor && e.Tipo == TipoEntradaStock.PackingList);
        if (existente != null)
        {
            TempData["EmailWarning"] = $"O Packing List {numero} de {nomeFornecedor} já foi importado.";
            return RedirectToAction(nameof(Entrada), new { id = existente.Id });
        }

        if (projetoPlanoId != null)
        {
            var erroProjeto = ValidarProjeto(projetoPlanoId);
            if (erroProjeto != null)
            {
                TempData["EmailWarning"] = erroProjeto;
                return RedirectToAction(nameof(Entradas), new { processoId });
            }
        }

        // O contrato 1+1 é da Huawei: só se preenche sozinho para a Huawei (ou se o utilizador o escrever).
        var ehHuawei = nomeFornecedor.Contains("huawei", StringComparison.OrdinalIgnoreCase);
        var entrada = new EntradaStock
        {
            Tipo = TipoEntradaStock.PackingList,
            Area = area,
            Numero = numero,
            Data = pl.Data ?? DateTime.Today,
            Fornecedor = nomeFornecedor,
            Contrato = Limitar(pl.Contrato, 60),
            Contrato1Mais1 = Vazio(contrato1Mais1) ?? (ehHuawei ? pl.Contrato1Mais1 : null),
            Fatura = Limitar(pl.Fatura, 60),
            PurchaseOrder = Limitar(pl.PurchaseOrder, 60),
            ProjetoFornecedor = Limitar(pl.ProjetoFornecedor, 200),
            ProjetoPlanoId = projetoPlanoId,
            PedidoPropostaId = compra?.PedidoPropostaId,
            Localizacao = Vazio(localizacao),
            Armazem = Vazio(armazem),
            NomeFicheiro = Limitar(Path.GetFileName(ficheiro.FileName), 200)
        };

        var materiais = ObterMateriais(pl.Linhas.Select(l => (l.Codigo, l.Modelo, l.Descricao, l.Unidade)), ehHuawei ? "Huawei" : nomeFornecedor, OrigemNomeMaterial.PackingList, entrada, codigosDoFornecedor: true);
        foreach (var l in pl.Linhas)
        {
            entrada.Movimentos.Add(new MovimentoStock
            {
                Material = materiais[l.Codigo],
                Area = area,
                Armazem = entrada.Armazem,
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
        var nomesDiferentes = NomesNovosDiferentes();
        _db.SaveChanges();

        TempData["Sucesso"] = $"Packing List {numero} importado: {pl.Linhas.Count} linha(s), {pl.Series.Count - avisosSeries} nº(s) de série."
            + (entrada.Contrato1Mais1 != null ? $" Contrato {entrada.Contrato1Mais1}." : "")
            + (avisosSeries > 0 ? $" {avisosSeries} nº(s) de série já existiam e foram ignorados." : "")
            + AvisoNomes(nomesDiferentes);
        return RedirectToAction(nameof(Entrada), new { id = entrada.Id });
    }

    public IActionResult NovaEntrada(TipoEntradaStock tipo = TipoEntradaStock.CompraDei, int? projetoPlanoId = null, int? processoId = null)
    {
        var compra = ResolverCompra(processoId);
        var vm = new NovaEntradaVM
        {
            ProcessoId = compra == null ? null : processoId,
            Entrada = new EntradaStock { Tipo = tipo, ProjetoPlanoId = projetoPlanoId ?? compra?.ProjetoPlanoId, Fornecedor = compra?.Fornecedor }
        };
        CarregarListasEntrada(vm);
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
        if (vm.Entrada.Tipo == TipoEntradaStock.PackingList) vm.Entrada.Tipo = TipoEntradaStock.Manual;

        if (!ModelState.IsValid)
        {
            CarregarListasEntrada(vm);
            return View(vm);
        }

        var entrada = vm.Entrada;
        if (ResolverCompra(vm.ProcessoId) is { } compraEntrada)
        {
            entrada.PedidoPropostaId = compraEntrada.PedidoPropostaId;
            entrada.ProjetoPlanoId ??= compraEntrada.ProjetoPlanoId;
            entrada.Fornecedor ??= compraEntrada.Fornecedor;
        }
        var materiais = ObterMateriais(linhas.Select(l => (l.Codigo!.Trim(), (string?)null, string.IsNullOrWhiteSpace(l.Descricao) ? l.Codigo!.Trim() : l.Descricao!.Trim(), Vazio(l.Unidade))), entrada.Fornecedor, OrigemNomeMaterial.Entrada, entrada);
        foreach (var l in linhas)
        {
            entrada.Movimentos.Add(new MovimentoStock
            {
                Material = materiais[l.Codigo!.Trim()],
                Area = entrada.Area,
                Armazem = entrada.Armazem,
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
        var nomesDiferentes = NomesNovosDiferentes();
        _db.SaveChanges();

        TempData["Sucesso"] = "Entrada registada." + (ignorados > 0 ? $" {ignorados} nº(s) de série já existiam e foram ignorados." : "") + AvisoNomes(nomesDiferentes);
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

    public IActionResult NovaSaida(int? materialStockId, AreaDepartamento? area, int? projetoPlanoId, string? armazem = null)
    {
        var vm = new NovaSaidaVM { MaterialStockId = materialStockId, Area = area ?? AreaDepartamento.DepInf, ProjetoPlanoId = projetoPlanoId, Armazem = string.IsNullOrWhiteSpace(armazem) ? null : armazem };
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
            var saldo = StockCalculos.Saldo(_db.MovimentosStock.Where(m => m.MaterialStockId == material.Id && m.Area == vm.Area && (vm.Armazem == null || m.Armazem == vm.Armazem)).ToList());
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
            Armazem = vm.Armazem,
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

    private int NomesNovosDiferentes() =>
        _db.ChangeTracker.Entries<DivergenciaNomeMaterial>().Count(e => e.State == EntityState.Added);

    private static string AvisoNomes(int n) =>
        n > 0 ? $" Atenção: {n} material(is) chegaram com um nome diferente do catálogo (mesmo código) — veja em Alarmes." : "";

    // Devolve (criando se preciso) os materiais do catálogo pelos códigos indicados.
    // codigosDoFornecedor (Packing List): o código é do fornecedor — se já estiver associado a um material
    // SAP vai para esse material; senão cria um material provisório (sem código SAP) para a DEI associar.
    // Nas outras entradas o código é o código SAP.
    private Dictionary<string, MaterialStock> ObterMateriais(IEnumerable<(string Codigo, string? Modelo, string Descricao, string? Unidade)> linhas, string? fabricante,
        OrigemNomeMaterial origem, EntradaStock entrada, bool codigosDoFornecedor = false)
    {
        var lista = linhas.GroupBy(l => l.Codigo, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
        var codigos = lista.Select(l => l.Codigo).ToList();
        var existentes = _db.MateriaisStock.Where(m => codigos.Contains(m.Codigo)).ToList()
            .ToDictionary(m => m.Codigo, StringComparer.OrdinalIgnoreCase);
        var diretos = new HashSet<string>(existentes.Keys, StringComparer.OrdinalIgnoreCase);

        var semMaterial = lista.Where(l => !existentes.ContainsKey(l.Codigo)).Select(l => l.Codigo).ToList();
        var correspondencias = codigosDoFornecedor && semMaterial.Count > 0
            ? _db.CodigosFornecedorMaterial.Include(c => c.Material).Where(c => semMaterial.Contains(c.Codigo)).ToList()
            : new List<CodigoFornecedorMaterial>();

        foreach (var l in lista.Where(l => !existentes.ContainsKey(l.Codigo)))
        {
            if (codigosDoFornecedor
                && CodigosFornecedor.Procurar(correspondencias.Where(c => c.Codigo.Equals(l.Codigo, StringComparison.OrdinalIgnoreCase)), fabricante) is { } c)
            {
                existentes[l.Codigo] = c.Material;
                continue;
            }
            var m = new MaterialStock
            {
                Codigo = Limitar(l.Codigo, 60)!,
                CodigoProvisorio = codigosDoFornecedor,
                Modelo = Limitar(l.Modelo, 100),
                Descricao = l.Descricao,
                Unidade = Limitar(l.Unidade, 20),
                Fabricante = Limitar(fabricante, 100)
            };
            if (codigosDoFornecedor)
                m.CodigosFornecedor.Add(new CodigoFornecedorMaterial
                {
                    Fornecedor = Limitar(fabricante, 150) ?? "(fornecedor não indicado)",
                    Codigo = Limitar(l.Codigo, 60)!,
                    Modelo = Limitar(l.Modelo, 100),
                    Descricao = l.Descricao
                });
            _db.MateriaisStock.Add(m);
            existentes[l.Codigo] = m;
        }
        // Material que já existe pelo mesmo código e chegou com outro nome: fica um alerta.
        foreach (var l in lista.Where(l => diretos.Contains(l.Codigo)))
            NomesMaterial.Registar(_db, existentes[l.Codigo], l.Descricao, origem, $"{entrada.Tipo.ToLabel()} {entrada.Numero}", entrada);
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

    private void CarregarListasEntrada(NovaEntradaVM vm) => CarregarListasCompra(vm.Entrada.ProjetoPlanoId, vm.ProcessoId);

    // Listas comuns às entradas: projetos Capex aceites, processos de compra da plataforma e fornecedores.
    private void CarregarListasCompra(int? projetoPlanoId, int? processoId)
    {
        ViewBag.ProjetosCapex = CapexProjetosHelper.ListaProjetos(_db, projetoPlanoId);
        ViewBag.Armazens = _db.ArmazensStock.OrderBy(a => a.Codigo).AsNoTracking().ToList();
        ViewBag.Processos = _db.Processos
            .Include(p => p.PedidoProposta)
            .Where(p => p.Status != StatusProcesso.Cancelado || p.Id == processoId)
            .OrderByDescending(p => p.CriadoEm)
            .AsNoTracking().ToList()
            .Select(p => new SelectListItem(
                $"{(string.IsNullOrWhiteSpace(p.NumeroProcesso) ? "" : p.NumeroProcesso + " — ")}{p.Nome} ({p.Status.ToLabel()})",
                p.Id.ToString(), p.Id == processoId))
            .ToList();
        ViewBag.Fornecedores = _db.Fornecedores.Where(f => f.Ativo).Select(f => f.Nome).ToList()
            .Concat(_db.EntradasStock.Where(e => e.Fornecedor != null).Select(e => e.Fornecedor!).Distinct().ToList())
            .Append("Huawei International Co. Limited")
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
    }

    private record CompraResolvida(int PedidoPropostaId, int? ProjetoPlanoId, string? Fornecedor);

    // Pedido, projeto Capex e fornecedor vencedor de um Processo de compra.
    private CompraResolvida? ResolverCompra(int? processoId)
    {
        if (processoId == null) return null;
        var p = _db.Processos.Include(x => x.PedidoProposta).Include(x => x.PropostaVencedora).AsNoTracking().FirstOrDefault(x => x.Id == processoId);
        return p == null ? null : new CompraResolvida(p.PedidoPropostaId, p.PedidoProposta.ProjetoPlanoId, p.PropostaVencedora?.Fornecedor);
    }

    private void CarregarListasSaida(NovaSaidaVM vm)
    {
        var materiais = _db.MateriaisStock.Include(m => m.Movimentos).AsNoTracking().ToList()
            .Select(m => (m, Saldo: StockCalculos.Saldo(m.Movimentos, vm.Area, vm.Armazem)))
            .Where(x => x.Saldo > 0 || x.m.Id == vm.MaterialStockId)
            .OrderBy(x => x.m.Descricao)
            .ToList();
        ViewBag.Materiais = materiais.Select(x => new SelectListItem($"{x.m.Codigo} · {x.m.Descricao} — {x.Saldo:0.###} {x.m.Unidade}", x.m.Id.ToString(), x.m.Id == vm.MaterialStockId)).ToList();
        ViewBag.Saldo = materiais.FirstOrDefault(x => x.m.Id == vm.MaterialStockId).Saldo;
        ViewBag.SeriesDisponiveis = vm.MaterialStockId == null ? new List<NumeroSerieStock>()
            : _db.NumerosSerieStock.Where(s => s.MaterialStockId == vm.MaterialStockId && s.Area == vm.Area && s.Estado == EstadoNumeroSerie.EmArmazem)
                .OrderBy(s => s.Serie).AsNoTracking().ToList();
        ViewBag.ProjetosCapex = CapexProjetosHelper.ListaProjetos(_db, vm.ProjetoPlanoId);
        ViewBag.Armazens = _db.ArmazensStock.OrderBy(a => a.Codigo).AsNoTracking().ToList();
    }

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? Limitar(string? s, int max) => s == null ? null : s.Length > max ? s[..max] : s;
}
