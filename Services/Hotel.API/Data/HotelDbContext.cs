using Hotel.API.Entities;
using Hotel.API.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hotel.API.Data
{
    public class HotelDbContext : DbContext
    {
        public HotelDbContext(DbContextOptions<HotelDbContext> options) : base(options) { }

        public DbSet<HotelEntity> Hotels { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<RoomType> RoomTypes { get; set; }

        // Reviews live in Hotel.API's DB because they're tightly coupled to hotel data.
        // The AI pipeline writes back to this table via the approved/rejected consumers.
        public DbSet<ReviewEntity> Reviews { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<HotelEntity>()
                .HasMany(h => h.Rooms)
                .WithOne(r => r.Hotel)
                .HasForeignKey(r => r.HotelId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HotelEntity>()
                .HasMany(h => h.RoomTypes)
                .WithOne(rt => rt.Hotel)
                .HasForeignKey(rt => rt.HotelId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Room>()
                .HasOne(r => r.RoomType)
                .WithMany()
                .HasForeignKey(r => r.RoomTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Reviews cascade-delete with the hotel — if a hotel is removed, its reviews go too.
            // We also index HotelId + UserId together because the "has this user reviewed this hotel?"
            // check runs on every review submission and we don't want a full table scan.
            modelBuilder.Entity<ReviewEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Hotel)
                      .WithMany(h => h.Reviews)
                      .HasForeignKey(e => e.HotelId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.HotelId, e.UserId });
                entity.Property(e => e.Comment).HasMaxLength(2000);
                entity.Property(e => e.GuestName).HasMaxLength(200);
                entity.Property(e => e.ToxicityScore).HasColumnType("real");
            });


            modelBuilder.Entity<HotelEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Location).HasMaxLength(200);
                entity.Property(e => e.PricePerNight).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Rating).HasColumnType("decimal(3,2)");
                entity.Property(e => e.ManagerId).HasMaxLength(450);
            });

            modelBuilder.Entity<RoomType>(entity =>
            {
                entity.Property(e => e.PricePerNight).HasColumnType("decimal(18,2)");
            });
        }
    }
}
