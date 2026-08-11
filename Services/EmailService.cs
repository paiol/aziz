using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
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
            message.Subject = $"Processo decidido: {processo.Nome}";
            message.Body = new TextPart("html") { Text = BuildCorpoRanking(processo) };

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
        }
    }

    private string BuildCorpoRanking(Processo processo)
    {
        var ranking = processo.Propostas
            .Select(p => new { p.Fornecedor, p.ValorTotal, Pontuacao = _scoringService.CalcularPontuacaoPonderada(p) })
            .OrderByDescending(p => p.Pontuacao)
            .ToList();

        var sb = new StringBuilder();
        sb.Append($"<h2>Processo decidido: {processo.Nome}</h2>");
        sb.Append("<table border='1' cellpadding='6' cellspacing='0' style='border-collapse:collapse'>");
        sb.Append("<tr><th>Fornecedor</th><th>Valor Total</th><th>Pontuação Ponderada</th></tr>");
        foreach (var r in ranking)
            sb.Append($"<tr><td>{r.Fornecedor}</td><td>{r.ValorTotal:C}</td><td>{r.Pontuacao:0.00}</td></tr>");
        sb.Append("</table>");
        return sb.ToString();
    }
}
