using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services;

public class EmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly IScoringService _scoringService;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpSettings> settings, IScoringService scoringService, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _scoringService = scoringService;
        _logger = logger;
    }

    public async Task EnviarNotificacaoDecisaoAsync(Processo processo)
    {
        var destinatarios = (processo.EmailsNotificacao ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (destinatarios.Count == 0 || string.IsNullOrWhiteSpace(_settings.Host))
        {
            _logger.LogInformation("Notificação de decisão não enviada para o processo {ProcessoId}: sem destinatários ou SMTP não configurado.", processo.Id);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_settings.From));
            foreach (var email in destinatarios)
                message.To.Add(MailboxAddress.Parse(email));
            message.Subject = $"Resultado de Adjudicação — Processo #{processo.Id}: {processo.Nome}";
            message.Body = new TextPart("html") { Text = BuildCorpoDecisao(processo) };

            using var client = new SmtpClient();
            var socketOptions = _settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions);
            if (!string.IsNullOrWhiteSpace(_settings.User))
                await client.AuthenticateAsync(_settings.User, _settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar email de notificação de decisão para o processo {ProcessoId}.", processo.Id);
            throw;
        }
    }

    public string ConstruirLinkMailto(Processo processo)
    {
        var destinatarios = (processo.EmailsNotificacao ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var numeroExibido = string.IsNullOrWhiteSpace(processo.NumeroProcesso) ? processo.Id.ToString() : processo.NumeroProcesso;
        var assunto = $"Resultado de Adjudicação — Processo #{numeroExibido}: {processo.Nome}";
        var corpo = BuildCorpoDecisaoTexto(processo);

        var to = string.Join(",", destinatarios.Select(Uri.EscapeDataString));
        return $"mailto:{to}?subject={Uri.EscapeDataString(assunto)}&body={Uri.EscapeDataString(corpo)}";
    }

    internal string BuildCorpoDecisaoTexto(Processo processo)
    {
        var vencedor = processo.PropostaVencedora
                       ?? processo.Propostas.FirstOrDefault(p => p.Id == processo.PropostaVencedoraId);

        var ranking = processo.Propostas
            .Select(p => new
            {
                Proposta = p,
                Pontuacao = _scoringService.CalcularPontuacaoPonderada(p, processo.Criterios)
            })
            .OrderByDescending(r => r.Pontuacao)
            .ThenBy(r => r.Proposta.ValorTotalCVE)
            .ToList();

        var numeroExibido = string.IsNullOrWhiteSpace(processo.NumeroProcesso) ? processo.Id.ToString() : processo.NumeroProcesso;

        var sb = new StringBuilder();
        sb.AppendLine($"Processo Nº {numeroExibido}: {processo.Nome}");
        if (!string.IsNullOrWhiteSpace(processo.Descricao))
            sb.AppendLine($"Descrição: {processo.Descricao}");
        sb.AppendLine();
        sb.AppendLine("--- Fornecedor Adjudicado ---");
        sb.AppendLine($"Fornecedor: {(vencedor?.Fornecedor ?? "Não especificado")}");

        if (vencedor != null)
        {
            sb.AppendLine($"Valor Adjudicado: {MoedaHelper.FormatarValor(vencedor.ValorTotal, vencedor.Moeda)}");
            if (string.Equals(vencedor.Moeda, MoedaHelper.MoedaEur, StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"Equivalente em CVE: {MoedaHelper.FormatarValor(vencedor.ValorTotalCVE, MoedaHelper.MoedaCve)} (Taxa: {vencedor.TaxaCambio:N3})");
            sb.AppendLine($"Prazo de Entrega: {(vencedor.PrazoEntregaDias.HasValue ? $"{vencedor.PrazoEntregaDias.Value} dias" : "-")}");
            sb.AppendLine($"Garantia: {(string.IsNullOrWhiteSpace(vencedor.Garantia) ? "-" : vencedor.Garantia)}");
            var pontuacao = processo.PontuacaoAdjudicada ?? _scoringService.CalcularPontuacaoPonderada(vencedor, processo.Criterios);
            sb.AppendLine($"Pontuação da Avaliação: {pontuacao:0.00} / 5.00");
        }

        sb.AppendLine($"Data da Decisão: {(processo.DataAdjudicacao?.ToString("dd/MM/yyyy HH:mm") ?? DateTime.Now.ToString("dd/MM/yyyy"))}");
        sb.AppendLine($"Responsável pela Decisão: {(processo.ResponsavelAdjudicacao ?? "-")}");

        if (!string.IsNullOrWhiteSpace(processo.JustificativaAdjudicacao))
            sb.AppendLine($"Justificação da Decisão: {processo.JustificativaAdjudicacao}");

        if (ranking.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- Fornecedores Consultados e Ranking Final ---");
            for (var i = 0; i < ranking.Count; i++)
            {
                var item = ranking[i];
                var valorFormatado = MoedaHelper.FormatarValor(item.Proposta.ValorTotal, item.Proposta.Moeda);
                sb.AppendLine($"{i + 1}º {item.Proposta.Fornecedor} — {valorFormatado} — Pontuação {item.Pontuacao:0.00}/5.00");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Email gerado pelo Sistema de Comparação de Propostas.");

        return sb.ToString();
    }

    internal string BuildCorpoDecisao(Processo processo)
    {
        var vencedor = processo.PropostaVencedora
                       ?? processo.Propostas.FirstOrDefault(p => p.Id == processo.PropostaVencedoraId);

        var ranking = processo.Propostas
            .Select(p => new
            {
                Proposta = p,
                Pontuacao = _scoringService.CalcularPontuacaoPonderada(p, processo.Criterios)
            })
            .OrderByDescending(r => r.Pontuacao)
            .ThenBy(r => r.Proposta.ValorTotalCVE)
            .ToList();

        var numeroExibido = string.IsNullOrWhiteSpace(processo.NumeroProcesso) ? processo.Id.ToString() : processo.NumeroProcesso;

        var sb = new StringBuilder();
        sb.Append("<div style='font-family: Arial, sans-serif; color: #212529; max-width: 680px; margin: 0 auto; line-height: 1.5;'>");
        sb.Append($"<h2 style='color: #0d6efd; margin-bottom: 4px;'>Resultado do Processo de Aquisição</h2>");
        sb.Append($"<p style='font-size: 1.1em; margin-top: 0;'><strong>Processo Nº {numeroExibido}:</strong> {processo.Nome}</p>");

        if (!string.IsNullOrWhiteSpace(processo.Descricao))
        {
            sb.Append($"<p><strong>Descrição / Objeto:</strong> {processo.Descricao}</p>");
        }

        if (processo.PedidoProposta != null)
        {
            sb.Append($"<p><strong>Pedido de Aquisição Associado:</strong> {processo.PedidoProposta.TipoProposta} — {processo.PedidoProposta.Area.ToLabel()}</p>");
        }

        sb.Append("<hr style='border: 0; border-top: 1px solid #dee2e6; margin: 16px 0;' />");

        sb.Append("<h3 style='color: #198754;'>Fornecedor Adjudicado</h3>");
        sb.Append("<table style='width: 100%; border-collapse: collapse; margin-bottom: 20px;'>");
        
        sb.Append($"<tr><td style='padding: 6px 0; width: 220px; font-weight: bold;'>Fornecedor Vencedor:</td><td>{(vencedor?.Fornecedor ?? "Não especificado")}</td></tr>");
        
        if (vencedor != null)
        {
            var valorFormatado = MoedaHelper.FormatarValor(vencedor.ValorTotal, vencedor.Moeda);
            sb.Append($"<tr><td style='padding: 6px 0; font-weight: bold;'>Valor Adjudicado:</td><td>{valorFormatado}</td></tr>");
            
            if (string.Equals(vencedor.Moeda, MoedaHelper.MoedaEur, StringComparison.OrdinalIgnoreCase))
            {
                var valorCve = MoedaHelper.FormatarValor(vencedor.ValorTotalCVE, MoedaHelper.MoedaCve);
                sb.Append($"<tr><td style='padding: 6px 0; font-weight: bold;'>Equivalente em CVE:</td><td>{valorCve} (Taxa: {vencedor.TaxaCambio:N3})</td></tr>");
            }

            sb.Append($"<tr><td style='padding: 6px 0; font-weight: bold;'>Prazo de Entrega:</td><td>{(vencedor.PrazoEntregaDias.HasValue ? $"{vencedor.PrazoEntregaDias.Value} dias" : "-")}</td></tr>");
            sb.Append($"<tr><td style='padding: 6px 0; font-weight: bold;'>Garantia:</td><td>{(string.IsNullOrWhiteSpace(vencedor.Garantia) ? "-" : vencedor.Garantia)}</td></tr>");
            
            var pontuacao = processo.PontuacaoAdjudicada ?? _scoringService.CalcularPontuacaoPonderada(vencedor, processo.Criterios);
            sb.Append($"<tr><td style='padding: 6px 0; font-weight: bold;'>Pontuação da Avaliação:</td><td><strong>{pontuacao:0.00}</strong> / 5.00</td></tr>");
        }

        sb.Append($"<tr><td style='padding: 6px 0; font-weight: bold;'>Data da Decisão:</td><td>{(processo.DataAdjudicacao?.ToString("dd/MM/yyyy HH:mm") ?? DateTime.Now.ToString("dd/MM/yyyy"))}</td></tr>");
        sb.Append($"<tr><td style='padding: 6px 0; font-weight: bold;'>Responsável pela Decisão:</td><td>{(processo.ResponsavelAdjudicacao ?? "-")}</td></tr>");

        if (!string.IsNullOrWhiteSpace(processo.JustificativaAdjudicacao))
        {
            sb.Append($"<tr><td style='padding: 6px 0; font-weight: bold; vertical-align: top;'>Justificação da Decisão:</td><td>{processo.JustificativaAdjudicacao}</td></tr>");
        }

        sb.Append("</table>");

        // Ranking completo — todos os fornecedores consultados
        if (ranking.Count > 0)
        {
            sb.Append("<h3 style='color: #0d6efd;'>Fornecedores Consultados e Ranking Final</h3>");
            sb.Append("<table style='width: 100%; border-collapse: collapse; margin-bottom: 20px; font-size: 0.95em;'>");
            sb.Append("<tr style='background: #f8f9fa;'>");
            sb.Append("<th style='padding: 6px; text-align: left; border-bottom: 2px solid #dee2e6;'>#</th>");
            sb.Append("<th style='padding: 6px; text-align: left; border-bottom: 2px solid #dee2e6;'>Fornecedor</th>");
            sb.Append("<th style='padding: 6px; text-align: left; border-bottom: 2px solid #dee2e6;'>Valor</th>");
            sb.Append("<th style='padding: 6px; text-align: left; border-bottom: 2px solid #dee2e6;'>Prazo</th>");
            sb.Append("<th style='padding: 6px; text-align: left; border-bottom: 2px solid #dee2e6;'>Garantia</th>");
            sb.Append("<th style='padding: 6px; text-align: left; border-bottom: 2px solid #dee2e6;'>Pontuação</th>");
            sb.Append("</tr>");

            for (var i = 0; i < ranking.Count; i++)
            {
                var item = ranking[i];
                var isVencedor = vencedor != null && item.Proposta.Id == vencedor.Id;
                var estilo = isVencedor ? "background: #d1e7dd; font-weight: bold;" : "";
                var valorFormatado = MoedaHelper.FormatarValor(item.Proposta.ValorTotal, item.Proposta.Moeda);

                sb.Append($"<tr style='{estilo}'>");
                sb.Append($"<td style='padding: 6px; border-bottom: 1px solid #eee;'>{i + 1}º{(isVencedor ? " 🏆" : "")}</td>");
                sb.Append($"<td style='padding: 6px; border-bottom: 1px solid #eee;'>{item.Proposta.Fornecedor}</td>");
                sb.Append($"<td style='padding: 6px; border-bottom: 1px solid #eee;'>{valorFormatado}</td>");
                sb.Append($"<td style='padding: 6px; border-bottom: 1px solid #eee;'>{(item.Proposta.PrazoEntregaDias.HasValue ? $"{item.Proposta.PrazoEntregaDias.Value} dias" : "-")}</td>");
                sb.Append($"<td style='padding: 6px; border-bottom: 1px solid #eee;'>{(string.IsNullOrWhiteSpace(item.Proposta.Garantia) ? "-" : item.Proposta.Garantia)}</td>");
                sb.Append($"<td style='padding: 6px; border-bottom: 1px solid #eee;'>{item.Pontuacao:0.00} / 5.00</td>");
                sb.Append("</tr>");
            }

            sb.Append("</table>");
        }

        // Resumo de Critérios
        if (processo.Criterios.Count > 0)
        {
            sb.Append("<h4 style='margin-bottom: 8px;'>Critérios de Avaliação Utilizados</h4>");
            sb.Append("<ul style='margin-top: 0; padding-left: 20px;'>");
            foreach (var c in processo.Criterios.OrderByDescending(c => c.Peso))
            {
                sb.Append($"<li>{c.Nome} — <strong>{c.Peso:0.##}%</strong></li>");
            }
            sb.Append("</ul>");
        }

        sb.Append("<p style='font-size: 0.9em; color: #6c757d; margin-top: 24px;'>Este email foi gerado automaticamente pelo Sistema de Comparação de Propostas. Os detalhes e o mapa comparativo completo podem ser consultados na plataforma.</p>");
        sb.Append("</div>");

        return sb.ToString();
    }
}
