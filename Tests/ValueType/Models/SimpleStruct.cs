using DeepEqual.Generator.Shared;

namespace Tests.ValueType.Models
{
    [DeepComparable]
    public partial struct SimpleStruct
    {
        public int Count { get; set; }
        public double Ratio { get; set; }
        public decimal Price { get; set; }
        public DateTime WhenUtc { get; set; }
        public SRole Role { get; set; }
    }
}
