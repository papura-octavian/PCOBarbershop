using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PCOBarbershop.Server.Models;

namespace PCOBarbershop.Server.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Barber> Barbers => Set<Barber>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<WorkingHours> WorkingHours => Set<WorkingHours>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Barber>()
            .HasOne(b => b.User)
            .WithOne()
            .HasForeignKey<Barber>(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Service>()
            .Property(s => s.Price)
            .HasPrecision(10, 2);

        builder.Entity<WorkingHours>()
            .HasOne(w => w.Barber)
            .WithMany(b => b.Schedule)
            .HasForeignKey(w => w.BarberId)
            .OnDelete(DeleteBehavior.Cascade);

        // un singur interval pe frizer si pe zi
        builder.Entity<WorkingHours>()
            .HasIndex(w => new { w.BarberId, w.DayOfWeek })
            .IsUnique();

        builder.Entity<Appointment>(e =>
        {
            e.HasOne(a => a.Client).WithMany()
                .HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Barber).WithMany(b => b.Appointments)
                .HasForeignKey(a => a.BarberId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Service).WithMany()
                .HasForeignKey(a => a.ServiceId).OnDelete(DeleteBehavior.Restrict);

            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

            e.HasIndex(a => new { a.BarberId, a.StartTime });

            e.Property(a => a.StripeSessionId).HasMaxLength(255);
            e.Property(a => a.StripePaymentIntentId).HasMaxLength(255);
            e.HasIndex(a => a.StripeSessionId);
        });
    }
}