using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PCOBarbershop.Server.Models;
using PCOBarbershop.Server.Settings;

namespace PCOBarbershop.Server.Data
{
    public class DbSeeder
    {
        public static async Task SeedBarbersAsync(IServiceProvider services, IConfiguration configuration)
        {
            var items = configuration.GetSection("SeedBarbers").Get<List<BarberSeedItem>>() ?? new();
            if (items.Count == 0)
            {
                return;
            }

            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var db = services.GetRequiredService<AppDbContext>();

            foreach (var item in items)
            {
                // 1. contul de utilizator (se creeaza doar daca nu exista)
                var user = await userManager.FindByEmailAsync(item.Email);
                if (user is null)
                {
                    user = new ApplicationUser
                    {
                        UserName = item.Email,
                        Email = item.Email,
                        EmailConfirmed = true,
                        FirstName = item.FirstName,
                        LastName = item.LastName
                    };

                    var result = await userManager.CreateAsync(user, item.Password);
                    if (!result.Succeeded)
                    {
                        var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                        throw new InvalidOperationException($"Nu s-a putut crea frizerul {item.Email}: {errors}");
                    }
                }

                // 2. rolul Frizer
                if (!await userManager.IsInRoleAsync(user, "Frizer"))
                {
                    await userManager.AddToRoleAsync(user, "Frizer");
                }

                // 3. profilul de frizer din tabela Barbers
                if (!await db.Barbers.AnyAsync(b => b.UserId == user.Id))
                {
                    db.Barbers.Add(new Barber { UserId = user.Id, DisplayName = item.DisplayName });
                    await db.SaveChangesAsync();
                }
            }
        }
    }
}
