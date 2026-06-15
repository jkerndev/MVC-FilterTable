using CookieShop.Mvc.Models;

namespace CookieShop.Mvc.Data;

public static class CookieSeedData
{
    public static void Seed(CookieShopDbContext dbContext)
    {
        dbContext.Database.EnsureCreated();

        if (dbContext.Cookies.Any())
        {
            return;
        }

        dbContext.Cookies.AddRange(
            new CookieItem { Cookie = "Chocolate Chip", Price = 2.50, Number = 42, InStock = true, BatchCode = "A-100", Notes = "Classic butter dough with dark chocolate." },
            new CookieItem { Cookie = "Oatmeal Raisin", Price = 1.75, Number = 9, InStock = true, BatchCode = "B-220", Notes = "Cinnamon, oats, and golden raisins." },
            new CookieItem { Cookie = "Pistachio Macaron", Price = 3.75, Number = 0, InStock = false, BatchCode = "M-310", Notes = "Almond shell with pistachio filling." },
            new CookieItem { Cookie = "Lemon Shortbread", Price = 2.10, Number = 18, InStock = true, BatchCode = "S-410", Notes = "Bright citrus glaze." },
            new CookieItem { Cookie = "Double Fudge", Price = 3.25, Number = 6, InStock = true, BatchCode = "F-515", Notes = "Dense cocoa cookie with fudge chunks." },
            new CookieItem { Cookie = "Ginger Snap", Price = 1.50, Number = 24, InStock = true, BatchCode = "G-120", Notes = "Molasses and cracked ginger." },
            new CookieItem { Cookie = "Sugar Cookie", Price = 1.25, Number = 31, InStock = true, BatchCode = "S-111", Notes = "Vanilla dough with sanding sugar." },
            new CookieItem { Cookie = "Peanut Butter", Price = 2.00, Number = 0, InStock = false, BatchCode = "P-940", Notes = "Crisscross top and roasted peanuts." },
            new CookieItem { Cookie = "Snickerdoodle", Price = 1.90, Number = 13, InStock = true, BatchCode = "C-333", Notes = "Cinnamon sugar coating." },
            new CookieItem { Cookie = "Salted Caramel", Price = 3.10, Number = 5, InStock = true, BatchCode = "K-808", Notes = "Soft center with sea salt." },
            new CookieItem { Cookie = "Matcha White Chocolate", Price = 3.60, Number = 7, InStock = true, BatchCode = "M-777", Notes = "Green tea dough and white chocolate." },
            new CookieItem { Cookie = "Black Sesame", Price = 2.80, Number = 2, InStock = true, BatchCode = "B-909", Notes = "Nutty sesame shortbread." });

        dbContext.SaveChanges();
    }
}
