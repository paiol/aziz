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
