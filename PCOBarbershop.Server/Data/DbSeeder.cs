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

        public static async Task SeedServicesAsync(AppDbContext db)
        {
            var services = new[]
            {
                new Service { Name = "Tuns simplu", Price = 80m, DurationMinutes = 40 },
                new Service { Name = "Tuns + Barba", Price = 100m, DurationMinutes = 60 },
                new Service { Name = "Barba", Price = 20m, DurationMinutes = 20 }
            };

            foreach (var service in services)
            {
                // adaugam serviciul doar daca nu exista deja unul cu acelasi nume
                if (!await db.Services.AnyAsync(s => s.Name == service.Name))
                {
                    db.Services.Add(service);
                }
            }

            await db.SaveChangesAsync();
        }

        public static async Task SeedWorkingHoursAsync(AppDbContext db)
        {
            var workDays = new[]
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday
            };

            var barbers = await db.Barbers.ToListAsync();

            foreach (var barber in barbers)
            {
                // daca frizerul are deja program, nu il atingem (nu suprascriem modificarile lui)
                if (await db.WorkingHours.AnyAsync(w => w.BarberId == barber.Id))
                {
                    continue;
                }

                foreach (var day in workDays)
                {
                    db.WorkingHours.Add(new WorkingHours
                    {
                        BarberId = barber.Id,
                        DayOfWeek = day,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(17, 0)
                    });
                }
            }

            await db.SaveChangesAsync();
        }
    }
}
