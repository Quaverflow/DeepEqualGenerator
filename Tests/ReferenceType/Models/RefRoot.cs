using DeepEqual.Generator.Shared;

namespace DeepEqual.Tests
{

    [DeepComparable]
    public class RefRoot
    {
        public string Title { get; set; } = "";
        public RefChild Child { get; set; } = new RefChild();
        public List<RefChild> Items { get; set; } = new();
    }
}
