using TradingLimitMVC.Models;

namespace TradingLimitMVC.Data
{
    public static class SeedData
    {
        public static class DropdownOptions
        {
            public static readonly string[] Companies = {
            "Company A",
            "Company B",
            "Company C"
            };


            public static readonly string[] Departments = {
            "Information Technology",
            "Human Resources",
            "Finance",
            "Operations",
            "Marketing",
            "Sales"
            };

            public static readonly string[] Currencies = {
            "USD",
            "EUR",
            "SGD",
            "MYR"
            };

            public static readonly string[] PurchaseTypes = {
            "Equipment",
            "Services",
            "Software",
            "Supplies",
            "Maintenance"
            };

            public static readonly string[] PaymentTerms = {
            "Net 30",
            "Net 60",
            "COD",
            "Prepaid"
            };

            public static readonly string[] VendorList = {
            "ABC Supplies Ltd",
            "Tech Solutions Inc",
            "Office Equipment Co",
            "Software Systems LLC",
            "Industrial Parts Ltd"
            };
        }

        public static void SeedDatabase(ApplicationDbContext context)
        {
            // Sample Purchase Requisition
            if (!context.PurchaseRequisitions.Any())
            {
                var samplePR = new PurchaseRequisition
                {
                    PRReference = "PR-2024-001",
                    PRInternalNo = "INT-001",
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
                    CreatedDate = DateTime.Now.AddDays(-5)
                };

                context.PurchaseRequisitions.Add(samplePR);

                // Sample Purchase Requisition Items
                var sampleItems = new List<PurchaseRequisitionItem>
                {
                new PurchaseRequisitionItem
                {
                    PurchaseRequisition = samplePR,
                    Action = "Purchase",
                    Description = "Dell Laptop i7",
                    Quantity = 5,
                    UnitPrice = 1200.00m,
                    Amount = 6000.00m,
                    GST = "7%"
                },
                new PurchaseRequisitionItem
                {
                    PurchaseRequisition = samplePR,
                    Action = "Purchase",
                    Description = "External Monitor 24\"",
                    Quantity = 5,
                    UnitPrice = 300.00m,
                    Amount = 1500.00m,
                    GST = "7%"
                }
            };

                context.PurchaseRequisitionItems.AddRange(sampleItems);
            }

            // Sample Purchase Order
            if (!context.PurchaseOrders.Any())
            {
                var samplePO = new PurchaseOrder
                {
                    POReference = "PO-2024-001",
                    PONo = "0001",
                    IssueDate = DateTime.Now.AddDays(-3),
                    DeliveryDate = DateTime.Now.AddDays(25),
                    Company = "Company A",
                    Vendor = "Tech Solutions Inc",
                    VendorAddress = "456 Vendor Street, Tech City",
                    Attention = "Sales Department",
                    PhoneNo = "+1-555-0456",
                    Email = "sales@techsolutions.com",
                    OrderIssuedBy = "Jane Doe",
                    CreatedDate = DateTime.Now.AddDays(-3)
                };

                context.PurchaseOrders.Add(samplePO);

                // Sample Purchase Order Items
                var samplePOItems = new List<PurchaseOrderItem>
            {
                new PurchaseOrderItem
                {
                    PurchaseOrder = samplePO,
                    Description = "Dell Laptop i7",
                    Details = "16GB RAM, 512GB SSD",
                    Quantity = 5,
                    UnitPrice = 1200.00m,
                    Amount = 6000.00m,
                    GST = "7%",
                    PRNo = "PR-2024-001"
                }
            };

                context.PurchaseOrderItems.AddRange(samplePOItems);
            }

            context.SaveChanges();
        }
    }


}
