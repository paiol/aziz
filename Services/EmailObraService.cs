using System.Text;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services;

// Só mailto: / texto para copiar — sem envio SMTP direto, seguindo a mesma decisão já
// tomada para o módulo de Processos (infraestrutura de email on-prem não é fiável).
public class EmailObraService : IEmailObraService
{
    private readonly IScoringObraService _scoringObraService;

    public EmailObraService(IScoringObraService scoringObraService)
    {
        _scoringObraService = scoringObraService;
    }

    public string ConstruirLinkMailto(ProjetoObra projeto)
    {
        var (destinatarios, assunto, corpo) = PrepararEmailResultado(projeto);
        var to = string.Join(",", destinatarios.Select(Uri.EscapeDataString));
        return $"mailto:{to}?subject={Uri.EscapeDataString(assunto)}&body={Uri.EscapeDataString(corpo)}";
    }

    public (string Destinatarios, string Assunto, string Corpo) ObterDadosEmailParaCopiar(ProjetoObra projeto)
    {
        var (destinatarios, assunto, corpo) = PrepararEmailResultado(projeto);
        return (string.Join(", ", destinatarios), assunto, corpo);
    }

    private (List<string> Destinatarios, string Assunto, string Corpo) PrepararEmailResultado(ProjetoObra projeto)
    {
        var destinatarios = (projeto.EmailsNotificacao ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var assunto = $"Resultado de Adjudicação — Projeto: {projeto.Designacao}";
        var corpo = BuildCorpoDecisaoTexto(projeto);

        return (destinatarios, assunto, corpo);
    }

    private string BuildCorpoDecisaoTexto(ProjetoObra projeto)
    {
        var vencedor = projeto.PropostaVencedora
                       ?? projeto.Propostas.FirstOrDefault(p => p.Id == projeto.PropostaVencedoraId);

        var ranking = projeto.Propostas
            .Select(p => new { Proposta = p, Pontuacao = _scoringObraService.CalcularPontuacaoPonderada(p, projeto.Criterios) })
            .OrderByDescending(r => r.Pontuacao)
            .ThenBy(r => r.Proposta.Subtotal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Projeto: {projeto.Designacao}");
        sb.AppendLine();
        sb.AppendLine("--- Empreiteiro Adjudicado ---");
        sb.AppendLine($"Empreiteiro: {(vencedor?.Empreiteiro ?? "Não especificado")}");

        if (vencedor != null)
        {
            sb.AppendLine($"Valor Adjudicado: {vencedor.Subtotal:C}");
            sb.AppendLine($"Prazo de Entrega: {(vencedor.PrazoEntregaDias.HasValue ? $"{vencedor.PrazoEntregaDias.Value} dias" : "-")}");
            var pontuacao = _scoringObraService.CalcularPontuacaoPonderada(vencedor, projeto.Criterios);
            sb.AppendLine($"Pontuação da Avaliação: {pontuacao:0.00} / 5.00");
        }

        sb.AppendLine($"Data da Decisão: {(projeto.DataAdjudicacao?.ToString("dd/MM/yyyy HH:mm") ?? DateTime.Now.ToString("dd/MM/yyyy"))}");
        sb.AppendLine($"Responsável pela Decisão: {(projeto.ResponsavelAdjudicacao ?? "-")}");

        if (!string.IsNullOrWhiteSpace(projeto.JustificativaAdjudicacao))
            sb.AppendLine($"Justificação da Decisão: {projeto.JustificativaAdjudicacao}");

        if (ranking.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- Empreiteiros Consultados e Ranking Final ---");
            for (var i = 0; i < ranking.Count; i++)
            {
                var item = ranking[i];
                sb.AppendLine($"{i + 1}º {item.Proposta.Empreiteiro} — {item.Proposta.Subtotal:C} — Pontuação {item.Pontuacao:0.00}/5.00");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Email gerado pelo Sistema de Comparação de Propostas — Módulo Obras.");

        return sb.ToString();
    }
}
