using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Controllers;

public class ComparacaoController : Controller
{
    private readonly IScoringService _scoringService;

    public ComparacaoController(IScoringService scoringService)
    {
        _scoringService = scoringService;
    }

    public IActionResult Index(int processoId)
    {
        try
        {
            var vm = _scoringService.BuildComparacao(processoId);
            return View(vm);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    public IActionResult Itens(int processoId)
    {
        try
        {
            var vm = _scoringService.BuildComparacaoItens(processoId);
            return View(vm);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
