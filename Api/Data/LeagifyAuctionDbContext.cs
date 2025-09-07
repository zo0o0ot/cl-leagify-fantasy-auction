using Microsoft.EntityFrameworkCore;
using LeagifyFantasyAuction.Api.Models;

namespace LeagifyFantasyAuction.Api.Data;

public class LeagifyAuctionDbContext : DbContext
{
    public LeagifyAuctionDbContext(DbContextOptions<LeagifyAuctionDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<School> Schools { get; set; }
    public DbSet<Auction> Auctions { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<RosterPosition> RosterPositions { get; set; }
    public DbSet<AuctionSchool> AuctionSchools { get; set; }
    public DbSet<DraftPick> DraftPicks { get; set; }
    public DbSet<BidHistory> BidHistories { get; set; }
    public DbSet<NominationOrder> NominationOrders { get; set; }
    public DbSet<AdminAction> AdminActions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // School configuration
        modelBuilder.Entity<School>(entity =>
        {
            entity.HasKey(e => e.SchoolId);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.ModifiedDate).HasDefaultValueSql("GETUTCDATE()");
        });

        // Auction configuration
        modelBuilder.Entity<Auction>(entity =>
        {
            entity.HasKey(e => e.AuctionId);
            // Temporarily disable unique constraints for testing
            // entity.HasIndex(e => e.JoinCode).IsUnique();
            // entity.HasIndex(e => e.MasterRecoveryCode).IsUnique();
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.ModifiedDate).HasDefaultValueSql("GETUTCDATE()");

            // Foreign key relationships
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false); // Allow null CreatedByUserId for system-created auctions

            entity.HasOne(e => e.CurrentNominatorUser)
                .WithMany()
                .HasForeignKey(e => e.CurrentNominatorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CurrentSchool)
                .WithMany()
                .HasForeignKey(e => e.CurrentSchoolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CurrentHighBidderUser)
                .WithMany()
                .HasForeignKey(e => e.CurrentHighBidderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasIndex(e => new { e.AuctionId, e.DisplayName }).IsUnique();
            entity.HasIndex(e => e.ConnectionId);
            entity.Property(e => e.JoinedDate).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.LastActiveDate).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Auction)
                .WithMany(e => e.Users)
                .HasForeignKey(e => e.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Team configuration
        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.TeamId);
            entity.Property(e => e.Budget).HasColumnType("decimal(10,2)");
            entity.Property(e => e.RemainingBudget).HasColumnType("decimal(10,2)");

            entity.HasOne(e => e.Auction)
                .WithMany(e => e.Teams)
                .HasForeignKey(e => e.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(e => e.Teams)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Check constraints
            entity.HasCheckConstraint("CK_Team_Budget_Positive", "[Budget] > 0 AND [RemainingBudget] >= 0 AND [RemainingBudget] <= [Budget]");
        });

        // UserRole configuration
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => e.UserRoleId);
            entity.Property(e => e.AssignedDate).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.User)
                .WithMany(e => e.UserRoles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Team)
                .WithMany(e => e.UserRoles)
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // RosterPosition configuration
        modelBuilder.Entity<RosterPosition>(entity =>
        {
            entity.HasKey(e => e.RosterPositionId);

            entity.HasOne(e => e.Auction)
                .WithMany(e => e.RosterPositions)
                .HasForeignKey(e => e.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Check constraints
            entity.HasCheckConstraint("CK_RosterPosition_Slots_Positive", "[SlotsPerTeam] > 0");
            entity.HasCheckConstraint("CK_RosterPosition_ColorCode_Format", "[ColorCode] LIKE '#[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]'");
        });

        // AuctionSchool configuration
        modelBuilder.Entity<AuctionSchool>(entity =>
        {
            entity.HasKey(e => e.AuctionSchoolId);
            entity.HasIndex(e => new { e.AuctionId, e.SchoolId }).IsUnique();
            entity.HasIndex(e => new { e.AuctionId, e.IsAvailable });

            entity.Property(e => e.ProjectedPoints).HasColumnType("decimal(8,2)");
            entity.Property(e => e.SuggestedAuctionValue).HasColumnType("decimal(10,2)");
            entity.Property(e => e.ProjectedPointsAboveAverage).HasColumnType("decimal(8,2)");
            entity.Property(e => e.ProjectedPointsAboveReplacement).HasColumnType("decimal(8,2)");
            entity.Property(e => e.AveragePointsForPosition).HasColumnType("decimal(8,2)");
            entity.Property(e => e.ReplacementValueAverageForPosition).HasColumnType("decimal(8,2)");

            entity.HasOne(e => e.Auction)
                .WithMany(e => e.AuctionSchools)
                .HasForeignKey(e => e.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.School)
                .WithMany(e => e.AuctionSchools)
                .HasForeignKey(e => e.SchoolId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // DraftPick configuration
        modelBuilder.Entity<DraftPick>(entity =>
        {
            entity.HasKey(e => e.DraftPickId);
            entity.HasIndex(e => e.TeamId);
            entity.Property(e => e.WinningBid).HasColumnType("decimal(10,2)");
            entity.Property(e => e.DraftedDate).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Auction)
                .WithMany(e => e.DraftPicks)
                .HasForeignKey(e => e.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Team)
                .WithMany(e => e.DraftPicks)
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AuctionSchool)
                .WithMany(e => e.DraftPicks)
                .HasForeignKey(e => e.AuctionSchoolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.RosterPosition)
                .WithMany(e => e.DraftPicks)
                .HasForeignKey(e => e.RosterPositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.NominatedByUser)
                .WithMany(e => e.NominatedPicks)
                .HasForeignKey(e => e.NominatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.WonByUser)
                .WithMany(e => e.WonPicks)
                .HasForeignKey(e => e.WonByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Check constraints
            entity.HasCheckConstraint("CK_DraftPick_WinningBid_Positive", "[WinningBid] > 0");
        });

        // BidHistory configuration
        modelBuilder.Entity<BidHistory>(entity =>
        {
            entity.HasKey(e => e.BidHistoryId);
            entity.HasIndex(e => new { e.AuctionSchoolId, e.BidDate });
            entity.Property(e => e.BidAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.BidDate).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Auction)
                .WithMany(e => e.BidHistories)
                .HasForeignKey(e => e.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AuctionSchool)
                .WithMany(e => e.BidHistories)
                .HasForeignKey(e => e.AuctionSchoolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                .WithMany(e => e.BidHistories)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Check constraints
            entity.HasCheckConstraint("CK_BidHistory_Amount_Positive", "[BidAmount] > 0");
        });

        // NominationOrder configuration
        modelBuilder.Entity<NominationOrder>(entity =>
        {
            entity.HasKey(e => e.NominationOrderId);

            entity.HasOne(e => e.Auction)
                .WithMany(e => e.NominationOrders)
                .HasForeignKey(e => e.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(e => e.NominationOrders)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // AdminAction configuration
        modelBuilder.Entity<AdminAction>(entity =>
        {
            entity.HasKey(e => e.AdminActionId);
            entity.Property(e => e.ActionDate).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Auction)
                .WithMany()
                .HasForeignKey(e => e.AuctionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.AdminUser)
                .WithMany()
                .HasForeignKey(e => e.AdminUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}