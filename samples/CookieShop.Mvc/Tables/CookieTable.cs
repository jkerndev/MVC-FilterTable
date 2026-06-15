using CookieShop.Mvc.Models;
using MvcFilterTable;

namespace CookieShop.Mvc.Tables;

public static class CookieTable
{
    public static readonly TableDefinition<CookieItem> Definition = TableDefinition
        .For<CookieItem>()
        .Column("cookie", "Cookie", cookie => cookie.Cookie)
        .Column("price", "Price", cookie => cookie.Price)
        .Column("number", "Qty", cookie => cookie.Number)
        .Column("instock", "Stock", cookie => cookie.InStock)
        .Column("batch", "Batch", cookie => cookie.BatchCode, visible: false)
        .Build();
}
