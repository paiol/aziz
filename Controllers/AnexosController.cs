using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Controllers;

public class AnexosController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AnexosController> _logger;

    private static readonly string[] ExtensoesPermitidas = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg" };
    private const long TamanhoMaximoBytes = 20 * 1024 * 1024;

    public AnexosController(AppDbContext db, IWebHostEnvironment env, ILogger<AnexosController> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Upload(int propostaId, IFormFile ficheiro)
    {
        var proposta = _db.Propostas.Find(propostaId);
        if (proposta == null) return NotFound();

        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Selecione um ficheiro válido.";
            return RedirectToAction("Edit", "Propostas", new { id = propostaId });
        }

        var extensao = Path.GetExtension(ficheiro.FileName).ToLowerInvariant();
        if (!ExtensoesPermitidas.Contains(extensao) || ficheiro.Length > TamanhoMaximoBytes)
        {
            TempData["EmailWarning"] = "Ficheiro inválido: tipo não suportado ou maior que 20MB.";
            return RedirectToAction("Edit", "Propostas", new { id = propostaId });
        }

        try
        {
            var pastaRelativa = Path.Combine("uploads", "propostas", proposta.ProcessoId.ToString(), proposta.Id.ToString());
            var pastaAbsoluta = Path.Combine(_env.WebRootPath, pastaRelativa);
            Directory.CreateDirectory(pastaAbsoluta);

            var nomeArmazenado = $"{Guid.NewGuid()}{extensao}";
            var caminhoAbsoluto = Path.Combine(pastaAbsoluta, nomeArmazenado);

            using (var stream = new FileStream(caminhoAbsoluto, FileMode.Create))
                ficheiro.CopyTo(stream);

            _db.PropostasAnexo.Add(new PropostaAnexo
            {
                PropostaId = propostaId,
                NomeArquivo = ficheiro.FileName,
                CaminhoArquivo = Path.Combine(pastaRelativa, nomeArmazenado).Replace("\\", "/")
            });
            _db.SaveChanges();

            TempData["Sucesso"] = "Anexo enviado com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar anexo para a proposta {PropostaId}.", propostaId);
            TempData["EmailWarning"] = "Não foi possível enviar o anexo.";
        }

        return RedirectToAction("Edit", "Propostas", new { id = propostaId });
    }

    public IActionResult Download(int id)
    {
        var anexo = _db.PropostasAnexo.Find(id);
        if (anexo == null) return NotFound();

        var caminhoAbsoluto = Path.Combine(_env.WebRootPath, anexo.CaminhoArquivo.Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (!System.IO.File.Exists(caminhoAbsoluto)) return NotFound();

        var bytes = System.IO.File.ReadAllBytes(caminhoAbsoluto);
        return File(bytes, "application/octet-stream", anexo.NomeArquivo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var anexo = _db.PropostasAnexo.Find(id);
        if (anexo == null) return NotFound();

        var propostaId = anexo.PropostaId;

        try
        {
            var caminhoAbsoluto = Path.Combine(_env.WebRootPath, anexo.CaminhoArquivo.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (System.IO.File.Exists(caminhoAbsoluto))
                System.IO.File.Delete(caminhoAbsoluto);

            _db.PropostasAnexo.Remove(anexo);
            _db.SaveChanges();
            TempData["Sucesso"] = "Anexo removido.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover anexo {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover o anexo.";
        }

        return RedirectToAction("Edit", "Propostas", new { id = propostaId });
    }
}
