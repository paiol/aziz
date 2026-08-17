using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.Processos;

namespace ComparacaoPropostas.Controllers;

public class ProcessosController : Controller
{
    private readonly AppDbContext _db;
    private readonly IScoringService _scoringService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ProcessosController> _logger;

    public ProcessosController(AppDbContext db, IScoringService scoringService, IEmailService emailService, ILogger<ProcessosController> logger)
    {
        _db = db;
        _scoringService = scoringService;
        _emailService = emailService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var lista = _db.Processos
            .Include(p => p.PedidoProposta)
            .Include(p => p.Propostas)
            .OrderByDescending(p => p.CriadoEm)
            .Select(p => new ProcessoIndexVM
            {
                Id = p.Id,
                NumeroProcesso = p.NumeroProcesso,
                Nome = p.Nome,
                Status = p.Status,
                PrazoEntrega = p.PedidoProposta.PrazoEntrega,
                TipoProposta = p.PedidoProposta.TipoProposta,
                Area = p.PedidoProposta.Area,
                TotalFornecedores = p.Propostas.Select(pr => pr.Fornecedor).Distinct().Count(),
                OrcamentoEstimado = p.PedidoProposta.OrcamentoEstimado,
                TotalPropostas = p.Propostas.Count,
                MenorValorOfertado = p.Propostas.Any() ? p.Propostas.Min(pr => pr.ValorTotal) : (decimal?)null
            })
            .ToList();

        return View(lista);
    }

    public IActionResult Details(int id)
    {
        var processo = _db.Processos
            .Include(p => p.PedidoProposta).ThenInclude(pp => pp.ItensPedido)
            .Include(p => p.Criterios)
            .Include(p => p.PropostaVencedora)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes).ThenInclude(a => a.Avaliador)
            .Include(p => p.Propostas).ThenInclude(pr => pr.ItensProposta)
            .Include(p => p.Propostas).ThenInclude(pr => pr.MemoriaCalculo)
            .FirstOrDefault(p => p.Id == id);

        if (processo == null) return NotFound();

        _scoringService.AtualizarAvaliacaoAutomatica(processo);

        var propostasOrdenadas = processo.Propostas
            .Select(p => new PropostaResumo
            {
                Id = p.Id,
                Fornecedor = p.Fornecedor,
                Moeda = p.Moeda,
                TaxaCambio = p.TaxaCambio,
                ValorTotal = p.ValorTotal,
                ValorTotalCVE = p.ValorTotalCVE,
                ValorTotalEUR = p.ValorTotalEUR,
                PrazoEntregaDias = p.PrazoEntregaDias,
                Garantia = p.Garantia,
                Status = p.Status,
                PontuacaoPonderada = _scoringService.CalcularPontuacaoPonderada(p, processo.Criterios),
                TotalAvaliadores = p.Avaliacoes.Select(a => a.AvaliadorId).Distinct().Count()
            })
            .OrderByDescending(p => p.PontuacaoPonderada)
            .ThenBy(p => p.ValorTotalCVE)
            .ToList();

        for (var i = 0; i < propostasOrdenadas.Count; i++)
        {
            propostasOrdenadas[i].PosicaoRanking = i + 1;
        }

        var vm = new ProcessoDetailVM
        {
            Processo = processo,
            SomaPesos = processo.Criterios.Sum(c => c.Peso),
            TotalItensPedido = processo.PedidoProposta.ItensPedido.Count,
            Propostas = propostasOrdenadas
        };

