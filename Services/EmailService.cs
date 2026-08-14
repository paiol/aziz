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

    private string BuildCorpoDecisao(Processo processo)
    {
        var vencedor = processo.PropostaVencedora 
                       ?? processo.Propostas.FirstOrDefault(p => p.Id == processo.PropostaVencedoraId);

        var sb = new StringBuilder();
        sb.Append("<div style='font-family: Arial, sans-serif; color: #212529; max-width: 680px; margin: 0 auto; line-height: 1.5;'>");
        sb.Append($"<h2 style='color: #0d6efd; margin-bottom: 4px;'>Resultado do Processo de Aquisição</h2>");
        sb.Append($"<p style='font-size: 1.1em; margin-top: 0;'><strong>Processo #{processo.Id}:</strong> {processo.Nome}</p>");

        if (!string.IsNullOrWhiteSpace(processo.Descricao))
        {
            sb.Append($"<p><strong>Descrição / Objeto:</strong> {processo.Descricao}</p>");
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
