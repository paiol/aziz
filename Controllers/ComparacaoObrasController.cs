// MÓDULO OBRAS — comentado por enquanto (pedido da DEI, 25/11/2026).
// Para voltar a ligar: acrescentar <DefineConstants>$(DefineConstants);MODULO_OBRAS</DefineConstants> ao .csproj.
#if MODULO_OBRAS
using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Controllers;

public class ComparacaoObrasController : Controller
{
    private readonly IScoringObraService _scoringObraService;

    public ComparacaoObrasController(IScoringObraService scoringObraService)
    {
        _scoringObraService = scoringObraService;
    }

    public IActionResult Index(int projetoObraId)
    {
        try
        {
            var vm = _scoringObraService.BuildComparacao(projetoObraId);
            return View(vm);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

#endif
