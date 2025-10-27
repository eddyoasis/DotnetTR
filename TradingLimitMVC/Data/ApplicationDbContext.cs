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
        public DbSet<PurchaseRequisition> PurchaseRequisitions { get; set; }
        public DbSet<PurchaseRequisitionItem> PurchaseRequisitionItems { get; set; }
        public DbSet<PurchaseRequisitionApproval> PurchaseRequisitionApprovals { get; set; }
        public DbSet<CostCenter> CostCenters { get; set; }
        public DbSet<ApprovalWorkflowStep> ApprovalWorkflowSteps { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
        public DbSet<PurchaseOrderApproval> PurchaseOrderApprovals { get; set; }
        public DbSet<POApprovalWorkflowStep> POApprovalWorkflowSteps { get; set; }
        public DbSet<POStatusHistory> POStatusHistories { get; set; }



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


            // Configure PurchaseRequisition
            modelBuilder.Entity<PurchaseRequisition>(entity =>
            {
                entity.ToTable("PurchaseRequisitions");
                entity.HasKey(e => e.Id);
                // Basic properties
                entity.Property(e => e.PRReference).HasMaxLength(50);
                entity.Property(e => e.PRInternalNo).HasMaxLength(50);
                entity.Property(e => e.Company).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Department).IsRequired().HasMaxLength(100);
                entity.Property(e => e.DeliveryAddress).HasMaxLength(500);
                entity.Property(e => e.ContactPerson).HasMaxLength(100);
                entity.Property(e => e.ContactPhoneNo).HasMaxLength(50);
                entity.Property(e => e.QuotationCurrency).HasMaxLength(10);
                entity.Property(e => e.ShortDescription).HasMaxLength(500);
                entity.Property(e => e.TypeOfPurchase).HasMaxLength(100);
                entity.Property(e => e.Reason).HasMaxLength(1000);
                entity.Property(e => e.Remarks).HasMaxLength(1000);
                // Status fields
                entity.Property(e => e.CurrentStatus).HasConversion<int>();
                entity.Property(e => e.Priority).HasConversion<int>();
                entity.Property(e => e.SubmittedBy).HasMaxLength(100);
                entity.Property(e => e.CurrentApprover).HasMaxLength(100);
                entity.Property(e => e.FinalApprover).HasMaxLength(100);
                // PO fields
                entity.Property(e => e.POReference).HasMaxLength(50);
                // Distribution fields
                entity.Property(e => e.DistributionType).HasConversion<int>();
                entity.Property(e => e.DistributionTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DistributionCurrency).HasMaxLength(10);
                // Exchange rate fields
                entity.Property(e => e.ExchangeRateToSGD).HasColumnType("decimal(18,4)").HasDefaultValue(1.0m);
                // SAP Concur fields
                entity.Property(e => e.ExpenseCode).HasMaxLength(50);
                entity.Property(e => e.ProjectCode).HasMaxLength(50);
                entity.Property(e => e.VendorFullAddress).HasMaxLength(500);
                // Rejection fields
                entity.Property(e => e.RejectionReason).HasMaxLength(1000);
                entity.Property(e => e.RejectedBy).HasMaxLength(100);
                //Configure relationships with proper cascade behavior
                entity.HasMany(e => e.Items)
                    .WithOne(e => e.PurchaseRequisition)
                    .HasForeignKey(e => e.PurchaseRequisitionId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.Attachments)
                    .WithOne(e => e.PurchaseRequisition)
                    .HasForeignKey(e => e.PurchaseRequisitionId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.Approvals)
                    .WithOne(e => e.PurchaseRequisition)
                    .HasForeignKey(e => e.PurchaseRequisitionId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.CostCenters)
                    .WithOne(e => e.PurchaseRequisition)
                    .HasForeignKey(e => e.PurchaseRequisitionId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.WorkflowSteps)
                    .WithOne(e => e.PurchaseRequisition)
                    .HasForeignKey(e => e.PurchaseRequisitionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure CostCenter
            modelBuilder.Entity<CostCenter>(entity =>
            {
                entity.ToTable("CostCenters");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.PurchaseRequisitionId).HasColumnName("PurchaseRequisitionId");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100).HasColumnName("Name");
                entity.Property(e => e.Distribution).IsRequired().HasMaxLength(50).HasColumnName("Distribution");
                entity.Property(e => e.Percentage).HasColumnType("decimal(5,2)").HasColumnName("Percentage");
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)").HasColumnName("Amount");
                entity.Property(e => e.Approver).IsRequired().HasMaxLength(200).HasColumnName("Approver");
                entity.Property(e => e.ApproverEmail).HasMaxLength(200).HasColumnName("ApproverEmail");
                entity.Property(e => e.ApprovalStatus).HasColumnName("ApprovalStatus").HasConversion<int>();
                entity.Property(e => e.ApprovedBy).HasMaxLength(100).HasColumnName("ApprovedBy");
                entity.Property(e => e.ApprovedDate).HasColumnName("ApprovedDate");
                entity.Property(e => e.ApprovalComments).HasMaxLength(500).HasColumnName("ApprovalComments");
                entity.Property(e => e.ApproverRole).HasMaxLength(100).HasColumnName("ApproverRole");
                entity.Property(e => e.ApprovalOrder).HasColumnName("ApprovalOrder");
                entity.Property(e => e.IsRequired).HasColumnName("IsRequired");

                entity.HasOne(e => e.PurchaseRequisition)
                    .WithMany(pr => pr.CostCenters)
                    .HasForeignKey(e => e.PurchaseRequisitionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure ApprovalWorkflowStep
            modelBuilder.Entity<ApprovalWorkflowStep>(entity =>
            {
                entity.ToTable("ApprovalWorkflowSteps");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.PurchaseRequisitionId).HasColumnName("PurchaseRequisitionId");
                entity.Property(e => e.StepOrder).HasColumnName("StepOrder");
                entity.Property(e => e.ApproverRole).IsRequired().HasMaxLength(100).HasColumnName("ApproverRole");
                entity.Property(e => e.ApproverName).IsRequired().HasMaxLength(100).HasColumnName("ApproverName");
                entity.Property(e => e.ApproverEmail).IsRequired().HasMaxLength(200).HasColumnName("ApproverEmail");
                entity.Property(e => e.Department).HasMaxLength(100).HasColumnName("Department");
                entity.Property(e => e.Status).HasColumnName("Status").HasConversion<int>();
                entity.Property(e => e.ActionDate).HasColumnName("ActionDate");
                entity.Property(e => e.Comments).HasMaxLength(500).HasColumnName("Comments");
                entity.Property(e => e.IsRequired).HasColumnName("IsRequired");
                entity.Property(e => e.IsParallel).HasColumnName("IsParallel");
                entity.Property(e => e.ApprovalAmount).HasColumnType("decimal(18,2)").HasColumnName("ApprovalAmount");

                entity.HasOne(e => e.PurchaseRequisition)
                    .WithMany(pr => pr.WorkflowSteps)
                    .HasForeignKey(e => e.PurchaseRequisitionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure PurchaseRequisitionItem
            modelBuilder.Entity<PurchaseRequisitionItem>(entity =>
            {
                entity.ToTable("PurchaseRequisitionItems");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PurchaseRequisitionId)
                    .HasColumnName("PurchaseRequisitionId")
                    .IsRequired();
                entity.Property(e => e.Action)
                    .HasColumnName("Action")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasDefaultValue("Purchase");
                entity.Property(e => e.Description)
                    .HasColumnName("Description")
                    .HasMaxLength(500);
                entity.Property(e => e.Quantity)
                    .HasColumnName("Quantity")
                    .IsRequired();
                entity.Property(e => e.UnitPrice)
                    .HasColumnName("UnitPrice")
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.Property(e => e.DiscountPercent)
                    .HasColumnName("DiscountPercent")
                    .HasColumnType("decimal(5,2)")
                    .HasDefaultValue(0);
                entity.Property(e => e.DiscountAmount)
                    .HasColumnName("DiscountAmount")
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);
                entity.Property(e => e.Amount)
                    .HasColumnName("Amount")
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.Property(e => e.GST)
                    .HasColumnName("GST")
                    .HasMaxLength(10);
                entity.Property(e => e.SuggestedSupplier)
                    .HasColumnName("SuggestedSupplier")
                    .HasMaxLength(200);
                entity.Property(e => e.PaymentTerms)
                    .HasColumnName("PaymentTerms")
                    .HasMaxLength(100);
                // IsFixedAsset is bool, not string
                entity.Property(e => e.IsFixedAsset)
                    .HasColumnName("IsFixedAsset")
                    .HasDefaultValue(false);
                entity.Property(e => e.AssetsClass)
                    .HasColumnName("AssetsClass")
                    .HasMaxLength(100);
                entity.Property(e => e.MaintenanceFrom)
                    .HasColumnName("MaintenanceFrom")
                    .HasColumnType("date");
                entity.Property(e => e.MaintenanceTo)
                    .HasColumnName("MaintenanceTo")
                    .HasColumnType("date");
                //  Configure the relationship
                entity.HasOne(e => e.PurchaseRequisition)
                    .WithMany(pr => pr.Items)
                    .HasForeignKey(e => e.PurchaseRequisitionId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_PurchaseRequisitionItems_PurchaseRequisitions");
            });

            // Configure PurchaseRequisitionApproval
            modelBuilder.Entity<PurchaseRequisitionApproval>(entity =>
            {
                entity.ToTable("PurchaseRequisitionApprovals");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ApproverName).HasColumnName("ApproverName").IsRequired().HasMaxLength(100);
                entity.Property(e => e.ApproverEmail).HasColumnName("ApproverEmail").IsRequired().HasMaxLength(200);
                entity.Property(e => e.Status).HasColumnName("Status").HasConversion<int>();
                entity.Property(e => e.Comments).HasColumnName("Comments").IsRequired().HasMaxLength(1000);
                entity.Property(e => e.ApprovalDate).HasColumnName("ApprovalDate");
                entity.Property(e => e.ApprovalLevel).HasColumnName("ApprovalLevel");
                entity.Property(e => e.Department).HasColumnName("Department").HasMaxLength(100);
                entity.Property(e => e.EmployeeId).HasColumnName("EmployeeId").HasMaxLength(50);
                entity.Property(e => e.ApproverRole).HasColumnName("ApproverRole").HasMaxLength(100);
                entity.Property(e => e.ApprovalMethod).HasColumnName("ApprovalMethod").HasConversion<int>();
                entity.Property(e => e.IPAddress).HasColumnName("IPAddress").HasMaxLength(50);
                entity.Property(e => e.TeamsMessageId).HasColumnName("TeamsMessageId").HasMaxLength(200);
            });

            // Configure Attachment
            modelBuilder.Entity<Attachment>(entity =>
            {
                entity.ToTable("Attachments");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.FileName).HasColumnName("FileName").IsRequired().HasMaxLength(255);
                entity.Property(e => e.FilePath).HasColumnName("FilePath").IsRequired().HasMaxLength(500);
                entity.Property(e => e.FileSize).HasColumnName("FileSize");
                entity.Property(e => e.UploadDate).HasColumnName("UploadDate");
                entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(100);
            });

            // Configure PurchaseOrder
            modelBuilder.Entity<PurchaseOrder>(entity =>
            {
                entity.ToTable("PurchaseOrders");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.POReference).HasColumnName("POReference").HasMaxLength(50);
                entity.Property(e => e.PONo).HasColumnName("PONo").HasMaxLength(50);
                entity.Property(e => e.IssueDate).HasColumnName("IssueDate");
                entity.Property(e => e.DeliveryDate).HasColumnName("DeliveryDate");
                entity.Property(e => e.POOriginator).HasColumnName("POOriginator").HasMaxLength(100);
                entity.Property(e => e.OrderIssuedBy).HasColumnName("OrderIssuedBy").HasMaxLength(100);
                entity.Property(e => e.PaymentTerms).HasColumnName("PaymentTerms").HasMaxLength(100);
                entity.Property(e => e.Company).HasColumnName("Company").IsRequired().HasMaxLength(100);
                entity.Property(e => e.DeliveryAddress).HasColumnName("DeliveryAddress").HasMaxLength(500);
                entity.Property(e => e.Vendor).HasColumnName("Vendor").IsRequired().HasMaxLength(100);
                entity.Property(e => e.VendorAddress).HasColumnName("VendorAddress").HasMaxLength(500);
                entity.Property(e => e.Attention).HasColumnName("Attention").HasMaxLength(100);
                entity.Property(e => e.PhoneNo).HasColumnName("PhoneNo").HasMaxLength(50);
                entity.Property(e => e.Email).HasColumnName("Email").HasMaxLength(200);
                entity.Property(e => e.FaxNo).HasColumnName("FaxNo").HasMaxLength(50);
                entity.Property(e => e.PRReference).HasColumnName("PRReference").HasMaxLength(50);
                entity.Property(e => e.PurchaseRequisitionId).HasColumnName("PurchaseRequisitionId");
                entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate");
                entity.Property(e => e.POStatus).HasColumnName("POStatus").HasMaxLength(50);
                entity.Property(e => e.Remarks).HasColumnName("Remarks").HasMaxLength(1000);

                entity.Property(e => e.CSPreparedBy).HasColumnName("CSPreparedBy").HasMaxLength(100);
                entity.Property(e => e.PaymentStatus).HasColumnName("PaymentStatus").HasMaxLength(50);
                entity.Property(e => e.CSRemarks).HasColumnName("CSRemarks").HasMaxLength(500);
                entity.Property(e => e.ITPersonnel).HasColumnName("ITPersonnel").HasMaxLength(100);
                entity.Property(e => e.ITContract).HasColumnName("ITContract").HasMaxLength(200);
                entity.Property(e => e.ITRemarks).HasColumnName("ITRemarks").HasMaxLength(500);
                entity.Property(e => e.MaintenanceFrom).HasColumnName("MaintenanceFrom");
                entity.Property(e => e.MaintenanceTo).HasColumnName("MaintenanceTo");
                entity.Property(e => e.MaintenanceNotes).HasColumnName("MaintenanceNotes").HasMaxLength(500);
                entity.Property(e => e.ActionTime).HasColumnName("ActionTime");
                entity.Property(e => e.ActionBy).HasColumnName("ActionBy").HasMaxLength(100);
                entity.Property(e => e.SelectedPRId).HasColumnName("SelectedPRId");

                entity.HasOne(e => e.PurchaseRequisition)
                    .WithMany()
                    .HasForeignKey(e => e.PurchaseRequisitionId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(e => e.Items)
                    .WithOne(e => e.PurchaseOrder)
                    .HasForeignKey(e => e.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure PurchaseOrderItem
            modelBuilder.Entity<PurchaseOrderItem>(entity =>
            {
                entity.ToTable("PurchaseOrderItems");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Description).HasColumnName("Description").HasMaxLength(500);
                entity.Property(e => e.Details).HasColumnName("Details").HasMaxLength(1000);
                entity.Property(e => e.Quantity).HasColumnName("Quantity").IsRequired();
                entity.Property(e => e.UnitPrice).HasColumnName("UnitPrice").HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountPercent).HasColumnName("DiscountPercent").HasColumnType("decimal(5,2)");
                entity.Property(e => e.DiscountAmount).HasColumnName("DiscountAmount").HasColumnType("decimal(18,2)");
                entity.Property(e => e.GST).HasColumnName("GST").HasMaxLength(10);
                entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)");
                entity.Property(e => e.PRNo).HasColumnName("PRNo").HasMaxLength(50);
            });

            // Configure POStatusHistory
            modelBuilder.Entity<POStatusHistory>(entity =>
            {
                entity.ToTable("POStatusHistories");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Status).HasColumnName("Status").IsRequired().HasMaxLength(100);
                entity.Property(e => e.ActionTime).HasColumnName("ActionTime").IsRequired();
                entity.Property(e => e.ActionBy).HasColumnName("ActionBy").IsRequired().HasMaxLength(100);
                entity.Property(e => e.Remarks).HasColumnName("Remarks").HasMaxLength(1000);

                entity.HasOne(e => e.PurchaseOrder)
                    .WithMany()
                    .HasForeignKey(e => e.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<POApprovalWorkflowStep>(entity =>
            {
                entity.ToTable("POApprovalWorkflowSteps");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.StepOrder).HasColumnName("StepOrder");
                entity.Property(e => e.ApproverRole).IsRequired().HasMaxLength(100).HasColumnName("ApproverRole");
                entity.Property(e => e.ApproverName).IsRequired().HasMaxLength(100).HasColumnName("ApproverName");
                entity.Property(e => e.ApproverEmail).IsRequired().HasMaxLength(200).HasColumnName("ApproverEmail");
                entity.Property(e => e.Department).HasMaxLength(100).HasColumnName("Department");
                entity.Property(e => e.Status).HasColumnName("Status").HasConversion<int>();
                entity.Property(e => e.ActionDate).HasColumnName("ActionDate");
                entity.Property(e => e.Comments).HasMaxLength(500).HasColumnName("Comments");
                entity.Property(e => e.IsRequired).HasColumnName("IsRequired");
                entity.HasOne(e => e.PurchaseOrder)
                    .WithMany(po => po.WorkflowSteps)
                    .HasForeignKey(e => e.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PurchaseOrderApproval>(entity =>
            {
                entity.ToTable("PurchaseOrderApprovals");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ApproverName).IsRequired().HasMaxLength(100).HasColumnName("ApproverName");
                entity.Property(e => e.ApproverEmail).IsRequired().HasMaxLength(200).HasColumnName("ApproverEmail");
                entity.Property(e => e.Status).HasColumnName("Status").HasConversion<int>();
                entity.Property(e => e.Comments).HasMaxLength(1000).HasColumnName("Comments");
                entity.Property(e => e.ApprovalDate).HasColumnName("ApprovalDate");
                entity.Property(e => e.ApprovalLevel).HasColumnName("ApprovalLevel");
                entity.Property(e => e.Department).HasMaxLength(100).HasColumnName("Department");
                entity.Property(e => e.ApprovalMethod).HasColumnName("ApprovalMethod").HasConversion<int>();

                entity.HasOne(e => e.PurchaseOrder)
                    .WithMany(po => po.Approvals)
                    .HasForeignKey(e => e.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
