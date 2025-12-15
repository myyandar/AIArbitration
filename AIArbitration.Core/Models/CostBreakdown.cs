using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class CostBreakdown
    {
        public decimal InputCost { get; set; }
        public decimal OutputCost { get; set; }
        public decimal ServiceFee { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total => InputCost + OutputCost + ServiceFee + Tax - Discount;

        public Dictionary<string, decimal> AdditionalCharges { get; set; } = new();
    }
}
