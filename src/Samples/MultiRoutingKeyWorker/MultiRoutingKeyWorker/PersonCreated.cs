namespace MultiRoutingKeyWorker;

public record PersonCreated(int Id, string Name, int Age, Gender Gender);

public enum Gender
{
    Male,
    Female
}