using System.Dynamic;

namespace DeepEqual.Generator.Benchmarking;

public class OrgNode
{
    public string Name { get; set; } = "";
    public Role Role { get; set; }
    public List<OrgNode> Reports { get; set; } = new();
    public IDictionary<string, object?> Extra { get; set; } = new ExpandoObject();
}