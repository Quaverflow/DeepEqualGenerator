using DeepEqual.Generator.Attributes;

namespace Tests;

[DeepComparable]
public class Person
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Role Role { get; set; }

    public Person? Manager { get; set; }
    public List<Person> Reports { get; set; } = [];

    private int SecretScore { get; set; }

    public void SetSecret(int s) => SecretScore = s;
}