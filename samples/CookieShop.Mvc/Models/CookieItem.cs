namespace CookieShop.Mvc.Models;

public sealed class CookieItem
{
    public int Id { get; set; }

    public string Cookie { get; set; } = string.Empty;

    public double Price { get; set; }

    public int Number { get; set; }

    public bool InStock { get; set; }

    public string BatchCode { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}
