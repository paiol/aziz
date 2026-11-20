using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.Planos;

namespace ComparacaoPropostas.Controllers;

public class PlanosController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPlanoExcelService _planoExcelService;
    private readonly ILogger<PlanosController> _logger;

    public PlanosController(AppDbContext db, IPlanoExcelService planoExcelService, ILogger<PlanosController> logger)
    {
        _db = db;
        _planoExcelService = planoExcelService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var planos = _db.Planos
            .Include(p => p.Projetos)
            .Include(p => p.AcoesFormacao)
            .OrderByDescending(p => p.Ano)
            .ThenBy(p => p.Designacao)
            .AsNoTracking()
            .ToList();

        return View(planos);
    }

    // Resumo do plano: totais, por rubrica, por caixa do plano, carga mensal vs capacidade e formação.
    public IActionResult Details(int id)
    {
        var plano = _db.Planos
            .Include(p => p.Projetos)
            .Include(p => p.AcoesFormacao)
            .AsNoTracking()
            .FirstOrDefault(p => p.Id == id);

        if (plano == null) return NotFound();

        return View(new PlanoDetailVM { Plano = plano, Resumo = PlanoCalculos.CalcularResumo(plano) });
    }

    public IActionResult Create() => View(new Plano { Designacao = $"Plano Capex {DateTime.Today.Year + 1} DEI", Departamento = "DEI" });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Plano plano)
    {
        if (!ModelState.IsValid) return View(plano);

        _db.Planos.Add(plano);
        _db.SaveChanges();

        TempData["Sucesso"] = "Plano criado com sucesso.";
        return RedirectToAction(nameof(Details), new { id = plano.Id });
    }

    public IActionResult Edit(int id)
    {
        var plano = _db.Planos.Find(id);
        if (plano == null) return NotFound();
        return View(plano);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Plano plano)
    {
        if (id != plano.Id) return NotFound();

        var existente = _db.Planos.AsNoTracking().FirstOrDefault(p => p.Id == id);
        if (existente == null) return NotFound();
        plano.CriadoEm = existente.CriadoEm;

        if (!ModelState.IsValid) return View(plano);

        _db.Planos.Update(plano);
        _db.SaveChanges();

        TempData["Sucesso"] = "Plano atualizado com sucesso.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var plano = _db.Planos.Find(id);
        if (plano == null) return NotFound();

        if (_db.Pedidos.Any(p => p.ProjetoPlano != null && p.ProjetoPlano.PlanoId == id))
        {
            TempData["EmailWarning"] = "Não é possível remover: há compras associadas a projetos deste plano.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            DesligarProjetosSeguintes(_db.ProjetosPlano.Where(p => p.PlanoId == id).Select(p => p.Id).ToList());
            _db.Planos.Remove(plano);
            _db.SaveChanges();
            TempData["Sucesso"] = "Plano removido com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover plano {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover o plano.";
        }

        return RedirectToAction(nameof(Index));
    }

    // Cria um plano novo a partir do ficheiro SCOPE_INF (.xlsx) de uma área.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Importar(IFormFile ficheiro, AreaDepartamento area = AreaDepartamento.DepInf)
    {
        var importado = LerFicheiro(ficheiro);
        if (importado == null) return RedirectToAction(nameof(Index));

        var ano = importado.Ano ?? DateTime.Today.Year + 1;
        var plano = new Plano
        {
            Ano = ano,
            Designacao = $"Plano Capex {ano} DEI",
            Departamento = "DEI",
            CapacidadeArquiteturaDias = importado.CapacidadeArquiteturaDias,
            CapacidadeEnergiaDias = importado.CapacidadeEnergiaDias,
            Descricao = $"{area.ToLabel()}: importado de {Path.GetFileName(ficheiro.FileName)}"
        };
        AplicarImportacao(plano, importado, area, substituirFormacao: true);

        _db.Planos.Add(plano);
        _db.SaveChanges();

        TempData["Sucesso"] = MensagemImportacao(importado, area);
        return RedirectToAction(nameof(Details), new { id = plano.Id });
    }

    // Carrega (ou recarrega) os projetos de UMA área a partir do ficheiro dessa área.
    // Os projetos das outras áreas ficam intactos.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarParaPlano(int id, IFormFile ficheiro, AreaDepartamento area)
    {
        var plano = _db.Planos
            .Include(p => p.Projetos)
            .Include(p => p.AcoesFormacao)
            .FirstOrDefault(p => p.Id == id);
        if (plano == null) return NotFound();

        var daArea = plano.Projetos.Where(p => p.Area == area).ToList();
        var idsDaArea = daArea.Select(p => p.Id).ToList();
        if (_db.Pedidos.Any(p => p.ProjetoPlanoId != null && idsDaArea.Contains(p.ProjetoPlanoId.Value)))
        {
            TempData["EmailWarning"] = $"Não é possível substituir os projetos da área {area.ToLabel()}: alguns já têm compras associadas.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var importado = LerFicheiro(ficheiro);
        if (importado == null) return RedirectToAction(nameof(Details), new { id });

        DesligarProjetosSeguintes(idsDaArea);
        foreach (var p in daArea) plano.Projetos.Remove(p);
        _db.ProjetosPlano.RemoveRange(daArea);

        var substituirFormacao = importado.AcoesFormacao.Count > 0;
        if (substituirFormacao)
        {
            _db.AcoesFormacaoPlano.RemoveRange(plano.AcoesFormacao);
            plano.AcoesFormacao.Clear();
            plano.CapacidadeArquiteturaDias = importado.CapacidadeArquiteturaDias ?? plano.CapacidadeArquiteturaDias;
            plano.CapacidadeEnergiaDias = importado.CapacidadeEnergiaDias ?? plano.CapacidadeEnergiaDias;
        }
        AplicarImportacao(plano, importado, area, substituirFormacao);

        _db.SaveChanges();

        TempData["Sucesso"] = MensagemImportacao(importado, area);
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---- Plano do ano seguinte -----------------------------------------------------------

    // Revisão do plano deste ano: para cada projeto decide-se se transita, se fica
    // condicionado ou se não entra no plano do ano seguinte.
    public IActionResult CriarSeguinte(int id)
    {
        var anterior = CarregarPlanoComCompras(id);
        if (anterior == null) return NotFound();

        var existente = _db.Planos.AsNoTracking().FirstOrDefault(p => p.Ano == anterior.Ano + 1);
        var jaTransitados = existente == null ? new HashSet<int>() : _db.ProjetosPlano
            .Where(p => p.PlanoId == existente.Id && p.ProjetoOrigemId != null)
            .Select(p => p.ProjetoOrigemId!.Value)
            .ToHashSet();

        var vm = new TransicaoPlanoVM
        {
            PlanoAnteriorId = anterior.Id,
            PlanoAnteriorDesignacao = anterior.Designacao,
            AnoNovo = anterior.Ano + 1,
            DesignacaoNova = existente?.Designacao ?? $"Plano Capex {anterior.Ano + 1} DEI",
            PlanoExistenteId = existente?.Id,
            Linhas = anterior.Projetos
                .OrderBy(p => p.Area).ThenBy(p => p.DesignacaoRubrica).ThenBy(p => p.Nome)
                .Select(p =>
                {
                    var execucao = PlanoCalculos.CalcularExecucao(p);
                    return new LinhaTransicaoVM
                    {
                        ProjetoAnteriorId = p.Id,
                        Nome = p.Nome,
                        Area = p.Area,
                        Rubrica = p.DesignacaoRubrica,
                        SituacaoAnterior = p.Situacao,
                        EstadoAnterior = p.EstadoAprovacao,
                        Orcamento = execucao.Orcamento,
                        Adjudicado = execucao.Adjudicado,
                        JaTransitado = jaTransitados.Contains(p.Id),
                        Decisao = PlanoCalculos.DecisaoTransicaoSugerida(p),
                        ValorTransitar = PlanoCalculos.ValorTransitarSugerido(p)
                    };
                })
                .ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CriarSeguinte(TransicaoPlanoVM model)
    {
        var anterior = CarregarPlanoComCompras(model.PlanoAnteriorId);
        if (anterior == null) return NotFound();

        var novo = _db.Planos.Include(p => p.Projetos).FirstOrDefault(p => p.Ano == anterior.Ano + 1);
        var planoCriado = novo == null;
        if (novo == null)
        {
            novo = new Plano
            {
                Ano = anterior.Ano + 1,
                Designacao = string.IsNullOrWhiteSpace(model.DesignacaoNova) ? $"Plano Capex {anterior.Ano + 1} DEI" : model.DesignacaoNova.Trim(),
                Departamento = anterior.Departamento,
                CapacidadeArquiteturaDias = anterior.CapacidadeArquiteturaDias,
                CapacidadeEnergiaDias = anterior.CapacidadeEnergiaDias,
                Descricao = $"Criado a partir de {anterior.Designacao}"
            };
            _db.Planos.Add(novo);
        }

        var jaTransitados = novo.Projetos.Where(p => p.ProjetoOrigemId != null).Select(p => p.ProjetoOrigemId!.Value).ToHashSet();
        var copiados = 0;
        foreach (var linha in model.Linhas.Where(l => l.Decisao != null && !jaTransitados.Contains(l.ProjetoAnteriorId)))
        {
            var projetoAnterior = anterior.Projetos.FirstOrDefault(p => p.Id == linha.ProjetoAnteriorId);
            if (projetoAnterior == null) continue;
            novo.Projetos.Add(PlanoCalculos.TransitarProjeto(projetoAnterior, linha.Decisao!.Value, linha.ValorTransitar));
            copiados++;
        }

        // Ações de formação já previstas para o ano seguinte passam para o novo plano.
        if (planoCriado)
        {
            foreach (var a in anterior.AcoesFormacao.Where(a => a.Ano == novo.Ano))
            {
                novo.AcoesFormacao.Add(new AcaoFormacaoPlano
                {
                    Prioridade = a.Prioridade, Tipo = a.Tipo, Acao = a.Acao, Datas = a.Datas, Local = a.Local, Formato = a.Formato,
                    LigacaoProjetos = a.LigacaoProjetos, NumeroPessoas = a.NumeroPessoas, Dias = a.Dias, InscricaoPorPessoa = a.InscricaoPorPessoa,
                    PassagemPorPessoa = a.PassagemPorPessoa, EstadiaDia = a.EstadiaDia, AjudaCustoDia = a.AjudaCustoDia, NoPlano = a.NoPlano,
                    Participantes = a.Participantes, Fonte = a.Fonte, Observacoes = a.Observacoes, Ano = a.Ano
                });
            }
        }

        _db.SaveChanges();

        TempData["Sucesso"] = $"{copiados} projeto(s) passaram para {novo.Designacao}. As áreas podem agora propor os projetos novos.";
        return RedirectToAction(nameof(Details), new { id = novo.Id });
    }

    private Plano? CarregarPlanoComCompras(int id) => _db.Planos
        .Include(p => p.Projetos).ThenInclude(pp => pp.Pedidos).ThenInclude(pe => pe.Processo)
        .Include(p => p.AcoesFormacao)
        .AsSplitQuery()
        .FirstOrDefault(p => p.Id == id);

    // Os projetos de planos seguintes deixam de apontar para os projetos que vão ser apagados.
    private void DesligarProjetosSeguintes(List<int> ids)
    {
        if (ids.Count == 0) return;
        foreach (var seguinte in _db.ProjetosPlano.Where(p => p.ProjetoOrigemId != null && ids.Contains(p.ProjetoOrigemId.Value)))
            seguinte.ProjetoOrigemId = null;
    }

    private PlanoImportado? LerFicheiro(IFormFile? ficheiro)
    {
        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Selecione o ficheiro Excel do plano (SCOPE_INF).";
            return null;
        }

        try
        {
            using var stream = ficheiro.OpenReadStream();
            var importado = _planoExcelService.LerPlanoExcel(stream);
            if (importado.Projetos.Count == 0 && importado.AcoesFormacao.Count == 0)
            {
                TempData["EmailWarning"] = "Não foi encontrado nenhum projeto nem ação de formação no ficheiro. "
                    + string.Join(" ", importado.Avisos);
                return null;
            }
            return importado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler o ficheiro do plano {Ficheiro}.", ficheiro.FileName);
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro Excel.";
            return null;
        }
    }

    private static void AplicarImportacao(Plano plano, PlanoImportado importado, AreaDepartamento area, bool substituirFormacao)
    {
        foreach (var p in importado.Projetos)
        {
            p.Area = area;
            plano.Projetos.Add(p);
        }
        if (substituirFormacao)
            foreach (var a in importado.AcoesFormacao) plano.AcoesFormacao.Add(a);
    }

    private static string MensagemImportacao(PlanoImportado importado, AreaDepartamento area)
    {
        var msg = $"{area.ToLabel()}: {importado.Projetos.Count} projeto(s) e {importado.AcoesFormacao.Count} ação(ões) de formação/feiras.";
        if (importado.Avisos.Count > 0) msg += " Avisos: " + string.Join(" ", importado.Avisos.Take(5));
        return msg;
    }
}
