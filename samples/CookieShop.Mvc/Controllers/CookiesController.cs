using CookieShop.Mvc.Data;
using CookieShop.Mvc.Tables;
using CookieShop.Mvc.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcFilterTable;

namespace CookieShop.Mvc.Controllers;

public sealed class CookiesController : Controller
{
    private readonly CookieShopDbContext _dbContext;

    public CookiesController(CookieShopDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IActionResult Index(string? q, string? sort, string? dir, int page = 1, int pageSize = 8)
    {
        var request = CreateRequest(q, sort, dir, page, pageSize);

        try
        {
            return View(CreateViewModel(request, q, errorMessage: null));
        }
        catch (TableQueryException exception)
        {
            var fallbackRequest = CreateRequest(null, null, null, 1, pageSize);
            return View(CreateViewModel(fallbackRequest, q, exception.Message));
        }
    }

    private CookieIndexViewModel CreateViewModel(TableRequest request, string? queryText, string? errorMessage)
    {
        var table = TableQuery.Apply(
            _dbContext.Cookies.AsNoTracking(),
            CookieTable.Definition,
            request,
            new TableQueryOptions { MaxRegexCandidateRows = 500 });

        return new CookieIndexViewModel
        {
            Table = table,
            QueryText = queryText,
            ErrorMessage = errorMessage
        };
    }

    private static TableRequest CreateRequest(string? query, string? sort, string? dir, int page, int pageSize)
    {
        return new TableRequest
        {
            Query = query,
            SortBy = sort,
            SortDirection = ParseDirection(dir),
            PageNumber = page,
            PageSize = pageSize
        };
    }

    private static SortDirection ParseDirection(string? direction)
    {
        return string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)
            ? SortDirection.Descending
            : SortDirection.Ascending;
    }
}
