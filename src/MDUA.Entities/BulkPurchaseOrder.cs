using System;
using System.Runtime.Serialization;
using System.ServiceModel;

using MDUA.Framework;
using MDUA.Entities.Bases;
using MDUA.Entities.List;

namespace MDUA.Entities
{
	public partial class BulkPurchaseOrder 
	{
        public class BulkOrderItemViewModel
        {
            // Added IDs for database operations
            public int PoRequestId { get; set; }
            public int ProductVariantId { get; set; }

            public string ProductName { get; set; }
            public string VariantName { get; set; }
            public int Quantity { get; set; }
            public string Status { get; set; }
            public DateTime RequestDate { get; set; }
        }
    }
}
