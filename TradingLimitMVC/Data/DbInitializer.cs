using TradingLimitMVC.Models;

namespace TradingLimitMVC.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            // Ensure database is created
            context.Database.EnsureCreated();

            // Check if data already exists
            if (context.PurchaseRequisitions.Any())
            {
                return; // DB has been seeded
            }
            try
            {
                // Add basic sample data
                var samplePR = new PurchaseRequisition
                {
                    PRReference = "PR-2024-001",
                    PRInternalNo = "1741",
                    Company = "Company A",
                    Department = "Information Technology",
                    IsITRelated = true,
                    ExpectedDeliveryDate = DateTime.Now.AddDays(30),
                    DeliveryAddress = "123 Technology Park, Silicon Valley",
                    ContactPerson = "John Smith",
                    ContactPhoneNo = "+1-555-0123",
                    QuotationCurrency = "USD",
                    ShortDescription = "Computer Hardware Upgrade",
                    TypeOfPurchase = "Equipment",
                    Reason = "Annual hardware refresh for development team",
                    CreatedDate = DateTime.Now.AddDays(-5),
                    CurrentStatus = WorkflowStatus.Submitted,
                    SubmittedDate = DateTime.Now.AddDays(-4),
                    SubmittedBy = "UserA"
                };
                context.PurchaseRequisitions.Add(samplePR);
                context.SaveChanges();
                // Add sample items
                var sampleItems = new List<PurchaseRequisitionItem>
            {
                new PurchaseRequisitionItem
                {
                    PurchaseRequisitionId = samplePR.Id,
                    Action = "Purchase",
                    Description = "Dell Laptop i7",
                    Quantity = 5,
                    UnitPrice = 1200.00m,
                    DiscountAmount = 100.00m,
                    Amount = 5500.00m,
                    GST = "7%",
                    SuggestedSupplier = "Dell Technologies",
                    PaymentTerms = "Net 30",
                    //FixedAssets = "Computer Equipment",
                    AssetsClass = "IT Hardware"
                }
            };
                context.PurchaseRequisitionItems.AddRange(sampleItems);
                context.SaveChanges();
            }
            catch (Exception ex)
            {
                // Log the error but don't crash the application
                Console.WriteLine($"Error seeding database: {ex.Message}");
            }
        }
    }

}
