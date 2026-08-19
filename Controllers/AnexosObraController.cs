using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Controllers;

public class AnexosObraController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AnexosObraController> _logger;

    private static readonly string[] ExtensoesPermitidas = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".dwg", ".png", ".jpg", ".jpeg" };
    private const long TamanhoMaximoBytes = 20 * 1024 * 1024;

    public AnexosObraController(AppDbContext db, IWebHostEnvironment env, ILogger<AnexosObraController> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Upload(int projetoObraId, TipoDocumentoObra tipoDocumento, IFormFile ficheiro)
    {
        var projeto = _db.ProjetosObra.Find(projetoObraId);
        if (projeto == null) return NotFound();

        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Selecione um ficheiro válido.";
            return RedirectToAction("Details", "ProjetosObra", new { id = projetoObraId });
        }

        var extensao = Path.GetExtension(ficheiro.FileName).ToLowerInvariant();
        if (!ExtensoesPermitidas.Contains(extensao) || ficheiro.Length > TamanhoMaximoBytes)
        {
            TempData["EmailWarning"] = "Ficheiro inválido: tipo não suportado ou maior que 20MB.";
            return RedirectToAction("Details", "ProjetosObra", new { id = projetoObraId });
        }

        try
        {
            var pastaRelativa = Path.Combine("uploads", "obras", projeto.Id.ToString());
            var pastaAbsoluta = Path.Combine(_env.WebRootPath, pastaRelativa);
            Directory.CreateDirectory(pastaAbsoluta);

            var nomeArmazenado = $"{Guid.NewGuid()}{extensao}";
            var caminhoAbsoluto = Path.Combine(pastaAbsoluta, nomeArmazenado);

            using (var stream = new FileStream(caminhoAbsoluto, FileMode.Create))
                ficheiro.CopyTo(stream);

            _db.ProjetoObraAnexos.Add(new ProjetoObraAnexo
            {
                ProjetoObraId = projetoObraId,
                NomeArquivo = ficheiro.FileName,
                CaminhoArquivo = Path.Combine(pastaRelativa, nomeArmazenado).Replace("\\", "/"),
                TipoDocumento = tipoDocumento
            });
            _db.SaveChanges();

            TempData["Sucesso"] = "Anexo enviado com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar anexo para o projeto de obra {ProjetoObraId}.", projetoObraId);
            TempData["EmailWarning"] = "Não foi possível enviar o anexo.";
        }

        return RedirectToAction("Details", "ProjetosObra", new { id = projetoObraId });
    }

    public IActionResult Download(int id)
    {
        var anexo = _db.ProjetoObraAnexos.Find(id);
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
        var anexo = _db.ProjetoObraAnexos.Find(id);
        if (anexo == null) return NotFound();

        var projetoObraId = anexo.ProjetoObraId;

        try
        {
            var caminhoAbsoluto = Path.Combine(_env.WebRootPath, anexo.CaminhoArquivo.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (System.IO.File.Exists(caminhoAbsoluto))
                System.IO.File.Delete(caminhoAbsoluto);

            _db.ProjetoObraAnexos.Remove(anexo);
            _db.SaveChanges();
            TempData["Sucesso"] = "Anexo removido.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover anexo {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover o anexo.";
        }

        return RedirectToAction("Details", "ProjetosObra", new { id = projetoObraId });
    }
}
