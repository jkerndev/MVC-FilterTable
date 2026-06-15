using CookieShop.Mvc.Models;
using MvcFilterTable;

namespace CookieShop.Mvc.ViewModels;

public sealed class CookieIndexViewModel
{
    public required TableResult<CookieItem> Table { get; init; }

    public string? QueryText { get; init; }

    public string? ErrorMessage { get; init; }
}