        return View(vm);
    }

    private void CarregarPedidosDisponiveis(int? incluirId = null)
    {
        ViewBag.PedidosDisponiveis = _db.Pedidos
            .Include(p => p.ItensPedido)
            .Where(p => p.Processo == null || p.Id == incluirId)
            .OrderByDescending(p => p.CriadoEm)
            .ToList();
    }

    private void CarregarFornecedoresDisponiveis()
    {
        ViewBag.FornecedoresDisponiveis = _db.Fornecedores
            .Where(f => f.Ativo)
            .OrderBy(f => f.Nome)
            .ToList();
    }

    private string GerarNumeroProcesso()
    {
        var ano = DateTime.UtcNow.Year;
        var prefixo = $"{ano}-";
        var proximaSequencia = _db.Processos.Count(p => p.NumeroProcesso.StartsWith(prefixo)) + 1;
        return $"{prefixo}{proximaSequencia:0000}";
    }

    public IActionResult Create()
    {
        CarregarPedidosDisponiveis();
        CarregarFornecedoresDisponiveis();
        return View(new ProcessoCreateVM
        {
            TaxaCambioPadrao = MoedaHelper.TaxaEurCvePadrao,
            Fornecedores = new List<string> { "", "" } // Começa com 2 campos de fornecedores
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ProcessoCreateVM model)
    {
        // Limpar fornecedores vazios
        var fornecedoresValidos = (model.Fornecedores ?? new List<string>())
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fornecedoresValidos.Count == 0)
        {
            ModelState.AddModelError("", "Adicione pelo menos um fornecedor para o processo.");
        }

        if (!ModelState.IsValid)
        {
            CarregarPedidosDisponiveis(model.PedidoPropostaId);
            CarregarFornecedoresDisponiveis();
            return View(model);
        }

        using var transaction = _db.Database.BeginTransaction();
        try
        {
            var pedido = _db.Pedidos
                .Include(p => p.ItensPedido)
                .FirstOrDefault(p => p.Id == model.PedidoPropostaId);

            if (pedido == null)
            {
                ModelState.AddModelError("PedidoPropostaId", "Pedido de proposta não encontrado.");
                CarregarPedidosDisponiveis();
                CarregarFornecedoresDisponiveis();
                return View(model);
            }

            var processo = new Processo
            {
                PedidoPropostaId = model.PedidoPropostaId,
                NumeroProcesso = GerarNumeroProcesso(),
                Nome = model.Nome.Trim(),
                Descricao = model.Descricao?.Trim(),
                CriadoPor = model.CriadoPor?.Trim(),
                EmailsNotificacao = model.EmailsNotificacao?.Trim(),
                TaxaCambioPadrao = model.TaxaCambioPadrao > 0 ? model.TaxaCambioPadrao : MoedaHelper.TaxaEurCvePadrao,
                Status = StatusProcesso.Criado,
                Fornecedor = fornecedoresValidos.FirstOrDefault() ?? ""
            };

            _db.Processos.Add(processo);
            _db.SaveChanges();

            // Criar uma proposta estruturada para cada fornecedor com os itens clonados do pedido
            foreach (var fornecedorNome in fornecedoresValidos)
            {
                var proposta = new Proposta
                {
                    ProcessoId = processo.Id,
                    Fornecedor = fornecedorNome,
                    Moeda = MoedaHelper.MoedaCve,
                    TaxaCambio = processo.TaxaCambioPadrao,
                    Status = StatusProposta.Recebida
                };

                _scoringService.ClonarItensPedidoParaProposta(proposta, pedido.ItensPedido);
                _db.Propostas.Add(proposta);
            }

            pedido.Status = StatusPedido.Finalizado;
            _db.SaveChanges();

            transaction.Commit();
            TempData["Sucesso"] = $"Processo criado com sucesso com {fornecedoresValidos.Count} proposta(s) associada(s).";
            return RedirectToAction(nameof(Details), new { id = processo.Id });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Erro ao criar processo com fornecedores estruturados.");
            ModelState.AddModelError("", "Não foi possível criar o processo.");
            CarregarPedidosDisponiveis(model.PedidoPropostaId);
            CarregarFornecedoresDisponiveis();
            return View(model);
        }
    }

    public IActionResult Edit(int id)
    {
        var processo = _db.Processos.Include(p => p.PedidoProposta).FirstOrDefault(p => p.Id == id);
        if (processo == null) return NotFound();
        return View(processo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Processo processo)
    {
        if (id != processo.Id) return NotFound();

        var existente = _db.Processos.AsNoTracking().FirstOrDefault(p => p.Id == id);
        if (existente == null) return NotFound();

        processo.PedidoPropostaId = existente.PedidoPropostaId;
        processo.Status = existente.Status;
        processo.PropostaVencedoraId = existente.PropostaVencedoraId;
        processo.ValorAdjudicado = existente.ValorAdjudicado;
        processo.ValorAdjudicadoMoeda = existente.ValorAdjudicadoMoeda;
        processo.ValorAdjudicadoCVE = existente.ValorAdjudicadoCVE;
        processo.PontuacaoAdjudicada = existente.PontuacaoAdjudicada;
        processo.DataAdjudicacao = existente.DataAdjudicacao;
        processo.ResponsavelAdjudicacao = existente.ResponsavelAdjudicacao;
        processo.JustificativaAdjudicacao = existente.JustificativaAdjudicacao;
        processo.EmailResultadoEnviadoEm = existente.EmailResultadoEnviadoEm;

        ModelState.Remove(nameof(Processo.PedidoPropostaId));

        if (!ModelState.IsValid) return View(processo);

        try
        {
            _db.Processos.Update(processo);
            _db.SaveChanges();
            TempData["Sucesso"] = "Processo atualizado com sucesso.";
            return RedirectToAction(nameof(Details), new { id = processo.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao editar processo {Id}.", id);
            ModelState.AddModelError("", "Não foi possível guardar as alterações.");
            return View(processo);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        try
        {
            var processo = _db.Processos.Find(id);
            if (processo == null) return NotFound();

            _db.Processos.Remove(processo);
            _db.SaveChanges();
            TempData["Sucesso"] = "Processo removido com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover processo {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover o processo.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Adjudicar(int id)
    {
        var processo = _db.Processos
            .Include(p => p.Criterios)
            .Include(p => p.PedidoProposta).ThenInclude(pp => pp.ItensPedido)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes)
            .Include(p => p.Propostas).ThenInclude(pr => pr.ItensProposta)
            .Include(p => p.Propostas).ThenInclude(pr => pr.MemoriaCalculo)
            .FirstOrDefault(p => p.Id == id);

        if (processo == null) return NotFound();

        _scoringService.AtualizarAvaliacaoAutomatica(processo);

        if (processo.Propostas.Count == 0)
        {
            TempData["EmailWarning"] = "O processo não possui propostas para adjudicação.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var ranking = processo.Propostas
            .Select(p => new OpcaoPropostaAdjudicacao
            {
                Id = p.Id,
                Fornecedor = p.Fornecedor,
                PontuacaoPonderada = _scoringService.CalcularPontuacaoPonderada(p, processo.Criterios),
                ValorTotal = p.ValorTotal,
                ValorTotalCVE = p.ValorTotalCVE,
                Moeda = p.Moeda,
                PrazoEntregaDias = p.PrazoEntregaDias,
                Garantia = p.Garantia
            })
            .OrderByDescending(p => p.PontuacaoPonderada)
            .ThenBy(p => p.ValorTotalCVE)
            .ToList();

        for (var i = 0; i < ranking.Count; i++)
        {
            ranking[i].PosicaoRanking = i + 1;
        }

        var primeiro = ranking.FirstOrDefault();

        var vm = new AdjudicacaoVM
        {
            ProcessoId = processo.Id,
            ProcessoNome = processo.Nome,
            PropostaVencedoraId = processo.PropostaVencedoraId ?? primeiro?.Id ?? 0,
            ResponsavelAdjudicacao = processo.ResponsavelAdjudicacao ?? "",
            JustificativaAdjudicacao = processo.JustificativaAdjudicacao,
            PrimeiroLugarRankingId = primeiro?.Id,
            PrimeiroLugarFornecedor = primeiro?.Fornecedor,
            PropostasDisponiveis = ranking
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Adjudicar(AdjudicacaoVM model)
    {
        var processo = _db.Processos
            .Include(p => p.Criterios)
            .Include(p => p.PedidoProposta).ThenInclude(pp => pp.ItensPedido)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes)
            .Include(p => p.Propostas).ThenInclude(pr => pr.ItensProposta)
            .Include(p => p.Propostas).ThenInclude(pr => pr.MemoriaCalculo)
            .FirstOrDefault(p => p.Id == model.ProcessoId);

        if (processo == null) return NotFound();

        _scoringService.AtualizarAvaliacaoAutomatica(processo);

        var propostaVencedora = processo.Propostas.FirstOrDefault(p => p.Id == model.PropostaVencedoraId);
        if (propostaVencedora == null)
        {
            ModelState.AddModelError("PropostaVencedoraId", "A proposta selecionada não pertence a este processo.");
        }

        // Calcular ranking para validação da justificação
        var ranking = processo.Propostas
            .Select(p => new { p.Id, Pontuacao = _scoringService.CalcularPontuacaoPonderada(p, processo.Criterios), p.ValorTotalCVE })
            .OrderByDescending(p => p.Pontuacao)
            .ThenBy(p => p.ValorTotalCVE)
            .ToList();

        var primeiroId = ranking.FirstOrDefault()?.Id;
        if (primeiroId.HasValue && model.PropostaVencedoraId != primeiroId.Value && string.IsNullOrWhiteSpace(model.JustificativaAdjudicacao))
        {
            ModelState.AddModelError("JustificativaAdjudicacao", "É obrigatório fornecer uma justificação ao selecionar um fornecedor diferente do 1º classificado no ranking.");
        }

        if (!ModelState.IsValid)
        {
            model.PropostasDisponiveis = processo.Propostas
                .Select(p => new OpcaoPropostaAdjudicacao
                {
                    Id = p.Id,
                    Fornecedor = p.Fornecedor,
                    PontuacaoPonderada = _scoringService.CalcularPontuacaoPonderada(p, processo.Criterios),
                    ValorTotal = p.ValorTotal,
                    ValorTotalCVE = p.ValorTotalCVE,
                    Moeda = p.Moeda,
                    PrazoEntregaDias = p.PrazoEntregaDias,
                    Garantia = p.Garantia
                })
                .OrderByDescending(p => p.PontuacaoPonderada)
                .ThenBy(p => p.ValorTotalCVE)
                .ToList();

            for (var i = 0; i < model.PropostasDisponiveis.Count; i++)
                model.PropostasDisponiveis[i].PosicaoRanking = i + 1;

            model.PrimeiroLugarRankingId = primeiroId;
            model.PrimeiroLugarFornecedor = processo.Propostas.FirstOrDefault(p => p.Id == primeiroId)?.Fornecedor;
            return View(model);
        }

        using var transaction = _db.Database.BeginTransaction();
        try
        {
            var pontuacaoVencedora = _scoringService.CalcularPontuacaoPonderada(propostaVencedora!, processo.Criterios);

            processo.PropostaVencedoraId = propostaVencedora!.Id;
            processo.ValorAdjudicado = propostaVencedora.ValorTotal;
            processo.ValorAdjudicadoMoeda = propostaVencedora.Moeda;
            processo.ValorAdjudicadoCVE = propostaVencedora.ValorTotalCVE;
            processo.PontuacaoAdjudicada = pontuacaoVencedora;
            processo.DataAdjudicacao = DateTime.UtcNow;
            processo.ResponsavelAdjudicacao = model.ResponsavelAdjudicacao.Trim();
            processo.JustificativaAdjudicacao = string.IsNullOrWhiteSpace(model.JustificativaAdjudicacao) ? null : model.JustificativaAdjudicacao.Trim();
            processo.Status = StatusProcesso.Concluido;

            foreach (var p in processo.Propostas)
            {
                if (p.Id == propostaVencedora.Id)
                {
                    p.Status = StatusProposta.Aceite;
                }
                else
                {
                    p.Status = StatusProposta.Rejeitada;
                }
            }

            _db.SaveChanges();
            transaction.Commit();

            TempData["Sucesso"] = $"Adjudicação registada com sucesso para o fornecedor '{propostaVencedora.Fornecedor}'. O resultado pode agora ser comunicado por email.";
            return RedirectToAction(nameof(Details), new { id = processo.Id });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Erro ao registar adjudicação do processo {ProcessoId}.", model.ProcessoId);
            ModelState.AddModelError("", "Não foi possível concluir a adjudicação.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ComunicarResultado(int id)
    {
        var processo = _db.Processos
            .Include(p => p.PedidoProposta)
            .Include(p => p.Criterios)
            .Include(p => p.PropostaVencedora)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes)
            .Include(p => p.Propostas).ThenInclude(pr => pr.MemoriaCalculo)
            .FirstOrDefault(p => p.Id == id);

        if (processo == null) return NotFound();

        if (processo.Status != StatusProcesso.Concluido || !processo.PropostaVencedoraId.HasValue)
        {
            TempData["EmailWarning"] = "Apenas é possível comunicar o resultado de processos adjudicados e concluídos.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (string.IsNullOrWhiteSpace(processo.EmailsNotificacao))
        {
            TempData["EmailWarning"] = "Não existem e-mails de notificação configurados neste processo. Edite o processo para adicionar destinatários.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            await _emailService.EnviarNotificacaoDecisaoAsync(processo);
            processo.EmailResultadoEnviadoEm = DateTime.UtcNow;
            _db.SaveChanges();

            TempData["Sucesso"] = "Comunicação de adjudicação enviada com sucesso por email.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar email de resultado para o processo {ProcessoId}.", id);
            TempData["EmailWarning"] = "Não foi possível enviar o email de comunicação. Verifique a configuração SMTP e os endereços configurados.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult MudarStatus(int id, StatusProcesso novoStatus)
    {
        var processo = _db.Processos.Find(id);
        if (processo == null) return NotFound();

        processo.Status = novoStatus;
        _db.SaveChanges();

        TempData["Sucesso"] = "Estado do processo atualizado.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
