using CookieShop.Mvc.Models;
using Microsoft.EntityFrameworkCore;

namespace CookieShop.Mvc.Data;

public sealed class CookieShopDbContext : DbContext
{
    public CookieShopDbContext(DbContextOptions<CookieShopDbContext> options)
        : base(options)
    {
    }

    public DbSet<CookieItem> Cookies => Set<CookieItem>();
}
