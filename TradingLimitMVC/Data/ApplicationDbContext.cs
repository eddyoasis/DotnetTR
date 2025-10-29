using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace TradingLimitMVC.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<Company> Companies { get; set; }
        public DbSet<SAPDropdownItem> SAPDropdownItems { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<TradingLimitRequest> TradingLimitRequests { get; set; }
        public DbSet<TradingLimitRequestAttachment> TradingLimitRequestAttachments { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Company>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CompanyCode).IsUnique();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
            });
            modelBuilder.Entity<SAPDropdownItem>(entity =>
            {
                entity.ToTable("SAPDropdownlist");
                entity.HasKey(e => e.ID);
                entity.Property(e => e.TypeID).IsRequired();
                entity.Property(e => e.TypeName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.DDID).IsRequired();
                entity.Property(e => e.DDName).IsRequired();
                entity.Property(e => e.VendorName).HasMaxLength(200);
                entity.Property(e => e.VendorAddress).HasMaxLength(500);
                entity.Property(e => e.ContactPerson).HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(200);
                entity.Property(e => e.FaxNumber).HasMaxLength(50);

                entity.HasIndex(e => new { e.TypeName, e.DDID })
                    .HasDatabaseName("IX_SAPDropdownlist_TypeName_DDID");
            });
            modelBuilder.Entity<SystemSetting>(entity =>
            {
                entity.ToTable("SystemSettings");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Value).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(200);
                entity.Property(e => e.UpdatedDate).IsRequired();
                entity.Property(e => e.UpdatedBy).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => new { e.Category, e.Key })
                    .HasDatabaseName("IX_SystemSettings_Category_Key");
            });






            // Configure TradingLimitRequest
            modelBuilder.Entity<TradingLimitRequest>(entity =>
            {
                entity.ToTable("TradingLimitRequests");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.RequestId).HasMaxLength(50);
                entity.Property(e => e.TRCode).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ClientCode).IsRequired().HasMaxLength(50);
                entity.Property(e => e.RequestType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.BriefDescription).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.GLCurrentLimit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.GLProposedLimit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CurrentCurrentLimit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CurrentProposedLimit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CreatedBy).HasMaxLength(100);
                entity.Property(e => e.ModifiedBy).HasMaxLength(100);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.SubmittedBy).HasMaxLength(100);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");

                entity.HasIndex(e => e.RequestId).IsUnique().HasFilter("[RequestId] IS NOT NULL");
                entity.HasIndex(e => e.TRCode);
                entity.HasIndex(e => e.ClientCode);
                entity.HasIndex(e => e.Status);
            });

            // Configure TradingLimitRequestAttachment
            modelBuilder.Entity<TradingLimitRequestAttachment>(entity =>
            {
                entity.ToTable("TradingLimitRequestAttachments");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.UploadedBy).HasMaxLength(100);
                entity.Property(e => e.UploadDate).HasDefaultValueSql("GETDATE()");

                entity.HasOne(e => e.TradingLimitRequest)
                    .WithMany(tr => tr.Attachments)
                    .HasForeignKey(e => e.TradingLimitRequestId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("RefreshTokens");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ExpiresAt).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.RevokedBy).HasMaxLength(500);
                entity.Property(e => e.RevokeReason).HasMaxLength(1000);
                entity.Property(e => e.ReplacedByToken).HasMaxLength(500);
                entity.Property(e => e.DeviceId).HasMaxLength(50);
                entity.Property(e => e.IpAddress).HasMaxLength(255);
                entity.Property(e => e.UserAgent).HasMaxLength(500);

                // Create indexes for performance
                entity.HasIndex(e => e.Token).IsUnique();
                entity.HasIndex(e => e.Username);
                entity.HasIndex(e => e.ExpiresAt);
                entity.HasIndex(e => new { e.Username, e.DeviceId });
            });
        }
    }
}
